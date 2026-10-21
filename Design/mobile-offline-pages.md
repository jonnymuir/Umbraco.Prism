# Prism Mobile: Saved Pages (Phase 3a)

Detailed design for the first step of phase 3 in [mobile.md](mobile.md): rule R3, "every screen
has a cached state", for the pages the server already renders. Native rendering from the Delivery
API (D4) follows only if this falls short.

## 1) Goals

- A tab's first page appears at once on launch, from a saved copy, and refreshes in the background.
- With no connection, a page the member has seen shows its saved copy, with a plain line saying the
  app is offline, instead of the offline screen.
- Nothing is saved that the site says must not be stored, and one member's saved pages never
  show to the next.
- One implementation for iOS and Android, and editors change nothing.

## 2) Without saved pages

Every page load goes to the network. Versioned CSS and JS are cached for a year (D3), so a repeat
visit fetches only the HTML, but with no connection any page that is not already open in a tab
shows the offline screen. Signed-in pages are sent `Cache-Control: no-store` (the reference site's
`NoCacheAuthenticated`), so nothing stores them, deliberately.

## 3) Mechanism: a service worker

A service worker, `prism-offline-sw.js`, saves pages and serves them back. It is the web's own
offline mechanism, so the same file works in both apps.

- **Served from the site root,** at `/prism-offline-sw.js` (`PrismSavedPagesController`, from an
  embedded copy), so its scope covers the whole site.
- **Registered by `prism-offline.js`,** which a host includes on every page rendered for a
  native-tab app, with `data-prism-saved-pages` (from `PrismMobileRequestDetection.SavesPages`) and
  `data-prism-signed-in`. In an app built without saved pages the same script removes any worker
  and saved pages an earlier build left behind. Websites in a browser are unaffected.
- **iOS:** WKWebView runs a service worker only for "App-Bound Domains": the domains listed under
  `WKAppBoundDomains` in the app's Info.plist (at most 10, no wildcards), with
  `limitsNavigationsToAppBoundDomains` set. The generated app lists the site's host and the
  identity provider's sign-in hosts (`IosProjectFiles.AppBoundDomains`). With App Sign-In the
  identity provider's pages never load in the app; a tenant that signs in inside the app on Entra
  must use an authority whose host is in that list, since its wildcard hosts cannot be listed.
- **Android:** the webview runs service workers as Chrome does, but sends a worker's requests with
  the webview's default user agent, without what the app appends. So each page tells the worker the
  app's user agent, and the worker passes it on in `X-Prism-User-Agent`, which
  `PrismMobileRequestDetection` reads alongside `User-Agent`. Until it has heard it, the worker
  leaves page loads to the webview.

## 4) What is saved

Only page navigations (`GET`, `text/html`, the site's own origin) that answer 200 and are allowed
to be stored:

- **Never** a response with `Cache-Control: no-store`, which today covers every signed-in page.
- **Never** `/auth/*`, `/umbraco/*`, the sign-in callback, or a page with a query string other than
  `prismMobile`.
- **Never** a POST. Wayfinder journey pages carry forms with antiforgery tokens, which ASP.NET Core
  sends `no-store`, so no journey page is saved.
- **Never** a page without the `Prism-Content-Version` label (section 6), so only pages the site
  rendered for this app.

Saved copies are kept per URL, at most 50, the oldest saved dropped first. The site's own
stylesheets, scripts, fonts and images are saved too, at most 300, since a saved page is no use
offline without them and the webview's own cache does not reliably keep them. Versioned ones (with
a `?v=` hash) never change, so they are served from the saved copy first; the rest come from the
network, falling back to the saved copy.

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
sets that refresh going (section 6).

The app tells the member nothing about copies, versions or refreshing. Those are the app's business,
and nothing a member could do with them. What the member can act on is having no connection, since
anything that needs the site will not work until it comes back. So while the app has no connection,
a page shows one line at the bottom, just above the tab bar and clear of the site's own header,
"You're offline. Some information may be out of date.", announced once to screen readers as a
status message. It goes as soon as the connection returns, and the page
quietly refreshes at the next of the moments above.

## 6) Keeping saved copies fresh and separate

- **Publishing.** Every page rendered for an app with saved pages carries a
  `Prism-Content-Version` header: a version that moves on every content publish or unpublish and on
  every start of the site (a deploy can change templates), plus whether a member was signed in.
  When a fresh copy's label differs from the saved copy's, or the site no longer allows the page to
  be stored, the worker tells the page, which swaps the fresh copy in at the next quiet moment. A
  load-balanced site keeps a version per instance; the worst that does is swap in a copy that looks
  the same.
- **Sign-in and sign-out.** Each page says hello to the worker with whether the member is signed
  in. When that changes, every saved page and asset is deleted, so a copy saved before sign-in can
  never be served after it, or the reverse.
- **Worker updates.** The worker's caches are versioned; a new version deletes the old ones.

## 7) Configuration

**Saved pages** is a bundle option, off by default, like push notifications: a switch in the
tenant's mobile settings in the backoffice, and `--saved-pages true` for the bundle CLI. It is a
build option rather than a site setting because the iOS App-Bound Domains are fixed when the app is
built. An app built with it adds `PrismSavedPages` to its user agent, which is how the site knows to
register the worker; an app built without it never has one.

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

Both platforms use the same worker and the same page script. iOS adds the App-Bound Domains to the
generated Info.plist (`ios-saved-pages.sh`) and sets `limitsNavigationsToAppBoundDomains` in the
Capacitor config. Android needs nothing native; the user agent hand-over (section 3) is in the
worker.

## 10) Verification

- Bundle and site tests (`PrismSavedPagesTests`): the user agent token, the iOS App-Bound Domains
  and navigation limit, the user agent hand-over header, the content version label and the worker
  endpoint.
- Worker tests against the local stack (`localhost-saved-pages.spec.ts`, an app user agent): a page
  a visitor has seen opens offline with the offline line; a page the site says must not be stored
  is never saved; signing in deletes every saved page and signed-in pages are never saved.
- Both apps, local stack: launch with the site unreachable shows the saved Home page, styled, with
  the offline line; a page never saved shows the offline screen; signed in, nothing saved before
  sign-in is shown. A new content version swaps the fresh copy in before the member touches the
  page.
