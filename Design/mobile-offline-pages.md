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

- **A tab's first page, and any saved page on launch:** served from the saved copy at once, then
  fetched in the background. If the fresh page differs and the member has not touched the page yet,
  it quietly replaces the saved one on screen; once they have, a small "Updated, tap to refresh"
  bar offers it instead, so nothing moves under their thumb.
- **Every other navigation:** from the network, falling back to the saved copy when the network
  fails or has not answered within 3 seconds.
- **No connection and no saved copy:** the branded offline screen, as today.

A page served from a saved copy says so: a small bar at the top, "Saved copy from 10:42", with the
same wording on both platforms. It disappears when a fresh copy replaces the page.

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
3. **Saved copy first only for tab first pages.** Showing an old copy of every page first would
   make most navigations show stale content briefly; for tab first pages, which are the most
   visited and change least, it is worth it.

## 9) Platforms

Both platforms use the same worker and the same page script (`prism-offline.js`, which shows the
saved copy bar and the update bar). iOS adds the App-Bound Domains to the generated Info.plist and
sets `limitsNavigationsToAppBoundDomains` in the Capacitor config. Android needs nothing native.

## 10) Verification

- First, a spike on the iOS simulator: App-Bound Domains with Capacitor, the tab shell, the
  navigation hold and the page colour script all still working, and the worker registering.
- Worker tests (Playwright against the local stack with an app user agent): a public page is saved
  and served offline; a `no-store` page and a POST response are never saved; sign-in and sign-out
  delete everything; `contentVersion` moving refreshes saved pages.
- Both apps, local stack: launch with no connection shows the saved tab first pages; a page never
  visited shows the offline screen; signing out and in again shows no page saved before.
- Launch time to first content, with and without saved pages, on the iOS simulator and the Android
  emulator.
