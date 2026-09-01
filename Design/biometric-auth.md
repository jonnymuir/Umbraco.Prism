# Prism Mobile: Biometric Login (Design)

## Product Goal

Allow returning mobile users to skip the full Entra OIDC flow and authenticate with their device biometric (Face ID / Touch ID on iOS; fingerprint / face unlock on Android). The full Entra flow remains the trusted root; biometric is a convenience shortcut that can be revoked by the user or a tenant admin.

---

## Constraints and Assumptions

- Auth root is always Entra OIDC. Biometric does not replace it; it accelerates repeat logins.
- The Capacitor WebView holds the active session. Biometric auth must result in a valid `PrismMemberCookie` being injected into that WebView.
- No Entra credentials (passwords or raw Entra tokens) are stored on the device. An opaque Prism-issued biometric token is stored instead.
- All device-side secret storage uses the platform Keychain (iOS) or Keystore (Android), gated behind biometric authentication.
- This is a mobile-only feature. It has no impact on the Umbraco backoffice or desktop web flows.

---

## Glossary

| Term | Meaning |
|---|---|
| `BiometricToken` | Prism-issued signed JWT, stored on device, exchanged for a session cookie |
| `PrismMemberCookie` | The existing encrypted ASP.NET auth cookie that establishes the WebView session |
| `PrismBiometricRecord` | Server-side DB row linking a BiometricToken to a tenant, user OID, and encrypted Entra refresh token |
| Entra OID | The user's immutable object ID from Entra (`oid` claim) |

---

## Registration Flow

### When to trigger

After the first successful Entra OIDC login in the mobile app, before the WebView navigates to the home page.

### Prerequisite check

1. The app native layer checks whether biometric hardware is available and enrolled (`BiometryType != none`).
2. If not available (no biometric enrolled, device too old), skip silently. No prompt.
3. If a `BiometricToken` for this tenant is already stored on device, skip silently (already enrolled).

### Step-by-step

```
User completes Entra OIDC → PrismMemberCookie is set in WebView
         |
         v
[Native Layer] Check: biometric available AND not already enrolled?
         |
        YES
         |
         v
[Native → WebView Bridge] Inject in-page prompt: "Enable biometric login?"
         |
        YES (user taps "Enable")
         |
         v
[WebView JS] POST /umbraco/prism/mobile/biometric/register
             (sends: { tenantId via cookie context })
             (auth: PrismMemberCookie)
         |
         v
[Server] Extract user OID from PrismMemberCookie claims
         Issue signed BiometricToken (JWT) containing: deviceId, tenantId, userOid, iat, exp
         Encrypt + store Entra refresh_token alongside BiometricToken record
         Save PrismBiometricRecord to DB (see schema below)
         Return: { biometricToken: "...", expiresAt: "..." }
         |
         v
[WebView JS] Pass biometricToken back to native via Capacitor bridge
         |
         v
[Native Layer] Store in platform Keychain/Keystore:
               Key:   "prism-biometric-{tenantHostname}"
               Value: { biometricToken, tenantHostname, userHint }
               Access: biometric-only (kSecAccessControlBiometryAny / BIOMETRIC_STRONG)
         |
         v
[Native Layer] Show success banner: "Biometric login enabled"
```

### What is stored where

| Location | What | How |
|---|---|---|
| Device Keychain (iOS) / Keystore (Android) | `BiometricToken` + `tenantHostname` | Encrypted by OS, biometric-gated |
| Server DB (`prismBiometricTokens`) | Hashed `BiometricToken`, `DeviceId`, encrypted Entra `refresh_token`, user OID, tenant ID, expiry, revoked flag | Encrypted at rest |

The Entra `refresh_token` is never sent to the device. It is stored server-side only, encrypted, and only used server-side during a token exchange.

---

## Login Flow

### Step-by-step (happy path)

