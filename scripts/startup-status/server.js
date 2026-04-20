#!/usr/bin/env node
// Startup status server — runs on port 3000 during Codespace boot.
// Serves the status page immediately and proxies service health checks
// server-side (avoiding CORS issues with the browser polling directly).

const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');

const PORT = 3000;
const CODESPACE_NAME = process.env.CODESPACE_NAME || '';
const DOMAIN = process.env.GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN || 'app.github.dev';

function codespacesUrl(port) {
  if (CODESPACE_NAME) {
    return `https://${CODESPACE_NAME}-${port}.${DOMAIN}`;
  }
  return `https://localhost:${port}`;
}

function probe(url) {
  return new Promise((resolve) => {
    const lib = url.startsWith('https') ? https : http;
    const req = lib.get(url, { rejectUnauthorized: false, timeout: 3000 }, (res) => {
      res.resume();
      resolve(res.statusCode < 500 ? 'ready' : 'pending');
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
            'content-type':           res.headers['content-type']           || '(none)',
            'x-content-type-options': res.headers['x-content-type-options'] || '(none)',
            'content-security-policy': res.headers['content-security-policy'] ? '(present)' : '(none)',
            'location':               res.headers['location']               || null,
          },
          bodyPreview: body,
        });
      });
    });
    req.on('error', (e) => resolve({ error: e.message }));
    req.on('timeout', () => { req.destroy(); resolve({ error: 'timeout' }); });
  });
}

// The Aspire dashboard always runs on HTTPS port 17214 — ASPIRE_ALLOW_UNSECURED_TRANSPORT only
// affects OtlpExporter transport, not the dashboard listener. probe() uses rejectUnauthorized:false
// so the self-signed dev cert is accepted in both local and Codespace environments.
const ASPIRE_PORT = 17214;
const ASPIRE_PROBE_URL = `https://localhost:17214`;

async function getStatus() {
  const [aspire, testsite, keycloak, mocbiz] = await Promise.all([
    probe(ASPIRE_PROBE_URL),
    probe(`http://localhost:9250`),
    probe(`https://localhost:8443/realms/prism-dev`),
    probe(`https://localhost:7245`),
  ]);

  const allReady = [aspire, testsite, keycloak, mocbiz].every(s => s === 'ready');

  return {
    phase: allReady ? 'ready' : 'starting',
    services: [
      { id: 'aspire',   name: 'Aspire Dashboard',    status: aspire,   url: codespacesUrl(ASPIRE_PORT) },
      { id: 'testsite', name: 'TestSite (Umbraco)',   status: testsite, url: codespacesUrl(44345) },
      { id: 'keycloak', name: 'Keycloak (SSO)',        status: keycloak, url: `${codespacesUrl(8443)}/admin` },
      { id: 'mocbiz',   name: 'MockBusinessApp',       status: mocbiz,   url: codespacesUrl(7245) },
    ],
    urls: {
      testSite:    codespacesUrl(44345),
      aspire:      codespacesUrl(ASPIRE_PORT),
      keycloak:    `${codespacesUrl(8443)}/admin`,
    },
    credentials: {
      sso:       { user: 'demo@prism.local',  pass: 'password' },
      backoffice: { user: 'admin@prism.local', pass: 'PrismLocal!12345' },
      keycloak:  { user: 'admin',             pass: 'admin' },
    },
  };
}

const HTML = fs.readFileSync(path.join(__dirname, 'index.html'), 'utf8');

const LOG_FILE = '/tmp/prism-apphost.log';
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
      const files = fs.readdirSync(dir).filter(f => f.startsWith('UmbracoTraceLog') && f.endsWith('.json'));
      for (const f of files) {
        const full = path.join(dir, f);
        const { mtimeMs } = fs.statSync(full);
        if (mtimeMs > latestMtime) { latestMtime = mtimeMs; latestFile = full; }
      }
    } catch (_) {}
  }
  if (!latestFile) return null; // no log yet — TestSite hasn't written anything
  try {
    const content = fs.readFileSync(latestFile, 'utf8');
    const lines = content.split('\n').filter(l => l.trim());
    return { file: path.basename(latestFile), tail: lines.slice(-80).join('\n') };
  } catch (e) {
    return { file: path.basename(latestFile), tail: `Error reading log: ${e.message}` };
  }
}

const server = http.createServer(async (req, res) => {
  if (req.url === '/api/status') {
    const status = await getStatus();
    res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-cache' });
    res.end(JSON.stringify(status));
    return;
  }
  if (req.url === '/api/log') {
    res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-cache' });
    res.end(getLogTail());
    return;
  }
  if (req.url === '/api/testsite-log') {
    const result = getUmbracoLogTail();
    if (!result) {
      res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-cache' });
      res.end(JSON.stringify({ available: false, message: 'No Umbraco log found yet — TestSite may still be in early startup or has not started.' }));
    } else {
      res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-cache' });
      res.end(JSON.stringify({ available: true, file: result.file, tail: result.tail }));
    }
    return;
  }
  if (req.url === '/api/diag') {
    const [root, blazor] = await Promise.all([
      probeWithHeaders('https://localhost:17214/'),
      probeWithHeaders('https://localhost:17214/_framework/blazor.web.js'),
    ]);
    const baseHref = root.bodyPreview
      ? (root.bodyPreview.match(/<base href="([^"]*)"/)?.[1] || '(not found in response)')
      : '(no body)';
    const diag = {
      timestamp: new Date().toISOString(),
      env: {
        DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: process.env.DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS || '(not set)',
        ASPIRE_ALLOW_UNSECURED_TRANSPORT:           process.env.ASPIRE_ALLOW_UNSECURED_TRANSPORT           || '(not set)',
        CODESPACE_NAME:                             process.env.CODESPACE_NAME ? '(set)' : '(not set)',
      },
      dashboard_root:  { url: 'https://localhost:17214/', ...root,   bodyPreview: undefined },
      blazor_web_js:   { url: 'https://localhost:17214/_framework/blazor.web.js', ...blazor, bodyPreview: undefined },
      base_href: baseHref,
    };
    res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-cache' });
    res.end(JSON.stringify(diag, null, 2));
    return;
  }
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  res.end(HTML);
});

server.listen(PORT, () => {
  console.log(`Startup status page: http://localhost:${PORT}`);
});
