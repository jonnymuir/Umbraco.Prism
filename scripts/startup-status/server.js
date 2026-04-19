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

// The Aspire dashboard always runs on HTTPS port 17214 — ASPIRE_ALLOW_UNSECURED_TRANSPORT only
// affects OtlpExporter transport, not the dashboard listener. probe() uses rejectUnauthorized:false
// so the self-signed dev cert is accepted in both local and Codespace environments.
const ASPIRE_PORT = 17214;
const ASPIRE_PROBE_URL = `https://localhost:17214`;

async function getStatus() {
  const [aspire, testsite, keycloak, mocbiz] = await Promise.all([
    probe(ASPIRE_PROBE_URL),
    probe(`https://localhost:44345`),
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
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  res.end(HTML);
});

server.listen(PORT, () => {
  console.log(`Startup status page: http://localhost:${PORT}`);
});
