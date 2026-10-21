# Prism Mobile: Saved Pages (Phase 3a)

Detailed design for the first step of phase 3 in [mobile.md](mobile.md): rule R3, "every screen
has a cached state", for the pages the server already renders. Native rendering from the Delivery
API (D4) follows only if this falls short.

## 1) Goals

- A tab's first page appears at once on launch, from a saved copy, and refreshes in the background.
- With no connection, a page the member has seen shows its saved copy, marked as saved, instead of
  the offline screen.
- Nothing is saved that the site says must not be stored, and one member's saved pages never
  show to the next.
- One implementation for iOS and Android, and editors change nothing.

## 2) Today

Every page load goes to the network. Versioned CSS and JS are cached for a year (D3), so a repeat
visit fetches only the HTML, but with no connection any page that is not already open in a tab
shows the offline screen. Signed-in pages are sent `Cache-Control: no-store` (the reference site's
`NoCacheAuthenticated`), so nothing stores them, deliberately.

## 3) Mechanism: a service worker

A service worker, `prism-offline-sw.js`, saves pages and serves them back. It is the web's own
offline mechanism, so the same file works in both apps.

- **Android:** the webview supports service workers as Chrome does.
- **iOS:** WKWebView runs a service worker only for "App-Bound Domains": the domains listed under
  `WKAppBoundDomains` in the app's Info.plist (at most 10, no wildcards), with
  `limitsNavigationsToAppBoundDomains` set. The generated app lists the tenant's own host. With
  App Sign-In the identity provider's pages never load in the app, so that is the only domain it
  needs. A tenant that signs in inside the app also lists its identity provider's hosts
  (`PrismIdentityProviderHosts`), which keeps them within the limit. Navigation to any other
  domain already leaves the app for the browser.
- **Served from the site root,** at `/prism-offline-sw.js`, so its scope covers the whole site. It
  is registered only on pages rendered for the app (`PrismNativeTabs` in the user agent) when the
  tenant has saved pages on (section 7). Websites in a browser are unaffected.

## 4) What is saved

Only page navigations (`GET`, `text/html`, the site's own origin) that answer 200 and are allowed
to be stored:

- **Never** a response with `Cache-Control: no-store`, which today covers every signed-in page.
- **Never** `/auth/*`, `/umbraco/*`, the sign-in callback, or a page with a query string other than
  `prismMobile`.
- **Never** a POST, and so never a Wayfinder journey stage reached by submitting a form. A journey's
  first page (a `GET`) can be saved like any page; its later stages cannot.

Saved copies are kept per URL, at most 50, the least recently used dropped first. CSS, JS and
images are not saved by the worker: the HTTP cache already keeps the versioned ones.

## 5) How a page is served

- **A tab's first page:** served from the saved copy at once, and fetched in the background.
- **Every other navigation:** from the network, falling back to the saved copy when the network
  fails or has not answered within 3 seconds.
- **No connection and no saved copy:** the branded offline screen, as today.

A fresh copy replaces what is on screen only at moments the member is not in the middle of
something: on launch, on coming back from the background, and on switching to the tab, and then
only until they first scroll, type or tap. On iOS the navigation hold covers the swap, so it looks
like the page settling rather than reloading. A fresh copy that arrives after the member has started
reading is kept for the next of those moments; it never replaces the page under them, never moves
their scroll position, and never takes a screen reader's focus away. The cost is that a page can
stay one visit out of date, which suits tab first pages: they change least, and publishing
refreshes them (section 6).

The app tells the member nothing about copies, versions or refreshing. Those are the app's business,
and nothing a member could do with them. What the member can act on is having no connection, since
anything that needs the site will not work until it comes back. So while the app has no connection,
a page shows one line at the top, "You're offline. Some information may be out of date.", announced
once to screen readers as a status message. It goes as soon as the connection returns, and the page
quietly refreshes at the next of the moments above.

## 6) Keeping saved copies fresh and separate

- **Publishing.** The manifest gains `contentVersion`, which Prism bumps whenever content is
  published. The app reads the manifest on launch and when it comes back to the foreground; when
  the version has moved, the worker refreshes every saved page in the background.
- **Sign-in and sign-out.** All saved pages are deleted when the member signs in or out: by the
  sign-in page and the sign-out script, and by a `Clear-Site-Data: "cache"` header on the
  responses that change the session. A page saved before sign-in can never be served after it, or
  the reverse.
- **Shell updates.** The worker's own cache is versioned; a new worker version starts with an empty
  cache.

## 7) Configuration

A per-tenant **Saved pages** switch in the tenant's mobile settings, off by default, and
`Prism:SeedTenant:MobileApp:SavedPages` for a seeded tenant. The manifest reports it, so the app
registers the worker only when it is on and unregisters it (deleting its cache) when it is turned
off.

## 8) Decisions

1. **Signed-in pages stay live by default.** The worker saves only what the site allows to be
   stored, and the reference site forbids storing any signed-in page. So the dashboard and other
   member pages need a connection. A host that wants them available offline sends
   `Cache-Control: private` instead of `no-store` for app requests; the worker then saves them,
   on the device only, deleted on sign-in and sign-out. The reference site keeps `no-store`.
2. **A service worker rather than a cache in each native shell.** One implementation, standard and
   inspectable in browser devtools, and reusable later if the site becomes installable as a web
   app. The cost is App-Bound Domains on iOS, which the system sign-in already makes cheap.
3. **No status about saved copies.** Members see only whether they are offline, in plain words;
   fresh copies swap in silently at quiet moments rather than through a refresh prompt.
4. **Saved copy first only for tab first pages.** Showing an old copy of every page first would
   make most navigations show stale content briefly; for tab first pages, which are the most
   visited and change least, it is worth it.

## 9) Platforms

Both platforms use the same worker and the same page script (`prism-offline.js`, which shows the
offline line and swaps in fresh copies). iOS adds the App-Bound Domains to the generated Info.plist and
sets `limitsNavigationsToAppBoundDomains` in the Capacitor config. Android needs nothing native.

## 10) Verification

- First, a spike on the iOS simulator: App-Bound Domains with Capacitor, the tab shell, the
  navigation hold and the page colour script all still working, and the worker registering.
- Worker tests (Playwright against the local stack with an app user agent): a public page is saved
  and served offline; a `no-store` page and a POST response are never saved; sign-in and sign-out
  delete everything; `contentVersion` moving refreshes saved pages.
- Both apps, local stack: launch with no connection shows the saved tab first pages with the
  offline line, which VoiceOver and TalkBack read once; a fresh copy never replaces a page after a
  scroll or tap; a page never
  visited shows the offline screen; signing out and in again shows no page saved before.
- Launch time to first content, with and without saved pages, on the iOS simulator and the Android
  emulator.