```
App launches
         |
         v
[Native Layer] Check Keychain for "prism-biometric-{tenantHostname}"
         |
      FOUND
         |
         v
[Native Layer] Prompt biometric: "Sign in to {AppName}"
         |
      PASS
         |
         v
[Native Layer] Retrieve BiometricToken from Keychain
         |
         v
[Native Layer] POST /umbraco/prism/mobile/biometric/exchange
               Body: { biometricToken: "..." }
               (no auth cookie required — this IS the auth step)
         |
         v
[Server] Validate JWT signature and claims (not expired, tenant matches request hostname)
         Look up PrismBiometricRecord by hashed token
         Check: not revoked, DeviceId in JWT claims matches registered DeviceId in DB row
         Use stored encrypted refresh_token → call Entra /token endpoint
         On success: get new access_token + new refresh_token
         Update stored refresh_token in DB (rolling rotation)
         Set PrismMemberCookie on HTTP response (same as post-OIDC flow)
         Return: 200 OK (with Set-Cookie header)
         |
         v
[Native Layer] Extract Set-Cookie header from exchange response
               Inject cookie into WKWebView (iOS: WKHTTPCookieStore)
                                       (Android: CookieManager.getInstance().setCookie())
         |
         v
[Native Layer] Navigate WebView to start URL
         |
         v
User is authenticated. No OIDC redirect seen.
```

### Fallback: biometric fails or is cancelled

```
[Native Layer] Biometric FAIL / CANCEL
         |
         v
[Native Layer] Fall back to full Entra OIDC flow
               (existing compliance mode / in-WebView flow, per mobile.md D4)
         |
         v
After OIDC success → re-offer biometric enrol prompt IF device supports it
```

### Fallback: BiometricToken is expired or revoked

```
[Server] Exchange request → token not found / revoked / expired
         Return: 401 { error: "biometric_token_invalid" }
         |
         v
[Native Layer] Delete stored Keychain credential for this tenant
               Fall back to full Entra OIDC flow
         |
         v
After OIDC success → re-offer biometric enrol prompt
```

### Fallback: Entra refresh_token is expired or rejected

```
[Server] Entra /token call returns error (refresh expired / user revoked)
         Return: 401 { error: "credential_refresh_failed" }
         |
         v
[Native Layer] Delete stored Keychain credential
               Fall back to full OIDC
         |
         v
After OIDC success → re-offer biometric enrol prompt
```

---

## Revocation Flow

### User-initiated unenrol

```
User navigates to "Account settings" in app
Taps "Remove biometric login"
         |
         v
[WebView JS] DELETE /umbraco/prism/mobile/biometric/unenrol
             (auth: PrismMemberCookie)
         |
         v
[Server] Soft-delete PrismBiometricRecord for user OID + tenant
         Return: 204 No Content
         |
         v
[Native Layer] Delete Keychain credential for this tenant
               Show confirmation: "Biometric login removed"
```

### Server-side revocation (tenant admin or Entra admin)

When a tenant admin revokes a user's access (via Prism backoffice or Entra), or Entra invalidates the underlying refresh token:

- The `PrismBiometricRecord` should be soft-deleted or marked revoked in the same action that removes the user's Prism access.
- On the user's next biometric exchange attempt, the server returns `401 biometric_token_invalid`.
- The app clears the Keychain credential and forces full OIDC re-auth.
- Since the Entra account itself is blocked, full OIDC will also fail, the user is effectively locked out, as intended.

There is no push notification or real-time revocation in v1. The device will hold a stale Keychain credential until the next login attempt. This is acceptable for v1, the server is the gatekeeper and will reject the exchange.

### Biometric enrollment change on device (new fingerprint added, Face ID reset)

iOS and Android can detect when the biometric enrollment set changes. When detected at app launch:

```
[Native Layer] Detect biometryEnrollmentChanged flag (iOS: LAError.biometryNotAvailable / biometryNotEnrolled after prior success)
         |
         v
Delete Keychain credential (it can no longer be accessed with old biometry)
Force full OIDC re-auth
After success → re-offer biometric enrol
```

This is a safety measure: if someone adds their fingerprint to a stolen unlocked phone, they should not inherit saved credentials.

---

## Prism Integration Points

### 1. How the biometric auth result enters the WebView session

The WebView and the Capacitor native layer share a WKWebView/WebView instance. The native layer can inject cookies directly into the WebView's cookie store before navigation. This is the cleanest path:

