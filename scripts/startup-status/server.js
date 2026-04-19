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

async function getStatus() {
  const [aspire, testsite, keycloak, mocbiz] = await Promise.all([
    probe(`https://localhost:17214`),
    probe(`https://localhost:44345`),
    probe(`https://localhost:8443/realms/prism-dev`),
    probe(`https://localhost:7245`),
  ]);

  const allReady = [aspire, testsite, keycloak, mocbiz].every(s => s === 'ready');

  return {
    phase: allReady ? 'ready' : 'starting',
    services: [
      { id: 'aspire',   name: 'Aspire Dashboard',    status: aspire,   url: codespacesUrl(17214) },
      { id: 'testsite', name: 'TestSite (Umbraco)',   status: testsite, url: codespacesUrl(44345) },
      { id: 'keycloak', name: 'Keycloak (SSO)',        status: keycloak, url: `${codespacesUrl(8443)}/admin` },
      { id: 'mocbiz',   name: 'MockBusinessApp',       status: mocbiz,   url: codespacesUrl(7245) },
    ],
    urls: {
      testSite:    codespacesUrl(44345),
      aspire:      codespacesUrl(17214),
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

const server = http.createServer(async (req, res) => {
  if (req.url === '/api/status') {
    const status = await getStatus();
    res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-cache' });
    res.end(JSON.stringify(status));
    return;
  }
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  res.end(HTML);
});

server.listen(PORT, () => {
  console.log(`Startup status page: http://localhost:${PORT}`);
});
