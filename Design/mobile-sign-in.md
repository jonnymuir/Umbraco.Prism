# Prism Mobile: System Sign-In (R4)

Detailed design for rule R4 of [mobile.md](mobile.md): interactive sign-in happens in the system
authentication session, never inside the app's webviews.

## 1) Goals

- Hosted identity provider pages never render in a tab's webview.
- Sign-in from any tab lands the member back on the page they were going to, with every tab
  signed in.
- No change to how a tenant registers with its identity provider: the same confidential web
  client and `/signin-oidc` redirect URI work for the site and the app.
- Biometric sign-in becomes a native step of the same flow, not a script on every page.

## 2) Today

Sign-in is a server-side OIDC code flow. `/auth/login` challenges `PrismEntraID`, the identity
provider redirects to `/signin-oidc`, Prism redeems the code with the tenant's client secret and
signs in `PrismMemberCookie`, whose ticket holds the access and refresh tokens. In the app all
of this runs inside the tab's webview. Biometric sign-in stores a Prism-issued biometric token
on the device; the server keeps the refresh token, encrypted, and exchanges one for the other.

## 3) Flow

```
 Webview (sign-in page)     Plugin             System auth session          Prism
     │ /auth/login?returnUrl=/x ────────────────────────────────────────────────▶│ sign-in page
     │ verifier v, challenge c=S256(v)                                          │
     │── authenticate(start?c) ▶│── open /umbraco/prism/mobile/auth/start?c ───▶│
     │                         │                    │◀── OIDC challenge ───────│
     │                         │                    │◀─▶ identity provider     │
     │                         │                    │── /signin-oidc ─────────▶│ member cookie
     │                         │                    │── .../auth/complete ────▶│ one-time code k
     │                         │◀── https://<site>/umbraco/prism/mobile/auth/callback?code=k
     │◀── callback URL ────────│   (handed only to the verified app)
     │── fetch POST .../auth/redeem {k, v} ─────────────────────────────────────▶│ checks S256(v)=c
     │◀── Set-Cookie PrismMemberCookie, redirect /x ─────────────────────────────│
     │ location.replace(/x); the other tabs pick up the session
```

1. **Sign-in page.** Generated apps add `PrismSystemSignIn` to their user agent. For such a
   request, on a platform the tenant has set up (section 4), `/auth/login` and `/auth/register`
   serve a small sign-in page instead of challenging the provider, and a challenge raised by any
   other page, such as an `[Authorize]` page, redirects to `/auth/login?returnUrl=<page>`. The
   page's script (`prism-system-sign-in.js`) tries biometric sign-in first (section 5), then
   creates the PKCE verifier and asks the app's `PrismSystemSignIn` plugin to open the system
   sheet at `start`. The plugin is the only native code: it opens the sheet and returns the
   callback URL. Identity provider hosts never load in a webview.
2. **Start.** `GET /umbraco/prism/mobile/auth/start?challenge=&returnUrl=&prompt=` validates the
   challenge (43 base64url characters) and the return URL (local only), then issues the normal
   OIDC challenge with `/umbraco/prism/mobile/auth/complete?challenge=&returnUrl=` as its
   redirect. Not found where the tenant has no sign-in or no app links (section 4).
3. **Complete.** Runs in the system session, just after `/signin-oidc` has signed in the member
   cookie there. It reads that ticket, stores it with the challenge, tenant and return URL under
   a fresh one-time code, and redirects to `https://<site>/umbraco/prism/mobile/auth/callback?code=<code>`.
4. **Callback.** The system session hands that URL straight to the app, because the app is
   verified for the site's domain (section 4). The page itself is only reached if the link was
   opened somewhere else: it ignores the code and says to return to the app, and is served
   `no-store`, `noindex` and `Referrer-Policy: no-referrer`.
5. **Redeem.** The sign-in page posts the code and verifier to `/umbraco/prism/mobile/auth/redeem`
   with `fetch`. Prism removes the code (single use), checks
   the tenant and that `S256(verifier)` equals the stored challenge, signs in
   `PrismMemberCookie` with the stored ticket plus a `prism_auth_method=system` claim, and
   redirects to the return URL, which the page then replaces itself with, so Back never returns
   to it. The cookie lands in the app's shared webview cookie store, and the shell's session
   check refreshes the other tabs.
6. **Cancel.** Closing the sheet takes the member back to the page they came from.

**Storage.** One-time codes live in `IDistributedCache` for two minutes, the ticket protected
with ASP.NET data protection. A single-instance host gets the in-memory cache; a multi-instance
host registers a shared one, as it already must for data protection keys.

**Sign-out.** The system session is ephemeral (decision 2), so no identity provider session
outlives sign-in. `/auth/logout` treats a `prism_auth_method=system` session like a biometric
one: it signs out the member cookie only. The webview's identity provider cookie cleanup in
`prism-biometric-signout.js` has nothing left to clear for these sessions.

## 4) App links

