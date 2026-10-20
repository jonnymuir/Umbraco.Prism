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
 Webview              Native shell                 System auth session          Prism
     │ tap "Sign in"       │                              │                          │
     │──/auth/login?──────▶│ cancel navigation            │                          │
     │   returnUrl=/x      │ verifier v, challenge c=S256(v)                         │
     │                     │──open /umbraco/prism/mobile/auth/start?c&returnUrl────▶│
     │                     │                              │◀── OIDC challenge ───────│
     │                     │                              │◀─▶ identity provider     │
     │                     │                              │── /signin-oidc ─────────▶│ member cookie
     │                     │                              │── .../auth/complete ────▶│ one-time code k
     │                     │◀── https://<site>/umbraco/prism/mobile/auth/callback?code=k (verified link)
     │◀─ POST .../auth/redeem {code k, verifier v} ───────────────────────────────────▶│ checks S256(v)=c
     │◀─ Set-Cookie PrismMemberCookie, 303 /x ──────────────────────────────────────────│
     │                     │ other webviews pick up the cookie and refresh           │
```

1. **Interception.** The shell cancels any webview navigation to its own site's `/auth/login`
   or `/auth/register` and starts the system flow with the same `returnUrl` (and
   `prompt=create` for register). Generated apps add `PrismSystemSignIn` to their user agent
   on both platforms; a challenge raised anywhere else, such as an `[Authorize]` page, answers
   such a request with a redirect to `/auth/login?returnUrl=<page>` instead of the identity
   provider, so it is intercepted the same way. Identity provider hosts leave the app's
   `allowNavigation` list.
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
5. **Redeem.** The shell loads `POST /umbraco/prism/mobile/auth/redeem` (form body: code,
   verifier) in the webview that started sign-in. Prism removes the code (single use), checks
   the tenant and that `S256(verifier)` equals the stored challenge, signs in
   `PrismMemberCookie` with the stored ticket plus a `prism_auth_method=system` claim, and
   redirects to the return URL. The cookie lands in the app's shared webview cookie store, and
   on iOS the existing cookie observer refreshes the other tabs.
6. **Cancel.** Closing the sheet leaves the page where it was.

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

When sign-in is intercepted and the device holds a biometric token, the shell asks for Face ID,
Touch ID or the Android fingerprint prompt first, and loads
`POST /umbraco/prism/mobile/biometric/exchange` in the webview, so the cookie lands in the
webview store. A failed or cancelled check falls through to the system flow; a rejected token
is cleared, as today. `prism-biometric-autologin.js` stops loading for `PrismSystemSignIn`
requests, which also ends the prompt that fired on every signed-out page view. Enrollment
stays the web banner shown after sign-in.

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

Both platforms ship together, on the same server endpoints.

- **iOS:** the native-tabs shell intercepts navigation in each tab's webview.
- **Android:** the single-webview shell intercepts navigation in a Capacitor plugin that wraps
  the bridge's `WebViewClient`, and receives the Auth Tab result or the App Link in
  `MainActivity`. Native tabs on Android follow later and reuse it.

## 9) Verification

- Controller tests: start refusals, complete issuing a code, redeem rejecting a reused code, a
  wrong verifier, another tenant's code and an expired code; both `.well-known` files for a
  configured tenant, and not found for an unconfigured one.
- Platform link checks need a public HTTPS host, so the local stack is reached through a
  temporary HTTPS tunnel, with a local tenant for the tunnel's hostname.
- iOS simulator and Android emulator: a Maestro flow signs in through the system sheet from a
  page other than home, lands on the return page signed in, signs out, and signing back in shows
  the provider's sign-in form again. On iOS, the other tabs are signed in too.
- Biometric: enrol, sign out, sign in with simulated Face ID or fingerprint, and a failed match
  falling through to the system sheet.
- TestFlight and Play internal builds against prismreference.com, with Entra sign-in checked on
  devices.
