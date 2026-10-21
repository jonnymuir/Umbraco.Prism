## Native shell

The app's own chrome is native code in `native/`, which the bootstrap scripts copy into the
generated projects. Each tab is its own Capacitor bridge and webview showing a page of your site;
the tabs come from your site's `/umbraco/prism/mobile/manifest`.

| What | iOS (`native/ios/`) | Android (`native/android/java/`) |
|---|---|---|
| Site address and shared logger | `PrismShell.swift` | `PrismShell.java` |
| Manifest fetch and offline copy | `PrismManifest.swift` | `PrismManifest.java` |
| Tab bar, session changes, notification routing | `PrismTabBarController.swift` | `MainActivity.java` |
| One tab's bridge and webview | `PrismWebTabViewController.swift`, `PrismBridgeViewController.swift` | `PrismTabFragment.java` |
| The site's page and brand colours, remembered between launches | `PrismPageColours.swift` | `PrismPageColours.java` |
| Holding the outgoing page while the next loads, and the loading ring | `PrismNavigationHold.swift`, `PrismLoadingRing.swift` | `PrismLoadingRing.java` (Android's webview holds the page itself) |
| Tab icons, including SVG | `PrismTabIconLoader.swift`, `PrismSvgIcon.swift`, `PrismSvgPath.swift` | `PrismTabIcons.java`, `PrismSvgIcon.java`, `PrismSvgPath.java`, `PrismSvgArc.java` |
| System sign-in sheet | `PrismSystemSignInPlugin.swift`, `App.entitlements` | `PrismSystemSignInPlugin.java` |
| Identity provider cookie cleanup on sign-out | `PrismIdentityCookiePlugin.swift` | `PrismIdentityCookiePlugin.java` |

### Diagnosing a live app

The shell logs its manifest fetches, sign-in and sign-out, and the system sign-in sheet's result,
all under the name `PrismShell`:

- **iOS:** in Xcode's console or Console.app, filter on subsystem `{{appId}}` and category
  `PrismShell`. Most messages are at the info level, which Console.app shows once
  **Action → Include Info Messages** is on.
- **Android:** `adb logcat -s PrismShell`.

If the sign-in sheet closes straight away, the log says why. The usual cause is that the site's
`/.well-known/apple-app-site-association` or `/.well-known/assetlinks.json` does not name this app:
check the tenant's **App Sign-In** settings, and that Google Play's app signing key fingerprint is
among the Android fingerprints.


### Saved pages

An app built with saved pages keeps copies of the site's pages in a service worker that the site
serves from `/prism-offline-sw.js`; no native code is involved. On iOS the build also lists the
app's own domains under `WKAppBoundDomains` in `Info.plist` and sets
`limitsNavigationsToAppBoundDomains`, since iOS runs a service worker only for those. A page on any
other domain cannot load in the app's webviews; if sign-in stops at a blank page, the identity
provider's host is missing from that list.

To inspect the saved pages, open the webview in Safari's **Develop** menu (iOS) or
`chrome://inspect` (Android) and look under **Storage → Cache Storage**, `prism-saved-pages-v1`.