- **iOS:** Use `WKHTTPCookieStore.setCookie()` after the exchange response. The WKWebView will send the cookie on the first request to the tenant hostname.
- **Android:** Use `CookieManager.getInstance().setCookie(tenantUrl, cookieHeaderValue)` before `webView.loadUrl()`.

The alternative (passing a token via the Capacitor JS bridge and having the WebView call a Prism endpoint) adds a round-trip and exposes the token to WebView JS. Prefer native cookie injection.

### 2. Capacitor bridge surface

The generated app bundle needs a thin native bridge module that:

1. Wraps `@aparajita/capacitor-biometric-auth` (or equivalent) for biometric prompt and availability check.
2. Wraps platform Keychain/Keystore access (via `capacitor-secure-storage-plugin` or equivalent; **not** `@capacitor/preferences`, that is not hardware-backed).
3. Makes the `/exchange` HTTP call from native (not from WebView JS) to receive the `Set-Cookie` header directly in the native HTTP client, necessary because `Set-Cookie` headers on cross-origin responses are not accessible to WebView JS.
4. Injects the resulting cookie into the WebView store.

This bridge module is generated as part of the Capacitor starter bundle by `MobileBundleService`.

### 3. Capacitor bridge → WebView communication

After successful biometric exchange and cookie injection, the native layer fires a Capacitor event to the WebView:

```js
// From native to WebView
Capacitor.triggerEvent('prismBiometricLoginComplete', {});
```

The WebView JS (injected by `PrismBrandingMiddleware` or inline in the generated `www/index.html`) listens for this event and initiates navigation to the authenticated start URL. No token is passed in this event, the cookie is already in the store.

---

## Backend Changes Required

### New API controller: `PrismBiometricController`

Hosted under `/umbraco/prism/mobile/biometric/`. Three endpoints:

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/register` | `PrismMemberCookie` required | Create a BiometricToken for the authenticated user |
| POST | `/exchange` | None (token IS the credential) | Exchange BiometricToken for a new `PrismMemberCookie` |
| DELETE | `/unenrol` | `PrismMemberCookie` required | Remove the user's BiometricToken record |

### New DB table: `prismBiometricTokens`

```sql
CREATE TABLE prismBiometricTokens (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TenantId        INT NOT NULL,                          -- FK to prismTenants
    UserOid         NVARCHAR(64) NOT NULL,                 -- Entra OID claim
    DeviceId        NVARCHAR(64) NOT NULL,                 -- Client-generated UUID; matches JWT deviceId claim
    TokenHash       NVARCHAR(128) NOT NULL UNIQUE,         -- SHA-256 of BiometricToken JWT
    RefreshTokenEnc NVARCHAR(MAX) NOT NULL,                -- Entra refresh_token, encrypted at rest
    CreatedAt       DATETIMEOFFSET NOT NULL,
    LastUsedAt      DATETIMEOFFSET NULL,
    ExpiresAt       DATETIMEOFFSET NOT NULL,               -- Default: 30 days from registration
    RevokedAt       DATETIMEOFFSET NULL                    -- NULL = active
);

