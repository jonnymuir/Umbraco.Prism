'use strict';

// Pure URL helpers extracted from server.js for testability.
// These functions are the core logic for generating public Codespaces URLs.

/**
 * Parses the JSON blob from `gh codespace ports --json sourcePort,browseUrl`
 * into a Map<port, url> with trailing slashes stripped.
 * Returns an empty Map for any falsy, empty, or malformed input.
 */
function parseCodespacePorts(json) {
  if (!json) {
    return new Map();
  }

  try {
    const ports = JSON.parse(json);
    return new Map(
      ports
        .filter((entry) => typeof entry?.sourcePort === 'number' && typeof entry?.browseUrl === 'string')
        .map((entry) => [entry.sourcePort, entry.browseUrl.replace(/\/$/, '')]),
    );
  } catch {
    return new Map();
  }
}

/**
 * Given a known Codespaces browseUrl (for any port), replaces the port segment
 * in the subdomain to produce a URL for `targetPort`.
 *
 * Handles both URL schemes:
 *   Legacy:   https://{CODESPACE_NAME}-{port}.app.github.dev
 *   Regional: https://{token}-{port}.{region}.app.github.dev
 *
 * Returns null if the URL cannot be parsed or does not contain a port segment.
 */
function deriveCodespacesUrl(knownUrl, targetPort) {
  try {
    const uri = new URL(knownUrl);
    const hostname = uri.hostname;
    const firstDot = hostname.indexOf('.');
    if (firstDot === -1) {
      return null;
    }

    const lastDash = hostname.lastIndexOf('-', firstDot);
    if (lastDash === -1) {
      return null;
    }

    const currentPort = hostname.substring(lastDash + 1, firstDot);
    if (!/^\d+$/.test(currentPort)) {
      return null;
    }

    const prefix = hostname.substring(0, lastDash);
    const suffix = hostname.substring(firstDot);
    return `${uri.protocol}//${prefix}-${targetPort}${suffix}`;
  } catch {
    return null;
  }
}

/**
 * Returns a `publicUrl(port, opts)` function bound to the given Codespace config.
 *
 * Resolution order:
 *   1. Exact entry in portUrls map
 *   2. URL derived from any known entry in portUrls (preserves regional suffix)
 *   3. Legacy fallback: https://{codespaceName}-{port}.{domain}
 *   4. localhost URL when not in Codespaces (codespaceName is empty)
 *
 * NOTE: Step 3 (legacy fallback) produces incorrect URLs on new-scheme regional
 * Codespaces (where the subdomain token ≠ CODESPACE_NAME). This happens when
 * portUrls is empty — typically because `gh codespace ports` was called before
 * any ports were registered (e.g., right after stop.sh kills all services, or on
 * Codespace resume before port 3000 is re-registered). Avoid relying on the
 * legacy fallback on regional Codespaces; prefer ensuring at least one port is
 * present in portUrls before generating URLs.
 */
function makePublicUrl({ codespaceName, domain, portUrls }) {
  return function publicUrl(port, { localScheme = 'https' } = {}) {
    if (!codespaceName) {
      return `${localScheme}://localhost:${port}`;
    }

    const exact = portUrls.get(port);
    if (exact) {
      return exact;
    }

    const knownUrl = portUrls.values().next().value;
    if (knownUrl) {
      const derived = deriveCodespacesUrl(knownUrl, port);
      if (derived) {
        return derived;
      }
    }

    return `https://${codespaceName}-${port}.${domain}`;
  };
}

module.exports = { parseCodespacePorts, deriveCodespacesUrl, makePublicUrl };
