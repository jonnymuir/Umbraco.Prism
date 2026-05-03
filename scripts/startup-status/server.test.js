'use strict';

// Regression tests for startup-status URL generation.
//
// Run with: node --test scripts/startup-status/server.test.js
//
// These tests guard the URL derivation logic that was responsible for:
//   1. Browser download prompts — when port 3000 had no listener, the Codespaces
//      proxy returned HTTP 404 with no Content-Type and x-content-type-options:nosniff,
//      which Chrome treated as an unknown blob and offered as a file download.
//   2. Repeated 404s — broken terminal URLs (https: instead of https://) caused by
//      `tr -d '/'` stripping all slashes; and legacy CODESPACE_NAME fallback producing
//      wrong URLs on new-scheme regional Codespaces.

const { test, describe } = require('node:test');
const assert = require('node:assert/strict');
const { parseCodespacePorts, deriveCodespacesUrl, makePublicUrl } = require('./url-utils.js');

// ── parseCodespacePorts ──────────────────────────────────────────────────────

describe('parseCodespacePorts', () => {
  test('returns empty Map for empty string', () => {
    const result = parseCodespacePorts('');
    assert.equal(result.size, 0);
  });

  test('returns empty Map for empty JSON array — the post-stop.sh scenario', () => {
    // Regression: after stop.sh kills all services, gh codespace ports returns [].
    // An empty Map triggers the legacy CODESPACE_NAME fallback in publicUrl.
    const result = parseCodespacePorts('[]');
    assert.equal(result.size, 0);
  });

  test('returns empty Map for invalid JSON', () => {
    assert.equal(parseCodespacePorts('not-json').size, 0);
  });

  test('returns empty Map for null-ish input', () => {
    assert.equal(parseCodespacePorts(null).size, 0);
    assert.equal(parseCodespacePorts(undefined).size, 0);
  });

  test('parses a single port entry', () => {
    const json = JSON.stringify([
      { sourcePort: 3000, browseUrl: 'https://abc-3000.northeurope.app.github.dev/' },
    ]);
    const map = parseCodespacePorts(json);
    assert.equal(map.size, 1);
    assert.equal(map.get(3000), 'https://abc-3000.northeurope.app.github.dev');
  });

  test('strips trailing slash from browseUrl', () => {
    // Regression: tr -d '/' was stripping ALL slashes, turning https:// into https:
    // and breaking terminal URLs. Now only trailing slashes are removed.
    const json = JSON.stringify([
      { sourcePort: 3000, browseUrl: 'https://abc-3000.northeurope.app.github.dev/' },
    ]);
    const url = parseCodespacePorts(json).get(3000);
    assert.ok(url.startsWith('https://'), `URL must start with https:// — got: ${url}`);
    assert.ok(!url.endsWith('/'), 'URL must not have trailing slash');
  });

  test('parses all four service ports', () => {
    const json = JSON.stringify([
      { sourcePort: 3000,  browseUrl: 'https://tok-3000.eu.app.github.dev/' },
      { sourcePort: 17214, browseUrl: 'https://tok-17214.eu.app.github.dev/' },
      { sourcePort: 44345, browseUrl: 'https://tok-44345.eu.app.github.dev/' },
      { sourcePort: 8443,  browseUrl: 'https://tok-8443.eu.app.github.dev/' },
    ]);
    const map = parseCodespacePorts(json);
    assert.equal(map.size, 4);
    assert.equal(map.get(44345), 'https://tok-44345.eu.app.github.dev');
  });

  test('skips entries missing sourcePort or browseUrl', () => {
    const json = JSON.stringify([
      { sourcePort: 'not-a-number', browseUrl: 'https://tok-1000.eu.app.github.dev/' },
      { sourcePort: 3000 },
      { browseUrl: 'https://tok-9999.eu.app.github.dev/' },
      { sourcePort: 44345, browseUrl: 'https://tok-44345.eu.app.github.dev/' },
    ]);
    const map = parseCodespacePorts(json);
    assert.equal(map.size, 1);
    assert.equal(map.get(44345), 'https://tok-44345.eu.app.github.dev');
  });
});

// ── deriveCodespacesUrl ──────────────────────────────────────────────────────

describe('deriveCodespacesUrl', () => {
  test('derives correct URL for new regional scheme', () => {
    // https://{opaque-token}-{port}.{region}.app.github.dev
    const result = deriveCodespacesUrl(
      'https://abc123xyz-3000.northeurope.app.github.dev',
      44345,
    );
    assert.equal(result, 'https://abc123xyz-44345.northeurope.app.github.dev');
  });

  test('derives correct URL for legacy scheme', () => {
    // https://{CODESPACE_NAME}-{port}.app.github.dev
    const result = deriveCodespacesUrl(
      'https://friendly-code-12345-3000.app.github.dev',
      44345,
    );
    assert.equal(result, 'https://friendly-code-12345-44345.app.github.dev');
  });

  test('preserves regional suffix (.eastus.app.github.dev)', () => {
    const result = deriveCodespacesUrl(
      'https://tok-3000.eastus.app.github.dev',
      8443,
    );
    assert.equal(result, 'https://tok-8443.eastus.app.github.dev');
  });

  test('preserves regional suffix (.westeurope.app.github.dev)', () => {
    const result = deriveCodespacesUrl(
      'https://tok-17214.westeurope.app.github.dev',
      44345,
    );
    assert.equal(result, 'https://tok-44345.westeurope.app.github.dev');
  });

  test('returns null when hostname has no dot', () => {
    assert.equal(deriveCodespacesUrl('https://localhost:3000', 44345), null);
  });

  test('returns null when no dash before first dot', () => {
    assert.equal(deriveCodespacesUrl('https://abc.app.github.dev', 44345), null);
  });

  test('returns null when segment before first dot is not purely numeric', () => {
    assert.equal(deriveCodespacesUrl('https://abc-notaport.app.github.dev', 44345), null);
  });

  test('returns null for invalid URL', () => {
    assert.equal(deriveCodespacesUrl('not-a-url', 44345), null);
  });
});

