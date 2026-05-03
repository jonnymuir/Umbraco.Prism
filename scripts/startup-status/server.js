#!/usr/bin/env node
// Startup status server — runs on port 3000 during Codespace boot.
// Serves the status page immediately and proxies service health checks
// server-side (avoiding CORS issues with the browser polling directly).

const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const { parseCodespacePorts, makePublicUrl } = require('./url-utils.js');

const PORT = Number(process.env.PORT || 3000);
const CODESPACE_NAME = process.env.CODESPACE_NAME || '';
const DOMAIN = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN || 'app.github.dev';
const REPO_ROOT = path.resolve(__dirname, '..', '..');
const STARTUP_STATUS_DIR = process.env.PRISM_STARTUP_LOG_DIR
  || path.join(REPO_ROOT, 'artifacts', 'startup-status');
const CODESPACE_PORTS_JSON = process.env.PRISM_CODESPACE_PORTS_JSON || '';
const CODESPACE_PORT_URLS = parseCodespacePorts(CODESPACE_PORTS_JSON);

const ASPIRE_LOCAL_PORT = Number(process.env.PRISM_STARTUP_ASPIRE_PUBLIC_PORT || 17214);
const ASPIRE_CODESPACES_PORT = Number(process.env.PRISM_STARTUP_ASPIRE_CODESPACES_PUBLIC_PORT || 17214);
const ASPIRE_PROBE_URL = process.env.PRISM_STARTUP_ASPIRE_URL
  || `https://localhost:${ASPIRE_LOCAL_PORT}`;
const TESTSITE_READY_URL = process.env.PRISM_STARTUP_TESTSITE_READY_URL
  || 'https://localhost:44345/api/prism/downstream-demo/seed-contract-ready';
const TESTSITE_PUBLIC_PORT = Number(process.env.PRISM_STARTUP_TESTSITE_PUBLIC_PORT || 44345);
const KEYCLOAK_PROBE_URL = process.env.PRISM_STARTUP_KEYCLOAK_URL
  || 'https://localhost:8443/realms/prism-dev/.well-known/openid-configuration';
const KEYCLOAK_PUBLIC_PORT = Number(process.env.PRISM_STARTUP_KEYCLOAK_PUBLIC_PORT || 8443);
const MOCBIZ_PROBE_URL = process.env.PRISM_STARTUP_MOCBIZ_URL || 'https://localhost:7245/debug/auth';
const MOCBIZ_PUBLIC_PORT = Number(process.env.PRISM_STARTUP_MOCBIZ_PUBLIC_PORT || 7245);

const publicUrl = makePublicUrl({ codespaceName: CODESPACE_NAME, domain: DOMAIN, portUrls: CODESPACE_PORT_URLS });
const ASPIRE_PUBLIC_URL = publicUrl(CODESPACE_NAME ? ASPIRE_CODESPACES_PORT : ASPIRE_LOCAL_PORT);

function wantsHtml(req) {
  return (req.headers.accept || '').includes('text/html');
}

function normalizedPath(req) {
  const pathname = new URL(req.url, `http://${req.headers.host || 'localhost'}`).pathname;
  if (pathname === '/') {
    return pathname;
  }

  return pathname.replace(/\/+$/, '');
}

function trimTrailingSlash(url) {
  return url.replace(/\/$/, '');
}

function writeJson(res, payload) {
  res.writeHead(200, {
    'Content-Type': 'application/json',
    'Cache-Control': 'no-store',
    'X-Content-Type-Options': 'nosniff',
  });
  res.end(JSON.stringify(payload));
}

function writeText(res, payload) {
  res.writeHead(200, {
    'Content-Type': 'text/plain; charset=utf-8',
    'Cache-Control': 'no-store',
    'Content-Disposition': 'inline',
    'X-Content-Type-Options': 'nosniff',
  });
  res.end(payload);
}