Only the tenant's own app can receive the callback, because each platform checks a file the
site serves before handing an `https` URL to an app.

- **iOS:** `GET /.well-known/apple-app-site-association` returns
  `{"webcredentials":{"apps":["<teamId>.<appId>"]}}`. The generated app carries the
  `webcredentials:<site host>` associated domain and opens the session with
  `ASWebAuthenticationSession.Callback.https(host:path:)`, which needs iOS 17.4. Generated apps
  target iOS 17.4 or later.
- **Android:** `GET /.well-known/assetlinks.json` returns a `delegate_permission/common.handle_all_urls`
  statement for the app's package and signing certificate fingerprints. The app opens an Auth
  Tab (`androidx.browser` `AuthTabIntent`) with the callback host and path, and Chrome checks
  the statement before returning the URL. Where the browser has no Auth Tab, it falls back to a
  Custom Tab and the callback arrives through a verified App Link intent filter
  (`android:autoVerify="true"`) on the same path.

The tenant's mobile app settings in the backoffice gain the Apple team ID, the Android package
name (defaulting to the app ID) and the Android signing certificate SHA-256 fingerprints: the
upload key's and, for Play App Signing, Google's. Each file is not found until its platform's
fields are set, and so is `start`. Generated bundles take the same values, so the app and the
site always agree.

## 5) Biometric sign-in

The sign-in page loads `prism-biometric-autologin.js` on demand and calls
`window.PrismBiometricSignIn()` before opening the sheet: Face ID, Touch ID or the Android
fingerprint prompt, then the existing token exchange. A failed or cancelled check falls through
to the sheet; a rejected token is cleared, as today. Layouts leave the auto-running script out for
such apps, which ends the prompt that fired on every signed-out page view in every tab.
Enrollment stays the web banner shown after sign-in.

## 6) Decisions

1. **Identity provider tokens stay on the server.** Holding the identity provider's own tokens
   on the device would make every tenant register a second, public native client and redirect
   URI with its provider. Instead the server keeps them (in the member ticket, and encrypted for
   biometric sign-in) and the device only ever holds Prism-issued credentials: the biometric
   token now, and a Prism app token for Delivery API content in phase 3.
2. **The system session is ephemeral** (`prefersEphemeralWebBrowserSession` on iOS, an
   ephemeral Auth Tab on Android where the browser supports one). There is no "wants to use …
   to sign in" alert and no identity provider session left behind, so sign-out is complete and
   a different member can sign in next. The cost is no single sign-on with the system browser:
   the member enters their credentials each time, with password autofill and passkeys still
   available, and biometric sign-in after the first time.
3. **No custom URL scheme.** Neither platform reserves a custom scheme for one app, so a code
   sent to one could be caught by another app claiming the same scheme. The callback is an
   `https` URL on the tenant's own domain, verified for its app, on both platforms.

## 7) Security

- The code is 256 bits, single use, two-minute lifetime, bound to one tenant.
- Only the tenant's verified app receives the callback, and the verifier never leaves that app
  until redeem, in a POST body. A code seen anywhere else, such as a callback link opened in a
  browser, cannot be redeemed. This is PKCE between the app and Prism.
- A flow started by someone else, such as a start link opened in Safari, delivers its code to
  the real app, which has no matching verifier and ignores it.
- `start`, `complete`, `callback` and `redeem` and the two `.well-known` files are anonymous and
  join `KnownAnonymous` in the authorization contract tests.

## 8) Platforms

Both platforms ship together, on the same server endpoints and the same sign-in page.

- **iOS:** `ASWebAuthenticationSession` with an `https` callback and the
  `webcredentials:<site host>` associated domain; apps target iOS 17.4.
- **Android:** `AuthTabIntent` with an `https` redirect (Chrome 137 and later), falling back to a
  Custom Tab where the browser has no Auth Tab, with the callback arriving as a verified App Link
  on `MainActivity`. Needs `androidx.browser` 1.9, and with it Android Gradle plugin 8.9.1.

## 9) Verification

- Controller tests: the sign-in page for apps the site vouches for, start refusals, complete
  issuing a code, redeem rejecting a reused code, a wrong verifier, another tenant's code; both
  `.well-known` files for a configured tenant, and not found for an unconfigured one.
- Generated bundle tests: the plugins, entitlements, App Link and user agent tokens.
- Android emulator against the local stack: the full flow from a page that needs a member,
  through the sheet and the provider, to the member's page with every tab signed in, Back
  skipping the sign-in page, and sign-out. The App Link is handed to the app with `adb`, since
  Android verifies only real domains.
- The domain association itself is checked on devices only: the iOS simulator cannot confirm an
  app's association (its builds carry no real signature) and neither platform verifies
  `localhost`. TestFlight and Play internal builds against prismreference.com cover it, with
  Entra sign-in.
- Tenants without app sign-in: the existing Maestro flows, on the iOS simulator and the Android
  emulator, still sign in inside the app.