// ── makePublicUrl (publicUrl factory) ────────────────────────────────────────

describe('makePublicUrl', () => {
  const regionalMap = new Map([
    [3000, 'https://abc123xyz-3000.northeurope.app.github.dev'],
  ]);
  const fullMap = new Map([
    [3000,  'https://abc123xyz-3000.northeurope.app.github.dev'],
    [17214, 'https://abc123xyz-17214.northeurope.app.github.dev'],
    [44345, 'https://abc123xyz-44345.northeurope.app.github.dev'],
    [8443,  'https://abc123xyz-8443.northeurope.app.github.dev'],
    [7245,  'https://abc123xyz-7245.northeurope.app.github.dev'],
  ]);

  test('returns HTTPS localhost URL when not in Codespaces', () => {
    const publicUrl = makePublicUrl({ codespaceName: '', domain: 'app.github.dev', portUrls: new Map() });
    assert.equal(publicUrl(44345), 'https://localhost:44345');
  });

  test('respects localScheme option for non-HTTPS services on localhost', () => {
    const publicUrl = makePublicUrl({ codespaceName: '', domain: 'app.github.dev', portUrls: new Map() });
    assert.equal(publicUrl(17214, { localScheme: 'https' }), 'https://localhost:17214');
  });

  test('returns exact URL from map when port is registered', () => {
    const publicUrl = makePublicUrl({ codespaceName: 'myspace', domain: 'app.github.dev', portUrls: fullMap });
    assert.equal(publicUrl(44345), 'https://abc123xyz-44345.northeurope.app.github.dev');
  });

  test('derives URL for unregistered port when any other port is known', () => {
    // port 3000 is known; derive URLs for 44345, 8443, 7245, 17214
    const publicUrl = makePublicUrl({ codespaceName: 'myspace', domain: 'app.github.dev', portUrls: regionalMap });
    assert.equal(publicUrl(44345), 'https://abc123xyz-44345.northeurope.app.github.dev');
    assert.equal(publicUrl(8443),  'https://abc123xyz-8443.northeurope.app.github.dev');
    assert.equal(publicUrl(17214), 'https://abc123xyz-17214.northeurope.app.github.dev');
  });

  test('derived URLs preserve the regional domain suffix', () => {
    const portUrls = new Map([[3000, 'https://tok-3000.westeurope.app.github.dev']]);
    const publicUrl = makePublicUrl({ codespaceName: 'myspace', domain: 'app.github.dev', portUrls });
    const url = publicUrl(7245);
    assert.equal(url, 'https://tok-7245.westeurope.app.github.dev');
  });

  test('regression: empty portUrls falls back to legacy CODESPACE_NAME pattern', () => {
    // This is the failure scenario: stop.sh kills all services, gh codespace ports returns [],
    // portUrls is empty, and publicUrl falls back to the CODESPACE_NAME-based URL.
    // On new-scheme regional Codespaces the CODESPACE_NAME token ≠ the opaque subdomain token,
    // so this URL is wrong — it 404s and Chrome shows a download prompt.
    // Fix: ensure at least one port entry is in PRISM_CODESPACE_PORTS_JSON at server start
    // (see on-start.sh: gh codespace ports is called before node server.js launches).
    const publicUrl = makePublicUrl({
      codespaceName: 'intelligent-space-abc123',
      domain: 'app.github.dev',
      portUrls: new Map(),
    });
    const url = publicUrl(44345);
    // Documents current fallback behaviour; on regional Codespaces this URL is incorrect.
    assert.equal(url, 'https://intelligent-space-abc123-44345.app.github.dev');
  });

  test('all four service port URLs have https:// scheme in Codespaces', () => {
    // Guard: no URL should ever come back as https: (missing //) due to slash-stripping.
    const publicUrl = makePublicUrl({ codespaceName: 'myspace', domain: 'app.github.dev', portUrls: fullMap });
    const ports = [17214, 44345, 8443, 7245];
    for (const port of ports) {
      const url = publicUrl(port);
      assert.ok(url.startsWith('https://'), `port ${port} URL missing https:// — got: ${url}`);
    }
  });

  test('status page URL (port 3000) derives correctly when other ports are known', () => {
    // Regression for resume scenario: status server dies during suspension,
    // port 3000 is not in CODESPACE_PORT_URLS but other service ports are.
    // publicUrl(3000) must derive from a known service URL, not fall back to legacy.
    const resumeMap = new Map([
      [17214, 'https://abc123xyz-17214.northeurope.app.github.dev'],
      [44345, 'https://abc123xyz-44345.northeurope.app.github.dev'],
    ]);
    const publicUrl = makePublicUrl({ codespaceName: 'myspace', domain: 'app.github.dev', portUrls: resumeMap });
    const url = publicUrl(3000);
    assert.equal(url, 'https://abc123xyz-3000.northeurope.app.github.dev');
  });
});
