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
 Tab webview          Native shell                 System auth session          Prism
     │ tap "Sign in"       │                              │                          │
     │──/auth/login?──────▶│ cancel navigation            │                          │
     │   returnUrl=/x      │ verifier v, challenge c=S256(v)                         │
     │                     │──open /umbraco/prism/mobile/auth/start?c&returnUrl────▶│
     │                     │                              │◀── OIDC challenge ───────│
     │                     │                              │◀─▶ identity provider     │
     │                     │                              │── /signin-oidc ─────────▶│ member cookie
     │                     │                              │── .../auth/complete ────▶│ one-time code k
     │                     │◀── <appId>://signed-in?code=k ───────────────────────────│
     │◀─ POST .../auth/redeem {code k, verifier v} ───────────────────────────────────▶│ checks S256(v)=c
     │◀─ Set-Cookie PrismMemberCookie, 303 /x ──────────────────────────────────────────│
     │                     │ cookie observer refreshes the other tabs                │
```

1. **Interception.** The shell cancels any webview navigation to its own site's `/auth/login`
   or `/auth/register` and starts the system flow with the same `returnUrl` (and
   `prompt=create` for register). A challenge raised anywhere else, such as an `[Authorize]`
   page, answers a native-tabs request with a redirect to `/auth/login?returnUrl=<page>`
   instead of the identity provider, so it is intercepted the same way. Identity provider hosts
   leave the iOS `allowNavigation` list.
2. **Start.** `GET /umbraco/prism/mobile/auth/start?app=&challenge=&returnUrl=&prompt=`
   validates the app ID (a reverse-domain identifier, and equal to the tenant's saved mobile
   app ID when it has one), the challenge (43 base64url characters) and the return URL (local
   only), then issues the normal OIDC challenge with
   `/umbraco/prism/mobile/auth/complete?app=&challenge=&returnUrl=` as its redirect. Not found
   where the tenant has no sign-in.
3. **Complete.** Runs in the system session, just after `/signin-oidc` has signed in the member
   cookie there. It reads that ticket, stores it with the challenge, tenant and return URL under
   a fresh one-time code, and redirects to `<appId>://signed-in?code=<code>`. The callback
   scheme is always a reverse-domain app ID, never `http`, `https` or anything a browser would
   load.
4. **Redeem.** The shell loads `POST /umbraco/prism/mobile/auth/redeem` (form body: code,
   verifier) in the tab that started sign-in. Prism removes the code (single use), checks the
   tenant and that `S256(verifier)` equals the stored challenge, signs in `PrismMemberCookie`
   with the stored ticket plus a `prism_auth_method=system` claim, and redirects to the return
   URL. The cookie lands in the shared webview store, and the existing cookie observer refreshes
   the other tabs.
5. **Cancel.** Closing the sheet leaves the tab where it was.

**Storage.** One-time codes live in `IDistributedCache` for two minutes, the ticket protected
with ASP.NET data protection. A single-instance host gets the in-memory cache; a multi-instance
host registers a shared one, as it already must for data protection keys.

**Sign-out.** The system session is ephemeral (decision 2), so no identity provider session
outlives sign-in. `/auth/logout` treats a `prism_auth_method=system` session like a biometric
one: it signs out the member cookie only. The webview's identity provider cookie cleanup in
`prism-biometric-signout.js` has nothing left to clear for these sessions.

## 4) Biometric sign-in

When sign-in is intercepted and the device holds a biometric token, the shell asks for Face ID
or Touch ID first and loads `POST /umbraco/prism/mobile/biometric/exchange` in the tab, so the
cookie lands in the webview store. A failed or cancelled check falls through to the system
flow; a rejected token is cleared, as today. `prism-biometric-autologin.js` stops loading for
native-tabs requests, which also ends the prompt that fired on every signed-out page view.
Enrollment stays the web banner shown after sign-in.

## 5) Decisions

1. **Identity provider tokens stay on the server.** R4 in mobile.md says tokens live in the
   Keychain. Holding the identity provider's own tokens on the device would make every tenant
   register a second, public native client and redirect URI with its provider. Instead the
   server keeps them (in the member ticket, and encrypted for biometric sign-in) and the device
   only ever holds Prism-issued credentials: the biometric token now, and a Prism app token
   for Delivery API content in phase 3. R4 is reworded to match.
2. **The system session is ephemeral** (`prefersEphemeralWebBrowserSession`). There is no
   "wants to use … to sign in" alert and no identity provider session left behind, so sign-out
   is complete and a different member can sign in next. The cost is no single sign-on with
   Safari: the member enters their credentials each time, with password autofill and passkeys
   still available, and biometric sign-in after the first time.

## 6) Security

- The code is 256 bits, single use, two-minute lifetime, bound to one tenant.
- The verifier never leaves the app until redeem and travels in a POST body, so a code taken
  from the callback URL is useless on its own. This is PKCE between the app and Prism.
- A flow started by someone else (a link opened in Safari) ends at a custom scheme URL only an
  app on the member's own device can receive, after the member has signed in at their own
  provider, which is always asked to show its account chooser. This is the same exposure as
  any OAuth native client with a custom scheme: iOS does not reserve a scheme for one app, so
  checking it against the saved app ID adds little. Universal Links callbacks (iOS 17.4 and
  later, with the site serving `apple-app-site-association`) would close it, and can follow
  once Prism stores each tenant's team and app ID.
- `start`, `complete` and `redeem` are anonymous endpoints and join `KnownAnonymous` in the
  authorization contract tests.

## 7) Platforms

- **iOS:** `ASWebAuthenticationSession` in the native shell, this work.
- **Android:** Custom Tabs and an intent filter for the same callback, with the same server
  endpoints, delivered with Android native tabs. Until then Android keeps signing in inside the
  webview: the challenge redirect in step 1 only applies to native-tabs requests.

## 8) Verification

- Controller tests: start refusals, complete issuing a code, redeem rejecting a reused code, a
  wrong verifier, another tenant's code and an expired code.
- Simulator against the local stack: the ASP.NET development certificate is added to the
  simulator's keychain so the system session trusts `https://localhost`. A Maestro flow signs in
  through the system sheet from a non-home tab, lands on the return page, and finds the other
  tabs signed in; signing out and back in shows the provider's sign-in form again.
- Biometric: enrol, sign out, sign in with simulated Face ID, and with a failed match falling
  through to the system sheet.
- TestFlight build against prismreference.com, Entra sign-in checked on a device.