CREATE INDEX IX_prismBiometricTokens_TokenHash ON prismBiometricTokens (TokenHash);
CREATE INDEX IX_prismBiometricTokens_TenantUser ON prismBiometricTokens (TenantId, UserOid);
```

Add migration to `PrismMigrationPlan`.

### New service: `IPrismBiometricService` / `PrismBiometricService`

Handles:
- Generating and storing BiometricToken records (`RegisterAsync`)
- Exchanging a BiometricToken for a new `PrismMemberCookie` via Entra token refresh (`ExchangeAsync`)
- Soft-deleting records on unenrol or revocation (`RevokeAsync`)
- Purging expired/revoked records (background cleanup, optional for v1)

The `/exchange` endpoint calls `PrismBiometricService.ExchangeAsync`, which:
1. Validates the JWT signature and claims (not expired, tenant matches request hostname).
2. Hashes the token and looks up the `PrismBiometricRecord`.
3. Validates not revoked; DeviceId in JWT claims matches registered DeviceId in the DB row.
4. Decrypts the stored Entra refresh_token.
5. Calls the Entra token endpoint (same path as `PrismContext.RefreshTokenAsync`).
6. Re-encrypts and stores the new refresh_token (rolling rotation).
7. Builds a `ClaimsPrincipal` from the returned token claims.
8. Signs and returns a `PrismMemberCookie` response using `HttpContext.SignInAsync`.

The RefreshToken encryption key should be stored in Azure Key Vault (via existing `SecretVaultService`), referenced by a new per-tenant or global secret key name.

### Changes to `MobileBundleService`

Add an optional `BiometricAuthEnabled` flag to `PrismMobileBundleRequest`. When true:

- Add `@aparajita/capacitor-biometric-auth` and `capacitor-secure-storage-plugin` to `package.json` devDependencies.
- Add a generated `src/biometric-bridge.ts` file containing:
  - `checkBiometricAvailability()`, wraps plugin availability check
  - `promptBiometricAndExchange(tenantUrl)`, biometric prompt → POST `/exchange` → inject cookie → return bool
  - `registerBiometric(tenantUrl, biometricToken)`, store to secure storage
  - `unenrolBiometric(tenantUrl)`, clear from secure storage + call DELETE `/unenrol`
  - Event emit on `prismBiometricLoginComplete`
- Add a `biometric.enabled` field to `capacitor.config.ts` (app-level flag read by the native layer at boot).
- Update `AGENT_PROMPT.md` to include biometric setup instructions.

`BiometricAuthEnabled` defaults to `false`. No changes to existing bundles.

---

## v1 Scope

### In scope

- Registration flow triggered after first successful Entra OIDC login
- Biometric prompt on app launch when credential is stored
- `/register`, `/exchange`, `/unenrol` endpoints
- `prismBiometricTokens` DB schema + migration
- `PrismBiometricService` (register, exchange, revoke)
- `MobileBundleService` changes (opt-in flag, generated `biometric-bridge.ts`)
- Fallback to full OIDC on all biometric failure paths
- Device biometric enrollment change detection and credential wipe
- Minimum exchange audit logging (attempt, outcome, token ID, IP), ~5 lines of code
- Server-side token expiry (30 days default, configurable per tenant, range: 7–90 days)

### Out of scope for v1

| Feature | Reason deferred |
|---|---|
| Multiple enrolled devices per user | Adds UI complexity; one device per user is sufficient for v1 |
| Tenant admin UI to view/revoke biometric enrollments | Backoffice work; not blocking core flow |
| Push notification on server-side revocation | Requires FCM/APNs integration; deferred |
| Biometric for Android API < 28 | API 28+ covers ~95% of active Android devices |
| Token rotation on exchange (rolling refresh) | Should land in v1, see note below |

> **Note on rolling refresh token rotation:** Rolling rotation (replace stored refresh_token on each successful exchange) is a security best practice and should be treated as a v1 hard requirement, not a deferral. If this slips, the threat model must be documented explicitly.

### Phased recommendation

| Phase | What ships |
|---|---|
| **v1, Core** | Registration, login, unenrol, fallback, DB schema, `MobileBundleService` opt-in |
| **v1.1, Hardening** | Rolling refresh token rotation, biometric enrollment change detection |
| **v2, Admin** | Backoffice UI: view enrolled devices, admin revoke, audit log |

---

## Open Questions (record before implementation starts)

1. **Refresh token encryption key:** Single global key in Key Vault, or one per tenant? Per-tenant is safer (blast radius on key compromise is contained) but adds operational complexity. Recommend global key with per-record IV for v1, with a path to per-tenant in v2.
2. **Token expiry duration:** Standardised at 30 days default, configurable per tenant (range: 7–90 days). Note: Entra's own refresh token window may be shorter than 90 days for some tenant CA policies, tenants with shorter Entra windows should configure the Prism token lifetime to match.
3. **In-WebView vs. compliance mode interaction:** If a tenant uses compliance mode (system browser OIDC per mobile.md D4), the post-OIDC callback lands in a different context. The registration prompt trigger point may need to differ. Needs validation against both auth modes.
4. **`/exchange` rate limiting:** Rate limiting policy: 3 failed exchange attempts within 10 minutes for a given token → token locked; requires re-registration. IP-based rate limiting as secondary layer.

---

## Security Considerations

### Overview

Biometric authentication in Prism Mobile introduces device-stored credentials that bypass full Entra OIDC flows on repeat visits. This creates significant security risks if not architected with tenant isolation, credential revocation, and bounded trust in mind.

### Recommended Credential Storage Approach

**DO NOT** store Entra refresh tokens in device keystores. Instead, use **Prism-issued device credentials** (the `BiometricToken` described in the architecture above).

**Architecture:**
1. After successful Entra login, the mobile app requests a device credential from the Prism backend (requires valid Entra access token)
2. Server issues a device-bound JWT containing:
   - Device ID (UUID generated on first registration)
   - Tenant ID (single tenant binding)
   - User ID (Entra object ID)
   - Expiration (30 days default, configurable per tenant, range: 7–90 days)
   - Signature (Prism backend signing key)
3. Device credential stored in iOS Keychain (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly` + `kSecAccessControlBiometryCurrentSet`) or Android Keystore (`setUserAuthenticationRequired(true)`)
4. On subsequent opens: biometric prompt → load device credential → exchange for short-lived access token via `POST /api/prism/device/exchange` → establish WebView session