function probe(url, isReady = (statusCode) => statusCode >= 200 && statusCode < 500) {
  return new Promise((resolve) => {
    const lib = url.startsWith('https') ? https : http;
    const req = lib.get(url, { rejectUnauthorized: false, timeout: 3000 }, (res) => {
      res.resume();
      resolve(isReady(res.statusCode) ? 'ready' : 'pending');
    });
    req.on('error', () => resolve('pending'));
    req.on('timeout', () => { req.destroy(); resolve('pending'); });
  });
}

// Probe a URL and return status + key response headers + first 500 chars of body.
// Used by /api/diag to inspect the Aspire dashboard without the browser proxy in the way.
function probeWithHeaders(url) {
  return new Promise((resolve) => {
    const lib = url.startsWith('https') ? https : http;
    const req = lib.get(url, { rejectUnauthorized: false, timeout: 5000 }, (res) => {
      let body = '';
      res.on('data', (chunk) => { if (body.length < 500) body += chunk; });
      res.on('end', () => {
        resolve({
          status: res.statusCode,
          headers: {
            'content-type': res.headers['content-type'] || '(none)',
            'x-content-type-options': res.headers['x-content-type-options'] || '(none)',
            'content-security-policy': res.headers['content-security-policy'] ? '(present)' : '(none)',
            'location': res.headers.location || null,
          },
          bodyPreview: body,
        });
      });
    });
    req.on('error', (e) => resolve({ error: e.message }));
    req.on('timeout', () => { req.destroy(); resolve({ error: 'timeout' }); });
  });
}

async function getStatus() {
  const [aspire, testsite, keycloak, mocbiz] = await Promise.all([
    probe(ASPIRE_PROBE_URL),
    probe(TESTSITE_READY_URL, (statusCode) => statusCode === 200),
    probe(KEYCLOAK_PROBE_URL, (statusCode) => statusCode === 200),
    probe(MOCBIZ_PROBE_URL, (statusCode) => statusCode === 200),
  ]);

  const allReady = [aspire, testsite, keycloak, mocbiz].every((status) => status === 'ready');

  return {
    phase: allReady ? 'ready' : 'starting',
    services: [
      { id: 'aspire', name: 'Aspire Dashboard', status: aspire, url: ASPIRE_PUBLIC_URL },
      { id: 'testsite', name: 'TestSite (Umbraco)', status: testsite, url: publicUrl(TESTSITE_PUBLIC_PORT) },
      { id: 'keycloak', name: 'Keycloak (SSO)', status: keycloak, url: `${publicUrl(KEYCLOAK_PUBLIC_PORT)}/admin` },
      { id: 'mocbiz', name: 'MockBusinessApp', status: mocbiz, url: publicUrl(MOCBIZ_PUBLIC_PORT) },
    ],
    urls: {
      testSite: publicUrl(TESTSITE_PUBLIC_PORT),
      aspire: ASPIRE_PUBLIC_URL,
      keycloak: `${publicUrl(KEYCLOAK_PUBLIC_PORT)}/admin`,
    },
    credentials: {
      sso: { user: 'demo@prism.local', pass: 'password' },
      backoffice: { user: 'admin@prism.local', pass: 'PrismLocal!12345' },
      keycloak: { user: 'admin', pass: 'admin' },
    },
  };
}

const HTML = fs.readFileSync(path.join(__dirname, 'index.html'), 'utf8');

const LOG_FILE = process.env.PRISM_APPHOST_LOG_FILE || path.join(STARTUP_STATUS_DIR, 'prism-apphost.log');
const LOG_TAIL_LINES = 50;

function getLogTail() {
  if (!fs.existsSync(LOG_FILE)) {
    return 'AppHost not started yet — log file not found.';
  }
  try {
    const content = fs.readFileSync(LOG_FILE, 'utf8');
    const lines = content.split('\n');
    return lines.slice(-LOG_TAIL_LINES).join('\n').trim() || '(log is empty)';
  } catch (e) {
    return `Error reading log: ${e.message}`;
  }
}

