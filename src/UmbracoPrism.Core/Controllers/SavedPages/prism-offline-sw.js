/*
 * Prism saved pages: the service worker that keeps copies of the site's pages on the device, so a
 * generated app opens its tabs at once and still shows pages it has seen when it has no
 * connection. PrismSavedPagesController serves it at /prism-offline-sw.js, so its scope is the
 * whole site, and prism-offline.js registers it, only inside an app built with saved pages. See
 * Design/mobile-offline-pages.md.
 *
 * - A tab's first page is served from its saved copy, then fetched again in the background.
 * - Every other page comes from the network, falling back to its saved copy when the network
 *   fails or is slow.
 * - Only GET page loads of the site's own pages that the site allows to be stored are saved.
 * - The site's own stylesheets, scripts, fonts and images are saved too, since a saved page is no
 *   use offline without them and the webview's own cache does not reliably keep them. Versioned
 *   ones (with a ?v= hash) never change, so they are served from the saved copy first.
 * - Each page says hello with whether the member is signed in; when that changes, every saved
 *   copy is deleted.
 *
 * The page hears from the worker through postMessage:
 * - { type: 'prism-fresh-copy' }: a newer copy of this page is saved; show it at a quiet moment.
 * - { type: 'prism-offline' }: this page is a saved copy shown because the network failed.
 */
'use strict';

const CACHE = 'prism-saved-pages-v1';
const ASSET_CACHE = 'prism-saved-pages-assets-v1';
const MAX_SAVED_PAGES = 50;
const MAX_SAVED_ASSETS = 300;
const ASSET_DESTINATIONS = ['style', 'script', 'font', 'image'];
const SLOW_NETWORK_MS = 3000;
// A copy saved this recently is served without fetching again, so a page reloaded to show a
// fresh copy does not start another round of fetching.
const JUST_SAVED_MS = 10000;
const MANIFEST_PATH = '/umbraco/prism/mobile/manifest';
const SAVED_AT = 'Prism-Saved-At';
const CONTENT_VERSION = 'Prism-Content-Version';
const NEVER_SAVED = [/^\/auth(\/|$)/i, /^\/umbraco(\/|$)/i, /^\/\.well-known(\/|$)/i];
const STATE_CACHE = 'prism-saved-pages-state';
const STATE_KEY = '/session';
const APP_AGENT_KEY = '/app-user-agent';
// Android's webview sends a worker's requests without what the app appends to its user agent, so
// the worker passes the app's on (see PrismMobileRequestDetection.AppUserAgentHeaderName).
const APP_AGENT_HEADER = 'X-Prism-User-Agent';

let tabPaths = null;
let appUserAgent = null;
let appUserAgentRead = false;
const readingAppUserAgent = caches.open(STATE_CACHE)
  .then((state) => state.match(APP_AGENT_KEY))
  .then((saved) => (saved ? saved.text() : null))
  .then((saved) => { appUserAgent = appUserAgent || saved; })
  .finally(() => { appUserAgentRead = true; });
// Notices for pages that were not ready to hear them yet, by client ID, sent when the page says hello.
const pendingNotices = new Map();

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const names = await caches.keys();
    const kept = [CACHE, ASSET_CACHE, STATE_CACHE];
    await Promise.all(names.filter((name) => name.startsWith('prism-saved-pages') && !kept.includes(name)).map((name) => caches.delete(name)));
    await self.clients.claim();
  })());
});

self.addEventListener('message', (event) => {
  const message = event.data || {};
  if (message.type === 'prism-hello') {
    event.waitUntil(Promise.all([noteSession(message.signedIn === true), noteAppUserAgent(message.userAgent)]));
    sendPending(event.source);
  }
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') return;

  if (request.mode === 'navigate') {
    // Until a page has said what the app's user agent is, the webview loads pages itself, so the
    // site renders them for the app (and the page then says hello).
    if (appUserAgentRead && !appUserAgent) return;
    const key = savedKey(request.url);
    if (key) event.respondWith(serve(event, request, key));
    return;
  }

  if (isSiteAsset(request)) {
    event.respondWith(serveAsset(event, request));
  }
});

function isSiteAsset(request) {
  return ASSET_DESTINATIONS.includes(request.destination) && new URL(request.url).origin === self.location.origin;
}

/** A versioned asset from its saved copy first; any other from the network, or its saved copy offline. */
async function serveAsset(event, request) {
  const cache = await caches.open(ASSET_CACHE);
  const saved = await cache.match(request, { ignoreVary: true });
  if (saved && new URL(request.url).searchParams.has('v')) {
    return saved;
  }

  try {
    const response = await fetch(request);
    if (storable(response)) {
      event.waitUntil(cache.put(request, response.clone()).then(() => trim(cache, MAX_SAVED_ASSETS)));
    }
    return response;
  } catch (error) {
    if (saved) return saved;
    throw error;
  }
}

/** One page load: the fetch event, the request as the app's, where it is saved, and its saved copy. */
async function serve(event, navigation, key) {
  const cache = await caches.open(CACHE);
  const page = {
    event,
    request: await asApp(navigation),
    key,
    cache,
    saved: await cache.match(key),
    tell: (type) => tellPage(event.resultingClientId, type)
  };

  if (page.saved && await isTabPage(key)) {
    if (!savedRecently(page.saved)) {
      event.waitUntil(refresh(page));
    }
    return page.saved;
  }

  return networkFirst(page);
}