**Rationale:**
- **Revocation Control:** Tenant admins can revoke device credentials without full user session termination
- **Least Privilege:** Device credential has narrower scope than Entra refresh token
- **Tenant Isolation:** Credential scoped to single tenant, preventing cross-tenant reuse
- **Bounded Lifetime:** Forces periodic full re-auth (max 30 days)
- **Device Binding:** Server validates device ID, detecting credential theft/replay

### Threat Model

| Threat | Likelihood | Impact | Mitigation |
|--------|------------|--------|------------|
| Device lost/stolen with biometric bypassed | Medium | High | 30-day max credential lifetime; server-side revocation; device binding validation on exchange |
| Root/jailbreak credential extraction | Low | High | Device registration approval flow; anomaly detection on exchange endpoint; credential rotation on suspicious activity |
| Cross-tenant credential leak | Low | **Critical** | Tenant ID in keystore key name (`prism_device_cred_{tenantId}_{userId}`); tenant-scoped JWT claims; exchange endpoint validates tenant match |
| Entra refresh token theft | N/A | **Critical** | **Mitigated by design:** Do not store Entra tokens on device |
| Biometric enrollment by attacker | Low | Medium | Device registration shows device name; admin audit of device list; revocation on suspicious access |
| Capacitor bridge compromise | Very Low | Medium | Device credential exchange via HTTPS; short-lived access token (5–15 min) limits exposure; WebView session cookie `httpOnly` |
| Credential replay after revocation | Low | Medium | Exchange endpoint checks revocation status in real-time; device ID binding prevents cross-device use |
| Extended credential lifetime abuse | Medium | Medium | Hard 30-day maximum enforced; tenant policy controls; forced full re-auth on expiration |

### Required Server-Side Controls

**1. Device Registry (Database)**
```
DeviceCredentials table:
- DeviceId (UUID, PK)
- TenantId (FK, indexed)
- UserId (Entra object ID)
- DeviceName (user-provided, for admin display)
- RegisteredAt, LastUsedAt, RevokedAt
- Platform (iOS/Android)
```

**2. Device Credential Issuance Endpoint**
- `POST /api/prism/device/register`
- Input: valid Entra access token, device name, platform
- Output: device credential JWT (7–30 day lifetime)

**3. Device Credential Exchange Endpoint**
- `POST /api/prism/device/exchange`
- Input: device credential JWT (from keystore)
- Output: short-lived access token (5–15 min)
- Validation: signature valid, not revoked, tenant matches, not expired, device binding consistent

**4. Admin Revocation API**
- `DELETE /api/prism/device/{deviceId}` (tenant admin only)
- Sets `RevokedAt` timestamp; subsequent exchange requests return `401 Unauthorized`