// Locate the most-recently modified Umbraco trace log file across several
// candidate paths (source-tree location when Aspire runs the project in-place,
// plus the runtime-root override in case it was redirected in future).
const UMBRACO_LOG_DIRS = [
  path.join(__dirname, '../../src/UmbracoPrism.TestSite/umbraco/Logs'),
];
if (process.env.PRISM_TESTSITE_RUNTIME_ROOT) {
  UMBRACO_LOG_DIRS.unshift(
    path.join(process.env.PRISM_TESTSITE_RUNTIME_ROOT, 'umbraco/Logs'),
  );
}

function getUmbracoLogTail() {
  let latestFile = null;
  let latestMtime = 0;
  for (const dir of UMBRACO_LOG_DIRS) {
    if (!fs.existsSync(dir)) continue;
    try {
      const files = fs.readdirSync(dir).filter((file) => file.startsWith('UmbracoTraceLog') && file.endsWith('.json'));
      for (const file of files) {
        const full = path.join(dir, file);
        const { mtimeMs } = fs.statSync(full);
        if (mtimeMs > latestMtime) {
          latestMtime = mtimeMs;
          latestFile = full;
        }
      }
    } catch (_) {}
  }
  if (!latestFile) return null;
  try {
    const content = fs.readFileSync(latestFile, 'utf8');
    const lines = content.split('\n').filter((line) => line.trim());
    return { file: path.basename(latestFile), tail: lines.slice(-80).join('\n') };
  } catch (e) {
    return { file: path.basename(latestFile), tail: `Error reading log: ${e.message}` };
  }
}

const server = http.createServer(async (req, res) => {
  const pathname = normalizedPath(req);

  if ((pathname === '/status' || pathname === '/log' || pathname === '/testsite-log') && wantsHtml(req)) {
    res.writeHead(302, { Location: '/', 'Cache-Control': 'no-store' });
    res.end();
    return;
  }

  if (pathname === '/api/status' || pathname === '/status') {
    const status = await getStatus();
    writeJson(res, status);
    return;
  }
  if (pathname === '/api/log' || pathname === '/log') {
    writeText(res, getLogTail());
    return;
  }
  if (pathname === '/api/testsite-log' || pathname === '/testsite-log') {
    const result = getUmbracoLogTail();
    if (!result) {
      writeJson(res, {
        available: false,
        message: 'No Umbraco log found yet — TestSite may still be in early startup or has not started.',
      });
    } else {
      writeJson(res, { available: true, file: result.file, tail: result.tail });
    }
    return;
  }
  if (pathname === '/api/diag') {
    const aspireProbeBaseUrl = trimTrailingSlash(ASPIRE_PROBE_URL);
    const [root, blazor] = await Promise.all([
      probeWithHeaders(`${aspireProbeBaseUrl}/`),
      probeWithHeaders(`${aspireProbeBaseUrl}/_framework/blazor.web.js`),
    ]);
    const baseHref = root.bodyPreview
      ? (root.bodyPreview.match(/<base href="([^"]*)"/)?.[1] || '(not found in response)')
      : '(no body)';
    writeJson(res, {
      timestamp: new Date().toISOString(),
      env: {
        DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: process.env.DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS || '(not set)',
        ASPIRE_ALLOW_UNSECURED_TRANSPORT: process.env.ASPIRE_ALLOW_UNSECURED_TRANSPORT || '(not set)',
        CODESPACE_NAME: process.env.CODESPACE_NAME ? '(set)' : '(not set)',
      },
      dashboard_root: { url: `${aspireProbeBaseUrl}/`, ...root, bodyPreview: undefined },
      blazor_web_js: { url: `${aspireProbeBaseUrl}/_framework/blazor.web.js`, ...blazor, bodyPreview: undefined },
      base_href: baseHref,
    });
    return;
  }
  if (pathname === '/' || pathname === '/index.html') {
    res.writeHead(200, {
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-store',
      'X-Content-Type-Options': 'nosniff',
    });
    res.end(HTML);
    return;
  }

  res.writeHead(404, {
    'Content-Type': 'text/plain; charset=utf-8',
    'Cache-Control': 'no-store',
    'X-Content-Type-Options': 'nosniff',
  });
  res.end('Not found');
});

server.listen(PORT, () => {
  console.log(`Startup status page: http://localhost:${PORT}`);
});