/** The network's answer, or the saved copy when the network fails or is slow. */
function networkFirst(page) {
  const network = fetch(page.request).then((response) => {
    page.event.waitUntil(keep(page.cache, page.key, response.clone()));
    return response;
  });

  if (!page.saved) {
    return network;
  }

  return new Promise((resolve) => {
    let settled = false;
    const settle = (response) => {
      if (settled) return false;
      settled = true;
      clearTimeout(timer);
      resolve(response);
      return true;
    };

    const timer = setTimeout(() => {
      settle(page.saved);
      page.event.waitUntil(network.then(
        (response) => { if (changed(page.saved, response)) page.tell('prism-fresh-copy'); },
        () => page.tell('prism-offline')));
    }, SLOW_NETWORK_MS);

    network.then(settle, () => {
      if (settle(page.saved)) page.tell('prism-offline');
    });
  });
}

/** Fetches a page served from its saved copy again, and tells the page when what it shows is out of date. */
async function refresh(page) {
  let response;
  try {
    response = await fetch(page.request);
  } catch {
    page.tell('prism-offline');
    return;
  }

  await keep(page.cache, page.key, response.clone());
  if (changed(page.saved, response)) {
    page.tell('prism-fresh-copy');
  }
}

/** Saves a page the site allows to be stored, and forgets the saved copy of one it no longer does. */
async function keep(cache, key, response) {
  if (!storablePage(response)) {
    await cache.delete(key);
    return;
  }

  const headers = new Headers(response.headers);
  headers.set(SAVED_AT, String(Date.now()));
  const copy = new Response(await response.blob(), { status: 200, statusText: response.statusText, headers });

  // Deleting first moves the page to the end of the cache's order, which is oldest first.
  await cache.delete(key);
  await cache.put(key, copy);
  await trim(cache, MAX_SAVED_PAGES);
}

/** A response of the site's own that the site allows to be stored. */
function storable(response) {
  return response.ok
    && response.type === 'basic'
    && !response.redirected
    && !/no-store/i.test(response.headers.get('Cache-Control') || '');
}

/** A page the site rendered for this app, labelled with its content version. */
function storablePage(response) {
  return storable(response)
    && (response.headers.get('Content-Type') || '').includes('text/html')
    && response.headers.has(CONTENT_VERSION);
}

/** True when the page the site would show now is not the saved copy's. */
function changed(saved, response) {
  return !storablePage(response) || saved.headers.get(CONTENT_VERSION) !== response.headers.get(CONTENT_VERSION);
}

function savedRecently(saved) {
  return Date.now() - Number(saved.headers.get(SAVED_AT) || 0) < JUST_SAVED_MS;
}

async function trim(cache, max) {
  const keys = await cache.keys();
  await Promise.all(keys.slice(0, Math.max(0, keys.length - max)).map((request) => cache.delete(request)));
}

/** The URL a page is saved under, without the app's own prismMobile flag; null for pages never saved. */
function savedKey(url) {
  const parsed = new URL(url);
  if (parsed.origin !== self.location.origin) return null;
  if (NEVER_SAVED.some((pattern) => pattern.test(parsed.pathname))) return null;

  parsed.searchParams.delete('prismMobile');
  if ([...parsed.searchParams.keys()].length > 0) return null;

  parsed.search = '';
  parsed.hash = '';
  return parsed.href;
}

async function isTabPage(key) {
  const path = normalisePath(new URL(key).pathname);
  return (await tabs()).includes(path);
}

/** The tabs' first pages, from the app's manifest. */
async function tabs() {
  if (tabPaths) return tabPaths;
  try {
    const response = await fetch(MANIFEST_PATH, { credentials: 'same-origin' });
    const manifest = await response.json();
    tabPaths = (manifest.tabs || []).map((tab) => normalisePath(tab.path || '/'));
  } catch {
    // Without the manifest (no connection), every page is treated alike: network first.
    return [];
  }
  return tabPaths;
}

function normalisePath(path) {
  const trimmed = path.replace(/\/+$/, '');
  return (trimmed || '/').toLowerCase();
}

/** Deletes every saved page when the member signs in or out, so no copy outlives the session it was saved in. */
async function noteSession(signedIn) {
  const state = await caches.open(STATE_CACHE);
  const saved = await state.match(STATE_KEY);
  const previous = saved ? (await saved.json()).signedIn : null;
  if (previous === signedIn) return;

  await caches.delete(CACHE);
  await caches.delete(ASSET_CACHE);
  tabPaths = null;
  await state.put(STATE_KEY, new Response(JSON.stringify({ signedIn }), { headers: { 'Content-Type': 'application/json' } }));
}

/** The page request with the app's user agent passed on, once a page has said what it is. */
async function asApp(navigation) {
  await readingAppUserAgent;
  if (!appUserAgent) return navigation;

  const headers = new Headers(navigation.headers);
  headers.set(APP_AGENT_HEADER, appUserAgent);
  return new Request(navigation.url, {
    headers,
    credentials: 'include',
    redirect: 'manual',
    referrer: navigation.referrer,
    cache: navigation.cache
  });
}

async function noteAppUserAgent(userAgent) {
  if (typeof userAgent !== 'string' || userAgent === appUserAgent) return;
  appUserAgent = userAgent;
  await (await caches.open(STATE_CACHE)).put(APP_AGENT_KEY, new Response(userAgent));
}

async function tellPage(clientId, type) {
  if (!clientId) return;
  const client = await self.clients.get(clientId);
  if (client) {
    client.postMessage({ type });
    return;
  }
  pendingNotices.set(clientId, [...(pendingNotices.get(clientId) || []), type]);
}

function sendPending(client) {
  if (!client) return;
  for (const type of pendingNotices.get(client.id) || []) {
    client.postMessage({ type });
  }
  pendingNotices.delete(client.id);
}