**5. Automatic Expiration**
- Default: 30 days maximum credential age (configurable per tenant)
- Expired credentials → force full Entra OIDC re-auth

### Multi-Tenant Isolation Requirements

- Keystore key naming pattern: `prism_device_cred_{tenantId}_{userId}`, prevents cross-tenant confusion
- Device credential JWT **MUST** contain `tenant_id` claim
- Exchange endpoint **MUST** validate request tenant matches credential tenant
- Registry queries **MUST** include tenant boundary filter

### Transport & Session Security

- Exchange endpoint requires HTTPS (enforced); consider certificate pinning for production
- Rate limiting: 3 failed exchange attempts within 10 minutes for a given token → token locked; requires re-registration. IP-based rate limiting as secondary layer.
- After successful exchange, native app injects access token via Capacitor bridge message to WebView
- Session cookie attributes: `Secure`, `HttpOnly`, `SameSite=Strict`, tenant-scoped path
- Access token lifetime: 5–15 minutes; native app re-exchanges device credential on expiry

### Hard Constraints

1. **No Entra Token Storage:** Device keystore MUST NOT contain Entra refresh tokens or long-lived access tokens
2. **Single-Tenant Binding:** Device credentials MUST be scoped to one tenant; no cross-tenant reuse
3. **Server-Side Registry:** All device credential lifecycle (issuance, validation, revocation) MUST be centrally controlled
4. **Bounded Lifetime:** Maximum 30-day credential age; no automatic renewal without full OIDC re-auth
5. **Biometric Failure Handling:** Failed biometric MUST trigger full Entra OIDC flow, no fallback to stored credential
6. **Revocation Check:** Every device credential exchange MUST check revocation status in real-time

### Root/Jailbreak Mitigation

- iOS: Check for jailbreak indicators at launch; Android: check for root indicators
- Server-side: flag suspicious device registrations for manual review
- Tenant policy option: require device registration approval by admin
- On detection: force full OIDC re-auth, skip biometric flow

### Token Lifetime & Revocation Summary

| Credential Type | Lifetime | Stored Where | Revocable By |
|-----------------|----------|--------------|--------------|
| Entra refresh token | 90 days+ | **NOT STORED** | N/A |
| Prism device credential | 30 days default, configurable per tenant (range: 7–90 days) | Device keystore (biometric-protected) | Tenant admin, automatic expiration |
| Access token (from exchange) | 5–15 minutes | WebView session (cookie) | Session logout, device credential revocation |

---

## Native Implementation

### Plugin Selection

**Biometric Authentication: `@aparajita/capacitor-biometric-auth@7.x`**

Rationale: Active maintenance with Capacitor 7 support, comprehensive iOS (FaceID/TouchID) and Android (BiometricPrompt API) coverage, built-in fallback to device PIN/passcode, strong TypeScript types, and superior maintenance status vs `@capacitor-community/biometric-auth`.

**Secure Storage: `@aparajita/capacitor-secure-storage@7.x`**

Rationale: Native mapping to iOS Keychain (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`) and Android EncryptedSharedPreferences (Keystore-backed AES256-GCM). Same author as the biometric plugin, consistent API surface. Superior to `@capacitor/preferences` (no encryption) and `capacitor-secure-storage-plugin` (unmaintained).

**Version Dependencies:**
```json
{
  "@aparajita/capacitor-biometric-auth": "^7.0.0",
  "@aparajita/capacitor-secure-storage": "^7.0.0"
}
```

### Platform Requirements

#### iOS

**Info.plist:**
```xml
<key>NSFaceIDUsageDescription</key>
<string>We use Face ID to securely log you in without requiring your password each time.</string>
```
> TouchID does not require a usage description. FaceID silently fails without this key.

#### Android

**AndroidManifest.xml:**
```xml
<uses-permission android:name="android.permission.USE_BIOMETRIC" />
```
Minimum SDK: API 23 for Keystore. BiometricPrompt API requires API 28+; plugin uses FingerprintManager compat on API 23–27.

### iOS vs Android Differences

| Aspect | iOS | Android |
|--------|-----|---------|
| Biometric types | FaceID (iPhone X+), TouchID (5s+) | Fingerprint, Face, Iris (device-dependent) |
| Usage description | Required for FaceID (`NSFaceIDUsageDescription`) | Not required |
| Permission | None (capability check only) | `USE_BIOMETRIC` in manifest |
| Lockout | 5 failures → `biometryLockout` | BiometricPrompt error codes (mapped by plugin) |
| Simulator/Emulator | `isAvailable: false`, no biometrics | Mock fingerprint via `adb emu finger touch 1` |

### Capability Detection

```typescript
import { BiometricAuth, BiometryError } from '@aparajita/capacitor-biometric-auth';

async function checkBiometricCapability() {
  const info = await BiometricAuth.checkBiometry();
  return {
    available: info.isAvailable,
    biometryType: info.biometryType,
    reason: info.reason
  };
}
```

**Fallback strategy:**
- `isAvailable: false` → hide biometric enrollment option; web login only
- `biometryNotEnrolled` → show "Enable Face ID in Settings" message
- `biometryLockout` → fall back to web login with message
- Always provide "Skip" / "Use Password" option; never block app usage

### Registration Flow (Native Side)

Triggered after Entra OIDC completes successfully. The web app signals the native layer via Capacitor bridge that auth succeeded.

```typescript
async function handleAuthSuccess(userId: string, prismDeviceToken: string) {
  const info = await BiometricAuth.checkBiometry();
  if (!info.isAvailable) return; // No biometrics — skip silently

  const wantsEnrollment = await promptEnableBiometrics(info.biometryType);
  if (!wantsEnrollment) return;

  await BiometricAuth.authenticate({
    reason: 'Confirm your identity to enable biometric login',
    allowDeviceCredential: true,
    iosFallbackTitle: 'Use Passcode'
  });

  // Store the Prism-issued device credential (NOT the Entra token)
  await SecureStorage.set({ key: `prism.biometric.${tenantId}.${userId}`, value: prismDeviceToken });
}
```

### Login Flow (Native Side)

On app launch, before loading the WebView:

```typescript
async function attemptBiometricLogin(tenantId: string, userId: string) {
  const credentialKey = `prism.biometric.${tenantId}.${userId}`;
  const hasCredential = await SecureStorage.get({ key: credentialKey }).then(r => !!r.value).catch(() => false);

  if (!hasCredential) { loadWebView(); return; }

  try {
    await BiometricAuth.authenticate({ reason: 'Log in with biometrics', allowDeviceCredential: true });
    const { value: deviceToken } = await SecureStorage.get({ key: credentialKey });
    const sessionToken = await exchangeDeviceToken(deviceToken); // POST /api/prism/device/exchange
    await injectSessionIntoWebView(sessionToken);
    loadWebView();
  } catch (error) {
    // Any failure → fall back to full Entra OIDC web flow
    loadWebView();
  }
}
```

### Changes to MobileBundleService Output

- **`BuildPackageJson()`**: Add `@aparajita/capacitor-biometric-auth` and `@aparajita/capacitor-secure-storage` to dependencies (gated on `BiometricAuthEnabled` flag)
- **`BuildBootstrapIosScript()`**: Auto-inject `NSFaceIDUsageDescription` into `Info.plist` after `npx cap add ios`
- **`BuildBootstrapAndroidScript()`**: Auto-inject `USE_BIOMETRIC` permission into `AndroidManifest.xml` after `npx cap add android`
- **`BuildReadme()`**: Add biometric setup section documenting iOS/Android prerequisites and simulator testing note
- **No changes to `capacitor.config.ts`**: Plugins auto-register via Capacitor discovery

### Testing Checklist

- [ ] Physical iOS device: FaceID/TouchID prompt appears; fallback to passcode works
- [ ] iOS Simulator: "Biometrics not available" fallback shown
- [ ] Physical Android: fingerprint prompt appears; fallback to PIN works
- [ ] Android emulator: mock fingerprint via `adb emu finger touch 1`
- [ ] Stored credential survives app restart
- [ ] Biometric lockout (5 failures) falls back to web login gracefully
- [ ] Credential cleared on logout
- [ ] Credential expired → forces full OIDC re-auth
