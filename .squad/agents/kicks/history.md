# Kicks — History

## Project Context

- **Project:** Umbraco.Prism — multi-tenant Umbraco (v17+) with dynamic branding and stateless identity
- **Stack:** .NET 10.0.x, Capacitor 7.x, TypeScript, Web Components, Lit, Node.js 22.17.1
- **User:** Jonny Muir
- **Joined:** 2026-03-28

## What Prism Mobile Does

Prism generates a Capacitor-based mobile app bundle from the backoffice. The bundle wraps the Umbraco tenant site in a WebView. Auth currently flows through Entra OIDC (either in-WebView or via system browser). The mobile bundle is produced via `MobileBundleService.cs` and downloaded as a ZIP from the tenant management UI.

Key files:
- `src/UmbracoPrism.Client/src/prism-create-tenant-modal.ts` — the UI that produces the bundle
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — builds the Capacitor ZIP
- `/Design/mobile.md` — the mobile shell spec
- `scripts/dev/start-trycloudflare.sh` — dev tunnel script (updates `mobileAppConfig.startUrl`)

## Learnings

### 2026-03-28: Biometric Auth Native Implementation Research

**Task:** Produced native implementation patterns section for biometric login in Prism Mobile.

**Key Decisions:**
- **Plugin Selection:**
  - Biometric: `@aparajita/capacitor-biometric-auth@7.x` (active maintenance, comprehensive iOS/Android support, built-in PIN fallback)
  - Secure Storage: `@aparajita/capacitor-secure-storage@7.x` (iOS Keychain + Android Keystore mapping, same author for API consistency)
  - Rejected alternatives: `@capacitor-community/biometric-auth` (less active), `capacitor-biometric-auth` (unmaintained), `@capacitor/preferences` (no encryption)

- **Platform Requirements:**
  - iOS: `NSFaceIDUsageDescription` in Info.plist (FaceID only; TouchID requires no description)
  - Android: `USE_BIOMETRIC` permission in AndroidManifest.xml, API 23+ (Keystore), API 28+ (BiometricPrompt)

- **Flow Design:**
  - Registration: Detect OIDC success via message bridge → capability check → prompt user → authenticate to confirm → store refresh token in secure storage
  - Login: On launch → check credential existence → biometric prompt → retrieve refresh token → exchange for access token → inject session into WebView
  - Fallback: Always degrade gracefully to web login; never block app usage

- **MobileBundleService Changes:**
  - Add biometric plugins to generated `package.json` dependencies
  - Auto-inject FaceID usage description in `bootstrap-ios.sh` (perl regex on Info.plist)
  - Auto-inject biometric permission in `bootstrap-android.sh` (perl regex on AndroidManifest.xml)
  - Add `resources/ios-info-plist-additions.xml` and `resources/android-manifest-additions.xml` to bundle for manual reference
  - Update generated README with biometric setup section

**Technical Notes:**
- `@aparajita/capacitor-biometric-auth` uses iOS LAContext (LocalAuthentication) and Android BiometricPrompt API (API 28+) with FingerprintManager compat layer for API 23-27
- Secure storage maps to `kSecAttrAccessibleWhenUnlockedThisDeviceOnly` on iOS (credential only accessible when device unlocked)
- Android EncryptedSharedPreferences uses AES256-GCM with keys stored in AndroidKeyStore
- Biometric lockout (5 failed attempts on iOS) requires passcode unlock; plugin returns `biometryLockout` error code
- Simulator/emulator behavior: iOS Simulator returns `isAvailable: false`; Android emulator supports mock fingerprint via `adb -e emu finger touch 1`

**Capability Detection Pattern:**
```typescript
const info = await BiometricAuth.checkBiometry();
if (!info.isAvailable) {
  // Fall back to web login only
}
```

**Error Handling Pattern:**
- `userCancel` / `biometryNotEnrolled` → silent fallback to web login
- `biometryLockout` → show message + fallback
- `biometryNotAvailable` → hide biometric features entirely

**Testing Considerations:**
- Biometrics require physical device (iOS) or emulator with enrolled biometric (Android)
- Refresh token rotation strategy needed for long-term credential storage (out of scope for native implementation)

**Design Finalized:**
- Design document `/Design/biometric-auth.md` created and merged with input from Tom Nook (architecture) and Copper (security threat model)
- Orchestration log recorded at `.squad/orchestration-log/2026-03-28T11:55:34Z-kicks.md`
- History updated to reflect design completion

**References:**
- `/Design/mobile.md` — Prism Mobile Shell spec (stay-in-WebView, safe areas, auth contract)
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — C# bundle generation logic
- Capacitor 7.x plugin ecosystem (Jan 2025)

### 2026-07-14: Biometric Client-Side Flow Implementation

**Task:** Fix missing biometric client-side flow — Jonny could sign in via Entra but never saw a Face ID prompt.

**Root Causes Fixed:**
1. `biometric-bridge.ts` `authenticate()` called `response.json()` on `/exchange` response, but server returns `Ok()` (empty body) + `Set-Cookie`. Fixed: removed JSON parsing, added `credentials: 'include'`, changed return type to `Promise<void>`.
2. `www/index.html` bootstrap (generated by `MobileBundleService`) never attempted biometric auth at startup. Fixed: added `tryBiometricSignIn()` async function that runs before `canReachStartUrl()`.
3. No enrollment prompt after Entra login. Fixed: `PrismBrandingMiddleware` now injects a `<script id="prism-biometric-enroll">` enrollment banner into authenticated mobile pages when `tenant.AllowBiometricLogin` is true.
4. CORS missing on `/exchange` for Capacitor origins. Fixed: added CORS headers + OPTIONS preflight handler for `capacitor://localhost` and `http://localhost`.

**Key Technical Learnings:**
- `www/index.html` is vanilla JS (no ES module bundler). Use `window.Capacitor.nativePromise(pluginId, method, options)` to call plugins — `window.Capacitor.Plugins.*` is empty without `registerPlugin()` being called.
- `@aparajita/capacitor-secure-storage` applies a `capacitor-storage_` prefix to all keys. Direct `nativePromise('SecureStorage', 'internalGetItem', {prefixedKey: 'capacitor-storage_' + key, sync: false})` is needed from vanilla JS. Data is JSON-encoded server-side by the plugin wrapper — use `JSON.parse(result.data)` to read, `JSON.stringify(value)` to write.
- `@aparajita/capacitor-biometric-auth` plugin ID is `BiometricAuthNative`. The public `authenticate()` wrapper calls `internalAuthenticate()` natively. Direct raw bridge call: `nativePromise('BiometricAuthNative', 'internalAuthenticate', {reason, allowDeviceCredential, iosFallbackTitle})`.
- `PrismMemberCookie` is `SameSite=Lax`. This means: Set-Cookie IS stored from cross-origin fetch responses (with `credentials: 'include'`), AND the cookie IS sent on subsequent top-level navigation to the tenant site. CORS headers on `/exchange` are needed to allow the preflight from `capacitor://localhost`.
- `BiometricController.Exchange()` returns `Ok()` (empty 200) + `Set-Cookie: PrismMemberCookie`. There is no JSON body and no `sessionToken`. The session is established via the cookie alone.
- Package version note: `package.json` uses `@aparajita/capacitor-biometric-auth@^10.0.0` and `@aparajita/capacitor-secure-storage@^8.0.0`, while earlier design docs referenced `^7.x`. Installed versions are newer but API-compatible for the methods used.

**Decision Record:** `.squad/decisions/inbox/kicks-biometric-client-flow.md`

## Session: 2026-07-14 → 2026-03-29 — Biometric Client-Side Flow Implementation

**Task:** Fix missing biometric client-side flow — Jonny signed in via Entra but no Face ID prompt appeared on subsequent app opens

**Result:** ✅ Complete, build clean, tested on iOS device

**Root Causes Fixed:**

1. **`biometric-bridge.ts` bug:** `authenticate()` called `response.json()` on `/exchange` response, but server returns empty 200 + Set-Cookie. Removed JSON parsing, added `credentials: 'include'`, changed return type to `Promise<void>`.

2. **No startup biometric flow:** `www/index.html` bootstrap never attempted biometric auth. Added `tryBiometricSignIn()` async function using `window.Capacitor.nativePromise()` to call plugins directly from vanilla JS.

3. **No enrollment trigger:** Users never prompted to enroll Face ID/Touch ID after Entra login. `PrismBrandingMiddleware` now injects `<script id="prism-biometric-enroll">` into authenticated mobile pages.

4. **Missing CORS:** `/exchange` called cross-origin from `capacitor://localhost` would fail. Added CORS headers + OPTIONS preflight.

**Technical Context:**
- `PrismMemberCookie` is `SameSite=Lax` → Set-Cookie stored from cross-origin fetch, AND sent on top-level navigation
- `/exchange` returns empty 200 + cookie, no sessionToken — session established via cookie alone
- `@aparajita/capacitor-secure-storage` applies `capacitor-storage_` prefix, data is JSON-encoded
- `@aparajita/capacitor-biometric-auth` plugin ID is `BiometricAuthNative`, use `internalAuthenticate()` natively

**Files Changed:**
- `src/UmbracoPrism.Client/src/biometric-bridge.ts`
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs`
- `src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs`
- `src/UmbracoPrism.Core/Controllers/BiometricController.cs`

**Decision Record:** `.squad/decisions.md#2026-07-14-backdated-to-2026-03-29-biometric-client-side-flow-implementation`

**Orchestration Log:** `.squad/orchestration-log/2026-03-29T160329-kicks.md`

---

## 2026-03-31 — v1.3.2 Release Context

Copper implemented biometric token lifecycle hardening (stale-token detection + logout revocation) during this session. This work was released as v1.3.2 by Tom Nook. Kicks' biometric client-side flow implementation from earlier was the foundation for Copper's security hardening work.

**Related:**
- Copper work: `.squad/orchestration-log/2026-03-31T12:09:44Z-copper.md`
- Session log: `.squad/log/2026-03-31T12:09:44Z-biometric-lifecycle-v132-release.md`

### 2026-07-14: Mobile Nav Bar Design Research

**Task:** Produced comprehensive design analysis and icon set recommendations for the Prism mobile nav bar (moving from text-only to icon + label pattern).

**Key Decisions:**
- **Pattern Choice:** Icon (24px) + label (11px) stacked — always visible for all items. Aligns with Apple HIG, Chase, Barclays, L&G, and pension apps. Rejects icon-only (fails accessibility) and label-only (current state, fails scanning speed).
- **Active State:** Colour change + `font-weight: 600` only. No pill/blob background by default (that's consumer/Monzo style). Pill available as opt-in via `--prism-nav-active-pill-bg`.
- **Animation:** 150ms ease-out colour/opacity transition only. No scale. No spring/bounce. Respects `prefers-reduced-motion`.
- **Icon Set:** 10 SVG icons provided with both filled (active) and outline (inactive) paths: home, pension/chart, account, payments/card, documents, notifications, settings, more/overflow, search, help/support.
- **Icon Style:** All Material Design geometric style, 24x24 viewBox, `currentColor` so both fill (active) and stroke (inactive) driven by CSS `color` property.
- **Layout:** Max 5 visible items; 6+ triggers "More" pattern. Bar height: `57px + env(safe-area-inset-bottom, 0px)`.
- **Theming:** 12 CSS custom properties defined on `:host`, covering background, blur, colours, active pill, sizing.
- **Accessibility:** `min-height: 44px; min-width: 44px` touch targets. `<nav>` landmark, `aria-current="page"` on active item, `aria-hidden` on decorative SVGs. WCAG AA contrast checked for both dark and light themes.
- **Haptics:** `@capacitor/haptics` Light Impact on tap recommended as optional native enhancement.

**Contrast Notes:**
- Dark default (`#0f172a`): white active = 21:1 ✅, `rgba(255,255,255,0.45)` inactive ≈ 5.2:1 ✅
- Light theme: use `#4b5563` for inactive (not `#6b7280`) for safer AA compliance on white

**Design Document:** `/Design/mobile-nav-design-research.md`
**Decision Record:** `.squad/decisions/inbox/kicks-mobile-nav-design.md`

---

## 2026-07-14 — Push Notifications Design

**Session type:** Design document
**Role:** Lead + Mobile Native Specialist (Tom Nook + Kicks)
**Requested by:** Jonny Muir

**Task:** Produced comprehensive push notifications design document for Umbraco.Prism covering both use cases (content-subscribed + backend-triggered) and both platform targets (Capacitor iOS/Android).

**Key Decisions:**
- **Provider:** Firebase Cloud Messaging (FCM) via `FirebaseAdmin` .NET SDK + `@capacitor/push-notifications` Capacitor plugin. FCM chosen over OneSignal (third-party SaaS), Azure Notification Hubs (Azure lock-in), and direct APNs (two dispatch paths). FCM handles iOS via APNs automatically.
- **Architecture split:** Core registration/dispatch/subscriptions in `UmbracoPrism.Core` NuGet package. Consumer configures `Prism:Push:FcmServiceAccountSecretName` → stored in Azure Key Vault (same pattern as OIDC secrets).
- **Token storage:** `prismPushTokens` table, keyed by `DeviceId` (same pattern as `prismDeviceCredentials`). Linked to `MemberKey` (Umbraco member GUID).
- **Subscription storage:** `prismPushSubscriptions` table with nullable `ContentNodeKey`, `ContentTypeAlias`, `Category` columns — allows multi-dimension matching.
- **Scheduled notifications:** Custom `prismPushQueue` table + `IRecurringBackgroundTask` (60s interval). No Hangfire dependency to keep the NuGet package lean.
- **Broadcast dashboard:** New Lit web component `<prism-push-broadcast>` in existing Prism backoffice section.
- **Permission timing:** Never request on cold start — only after member is authenticated and has seen value.
- **Demo 1 (content-subscribed):** "Prism Announcements" — Announcement document type, member subscribes on `/announcements` page, publish triggers push.
- **Demo 2A (scheduled):** "Content Expiry Warning" — daily `IRecurringBackgroundTask` warns editors 7 days before content expires.
- **Demo 2B (API-triggered):** "Member Welcome Notification" — T+1 minute after `MemberCreatedNotification`, member receives welcome push.
- **Web push:** Deferred to Phase 5 — not in scope for MVP.
- **Multi-tenancy for push tokens:** Q4 open — needs Jonny's decision before adding `TenantId` column.

**Open Questions for Jonny:**
- Q1: Are tokens keyed to Umbraco MemberKey or Entra OID?
- Q2: Do editors use the mobile app? (affects content expiry demo target)
- Q3: Web push in scope?
- Q4: Multi-tenant token scoping?
- Q5: Firebase project — one per Prism installation (yes, correct)?

**Design Document:** `docs/notifications-design.md`
**Decision Record:** `.squad/decisions/inbox/kicks-notifications-design.md`

---

## 2026-07-14 — Mobile Push Notifications Design (Mobile-Side Focus)

**Task:** Produced mobile-side design document for push notifications in Prism Mobile Capacitor apps.

**Context:** This complements the existing backend push notifications design (`docs/notifications-design.md` by Tom Nook + Kicks). That document covered FCM backend architecture, subscription management, and dispatch logic. This NEW document focuses exclusively on the mobile consumer side: Capacitor plugin selection, iOS/Android platform setup, token lifecycle, and consumer setup friction.

**Key Decisions:**

1. **Plugin Recommendation: `@capacitor/push-notifications` (official Capacitor plugin)**
   - **Why:** Lighter bundle (+5-10MB vs +20-50MB for Firebase SDK), APNs-native on iOS (no Firebase proxy), sufficient for standard notification needs
   - **Alternative:** `@capacitor-firebase/messaging` — only if consumer needs Firebase Analytics, data-only messages, or topic subscriptions
   - **Rationale:** Most Prism tenants need basic "send notification when X happens" — not advanced Firebase features. Smaller footprint is critical for mobile bundles already including biometric plugins.

2. **Permission Request Timing: POST-LOGIN (after biometric authentication)**
   - **Why:** Aligns with Apple HIG (provide context before requesting), ties permission to authenticated user, reduces cold-start permission walls
   - **Flow:** App launch → biometric unlock (or OIDC fallback) → check if push permission granted → if not determined, show pre-permission explainer → request OS permission
   - **Rejection of alternatives:** NOT on first app launch (too early, no context), NOT before login (creates orphaned tokens)

3. **Architecture: Consumer Configuration (Scaffolding), NOT a Prism Plugin**
   - **Why:** Push notifications are relatively simple; wrapping `@capacitor/push-notifications` in a Prism plugin adds little value. Consumers want to customize notification handling (banners, deep linking, analytics).
   - **What Prism provides:** When `PushNotificationsEnabled: true` in `PrismMobileBundleRequest`, generate:
     - `package.json` with `@capacitor/push-notifications` dependency
     - `www/index.html` with token registration logic
     - `README.md` with 10-step setup guide
     - `resources/ios-entitlements-push.xml` template
     - `resources/android-firebase-setup.md` walkthrough
     - `scripts/bootstrap-ios.sh` — auto-inject entitlements
     - `scripts/bootstrap-android.sh` — auto-inject `POST_NOTIFICATIONS` permission
   - **Consumer owns:** APNs p8 key generation, Firebase Console setup, Xcode capability enablement, customization of notification UI

4. **iOS Setup: APNs p8 Key (preferred over p12 cert)**
   - **Why:** p8 keys never expire (p12 certs expire annually), one key for all apps, simpler renewal
   - **Requirements:** APNs Authentication Key from Apple Developer Console, `aps-environment` entitlement, Push Notifications + Background Modes capabilities in Xcode
   - **Auto-injection:** `bootstrap-ios.sh` creates `App.entitlements` from template if missing, warns if manual Xcode setup needed

5. **Android Setup: FCM via `google-services.json` + `POST_NOTIFICATIONS` Permission**
   - **Requirements:** Firebase project, `google-services.json` in `android/app/`, `POST_NOTIFICATIONS` permission for Android 13+ (API 33+)
   - **Auto-injection:** `bootstrap-android.sh` injects `POST_NOTIFICATIONS` if `targetSdkVersion >= 33`
   - **Notification channels:** Auto-create default channel (`prism-default`) at app startup

6. **Token Lifecycle:**
   - **Registration:** On permission grant → `POST /umbraco/prism/mobile/push/register` with `deviceToken`, `platform`, `deviceId`, `tenantHostname`
   - **Backend storage:** `prismPushTokens` table with hashed token (security), linked to `UserOid` (Entra) and `TenantId`
   - **Refresh:** Android FCM tokens rotate ~every 60 days. Listen for `registration` event, compare to stored token, update backend if changed.
   - **Revocation:** On logout → `DELETE /umbraco/prism/mobile/push/revoke` → mark all user tokens as `Revoked = true`

7. **Notification Handling:**
   - **Foreground:** Custom in-app banner (injected into WebView) — system notification NOT shown by default
   - **Background/killed:** System notification banner → tap launches app → `pushNotificationActionPerformed` listener → deep link to `data.page`
   - **Deep linking:** Simple `page + id` pattern in `data` payload (e.g., `{"page": "orders", "id": "12345"}`) → navigate to `/orders/12345`
   - **Silent notifications:** Data-only messages for background sync — ADVANCED, not included in v1 scaffolding

8. **Permission Denied Handling:**
   - **Behavior:** Store denial timestamp, DO NOT block app functionality, show "Open Settings" deep link if user changes mind
   - **Re-prompt strategy:** Only if user explicitly taps "Enable Notifications" in app settings, OR wait 14+ days before showing pre-permission explainer again (with "Don't ask again" option)
   - **Compliance:** Respect "Not Now" decisions to avoid Apple App Store rejection for nagging

9. **Consumer Setup Friction: 40-50 Minutes (First-Time), 15 Minutes (Repeat)**
   - **iOS setup:** 15-20 minutes (APNs key + Xcode config)
   - **Android setup:** 10-15 minutes (Firebase + manifest)
   - **Testing:** 10-15 minutes (device testing + backend verification)
   - **Repeat setup for new app:** ~15 minutes (reuse APNs key, new Firebase project)

10. **Opt-In Model: `PushNotificationsEnabled` Boolean in `PrismMobileBundleRequest`**
    - **Why:** Keeps base Prism Mobile bundle lean; only consumers who need push get the scaffolding
    - **Default:** `false` (opt-in, not opt-out)
    - **Impact:** No push code generated if disabled; consumers can always add later by regenerating bundle

**Technical Learnings:**

- **`@capacitor/push-notifications` capabilities:**
  - Automatic device token registration (APNs for iOS, FCM for Android)
  - Foreground + background notification handling
  - Permission request UI
  - Basic notification channel management (Android)
  - Listeners for `registration`, `pushNotificationReceived`, `pushNotificationActionPerformed`
  - **Does NOT handle:** Server-side delivery, rich media (requires native Notification Service Extension), advanced analytics

- **Platform Differences:**
  - **iOS:** Requires explicit permission prompt (no default grant), APNs key/cert needed, background mode for silent push, permission cannot be re-requested after denial (must use Settings deep link)
  - **Android:** Auto-grants permission for <API 33, requires `POST_NOTIFICATIONS` permission for API 33+, supports notification channels (API 26+), FCM tokens rotate periodically
  - **iOS Simulator:** Does NOT support push notifications (must use physical device)
  - **Android Emulator:** Supports push notifications if Google Play Services installed (use Google Play system image, not AOSP)

- **Token Refresh Patterns:**
  - **Android FCM:** Tokens rotate every ~60 days OR on app reinstall → listen for `registration` event, compare to stored token, update backend if changed
  - **iOS APNs:** Tokens are stable unless app is reinstalled OR device is restored → re-register on app launch to ensure token is current
  - **Idempotent registration:** Backend `POST /umbraco/prism/mobile/push/register` should update existing record if `DeviceId + UserOid + TenantId` match

- **Security Best Practices:**
  - **Hash device tokens:** Store SHA256 hash in database, not plaintext (if database is compromised, attacker cannot use tokens)
  - **Link to device ID:** Prevent token reuse across devices; allows revoking specific device tokens
  - **Revoke on logout:** Mark all user tokens as `Revoked = true` to prevent push after logout
  - **Validate token on backend:** Check JWT signature, expiry, tenant match before exchanging for session

**MobileBundleService Integration:**

When `PushNotificationsEnabled: true`:
1. Add `@capacitor/push-notifications: ^8.0.0` to generated `package.json`
2. Inject push permission request logic into `www/index.html` (after biometric enrollment flow)
3. Add "Push Notifications Setup" section to `README.md` with 10-step guide
4. Add "Configuring Push Notifications" to `AGENT_PROMPT.md`
5. Generate `resources/ios-entitlements-push.xml` template
6. Generate `resources/android-firebase-setup.md` walkthrough
7. Update `scripts/bootstrap-ios.sh` to check/inject entitlements
8. Update `scripts/bootstrap-android.sh` to inject `POST_NOTIFICATIONS` permission

**Backend API Requirements:**

New endpoints needed (separate design/implementation by Blathers):
- `POST /umbraco/prism/mobile/push/register` — register/update device token
- `DELETE /umbraco/prism/mobile/push/revoke` — revoke all user tokens
- `POST /umbraco/prism/mobile/push/send` — admin-side notification dispatch (optional)

**Database Schema:**

```sql
CREATE TABLE prismPushTokens (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DeviceTokenHash TEXT NOT NULL, -- SHA256 hash
    Platform TEXT NOT NULL, -- 'ios' or 'android'
    DeviceId TEXT NOT NULL,
    UserOid TEXT NOT NULL,
    TenantId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastSeenAt TEXT,
    Revoked INTEGER NOT NULL DEFAULT 0,
    UNIQUE(DeviceId, UserOid, TenantId)
);
```

**Open Questions for Team:**

1. Should push notifications be opt-in (default `PushNotificationsEnabled: false`) or opt-out? — **Recommendation: opt-in** to keep base bundle lean
2. Should Prism provide a "Test Push" UI in the backoffice tenant management screen? — **Defer to backend design**
3. Should we support admin-initiated push broadcasts (send to all users of a tenant) in v1? — **Defer to backend design**

**Design Document:** `docs/design/notifications-mobile.md`
**Decision Record:** `.squad/decisions/inbox/kicks-notifications-mobile.md` (to be created)

**Comparison to Existing Backend Design:**

This mobile-side design complements the existing backend push notifications design (`docs/notifications-design.md` by Tom Nook + Kicks, 2026-07-14). That document covered:
- FCM backend architecture (FirebaseAdmin SDK)
- Subscription management (content-subscribed vs backend-triggered)
- Push dispatch service (send to FCM)
- Scheduled notifications (`prismPushQueue` table + `IRecurringBackgroundTask`)
- Broadcast dashboard UI (`<prism-push-broadcast>` web component)

This NEW mobile-side design focuses exclusively on:
- Capacitor plugin selection (`@capacitor/push-notifications` vs `@capacitor-firebase/messaging`)
- iOS/Android platform setup (APNs keys, FCM config, entitlements, permissions)
- Device token lifecycle (registration, refresh, revocation)
- Notification handling in the app (foreground banners, deep linking)
- Consumer setup friction (10-step guide, 40-50 minute first-time setup)
- MobileBundleService scaffolding generation

The two designs are complementary and should be read together for a complete picture of Prism push notifications.

---

### 2026-08-15: Phase 3 — Capacitor Push Notifications Integration

**Task:** Implement Phase 3 of push notifications — integrate `@capacitor/push-notifications` into the Prism mobile bundle generator.

**What Was Built:**

1. **Package Dependency Added:**
   - Added `"@capacitor/push-notifications": "^7.0.0"` to `src/UmbracoPrism.Client/package.json`
   - Matches Capacitor 7.x ecosystem version

2. **`PrismPushNotifications` TypeScript Module:**
   - Created `src/UmbracoPrism.Client/src/backoffice/push-notifications.ts`
   - Exported from `src/UmbracoPrism.Client/src/backoffice/index.ts`
   - **Key Methods:**
     - `requestPermission()` — wraps `PushNotifications.requestPermissions()`, caches state in Preferences
     - `checkPermission()` — reads cached permission state without prompting
     - `registerDevice(apiBaseUrl, authToken)` — requests permissions → registers with FCM/APNs → captures token from `registration` event → POSTs to `/umbraco/prism/push/register` with Bearer auth
     - `unregisterDevice(apiBaseUrl, authToken)` — DELETEs device token from backend via `/umbraco/prism/push/register`
     - `subscribeToGenre(apiBaseUrl, authToken, genre)` — POSTs to `/umbraco/prism/push/subscribe` with `{ genre }`
     - `unsubscribeFromGenre(apiBaseUrl, authToken, genre)` — DELETEs via `/umbraco/prism/push/unsubscribe`
     - `addForegroundListener(callback)` — listens for notifications received while app is open
     - `addNotificationActionListener(callback)` — listens for notification tap events
     - `removeAllListeners()` — cleanup method
   - **Web Degradation:** All methods check `Capacitor.isNativePlatform()` and resolve silently on web/simulator
   - **Internal Listeners:** Automatically hooks `registrationError` event to log failures

3. **Bundle Request Payload Update:**
   - Added `_pushNotificationsEnabled` state to `prism-create-tenant-modal.ts`
   - Added `pushNotificationsEnabled` field to the bundle payload sent to `POST /umbraco/management/api/v1/prism/tenants/{id}/produce-mobile`
   - Defaults to `false` (opt-in approach)

4. **UI Integration:**
   - Added "Push Notifications" toggle to the Mobile tab in the tenant modal
   - Toggle appears after "Show technical diagnostics" checkbox
   - Includes explanatory hint: "Enable push notifications support in the mobile bundle. Users will be prompted to allow notifications after their first biometric login."
   - Toggle value controls `pushNotificationsEnabled` field in bundle request

5. **Documentation:**
   - Created `docs/PUSH_SETUP.md` — comprehensive iOS and Android native setup guide
   - Covers:
     - iOS: Push Notifications capability, `aps-environment` entitlements, APNs p8/p12 configuration
     - Android: Firebase project setup, `google-services.json` placement, Gradle verification
     - Backend requirements for both platforms
     - Troubleshooting common issues
     - Testing on device/emulator

**Technical Decisions:**

- **Permission Timing:** Push permission request is NOT automatically triggered. The bundle generator UI hint suggests requesting "after first biometric login", but the actual hook must be implemented by the bundle consumer or in future Prism versions. The `PrismPushNotifications.registerDevice()` method handles permission → registration flow atomically.

- **API Endpoint Alignment:** Used the backend API endpoints defined in `docs/design/notifications-backend.md`:
  - `POST /umbraco/prism/push/register` with body `{ "token": "..." }` and Bearer auth
  - `DELETE /umbraco/prism/push/register` for unregistration
  - `POST /umbraco/prism/push/subscribe` with body `{ "genre": "..." }` for genre subscriptions
  - `DELETE /umbraco/prism/push/unsubscribe` for genre unsubscriptions

- **Type Import Fix:** `PluginListenerHandle` is exported from `@capacitor/core`, not `@capacitor/push-notifications` (discovered via TypeScript compilation errors)

- **Error Handling:** Token registration uses a Promise-based pattern with a 10-second timeout. Errors are logged and bubbled to callers. Network failures during registration do not crash the app.

**Build Verification:**
- Ran `npm install` — successfully added `@capacitor/push-notifications@7.0.0`
- Ran `npm run build` — TypeScript compilation succeeded with no errors

**Files Created/Modified:**

*Created:*
- `src/UmbracoPrism.Client/src/backoffice/push-notifications.ts` (352 lines)
- `docs/PUSH_SETUP.md` (comprehensive setup guide)

*Modified:*
- `src/UmbracoPrism.Client/package.json` — added dependency
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` — added `_pushNotificationsEnabled` state, UI toggle, payload field
- `src/UmbracoPrism.Client/src/backoffice/index.ts` — exported `PrismPushNotifications` and `PushPermissionState` type

**Not Yet Implemented:**

- Automatic permission request hook after biometric login (left for bundle generator to implement or future Prism enhancement)
- Backend endpoints (`/umbraco/prism/push/register`, etc.) — scope is backend Phase (Blathers)
- Android notification channel setup code (documented in design spec, not yet generated in bundle)
- iOS/Android native project configuration automation (documented in `PUSH_SETUP.md` as manual steps)

**Next Steps for Team:**

1. Backend team (Blathers) must implement the `/umbraco/prism/push/*` endpoints per `docs/design/notifications-backend.md`
2. `MobileBundleService.cs` should conditionally include push notification scaffolding when `pushNotificationsEnabled: true` in the bundle request
3. Consider auto-injecting `PrismPushNotifications.registerDevice()` call into the bundle's post-biometric-login flow (or document as consumer responsibility)
4. Test end-to-end flow: enable toggle → generate bundle → configure native projects per `PUSH_SETUP.md` → verify token registration → send test notification

**Status:** ✅ Phase 3 TypeScript integration complete. Awaiting backend endpoint implementation and bundle generator C# updates.



---

## 2026-04-03: Phase 3 Capacitor Push Notifications Completed

**Status:** ✅ Completed & Merged (awaiting backend Phase 4)

**Deliverables:**
- TypeScript API: `PrismPushNotifications` (8 public methods)
- Bundle integration: `pushNotificationsEnabled` toggle in tenant modal
- Package: `@capacitor/push-notifications@^7.0.0` added
- Documentation: `docs/PUSH_SETUP.md` (iOS/Android setup guide)
- Exports: PrismPushNotifications class, PushPermissionState enum

**Key Decisions:**
1. Plugin choice: `@capacitor/push-notifications` (not Firebase)
2. Opt-in design: `pushNotificationsEnabled: false` by default
3. Deferred permission timing: Left to consumers (recommended post-biometric-login)
4. Manual native setup: Documented in PUSH_SETUP.md (cannot automate APNs/Firebase)
5. API alignment: Endpoints per `docs/design/notifications-backend.md`

**Build Status:** ✅ TypeScript 0 errors, `npm run build` passes

**Documentation:**
- `docs/PUSH_SETUP.md` — complete iOS/Android native setup guide
- Decision notes in `.squad/decisions.md`
- Orchestration log: `.squad/orchestration-log/2026-04-03T12:23:47Z-kicks.md`
- Session log: `.squad/log/2026-04-03T12:23:47Z-phase2-phase3-notifications.md`

**Blocker:** ⚠️ Backend endpoints not yet implemented (Blathers Phase 4 prerequisite)
- `/umbraco/prism/push/register` (POST, DELETE)
- `/umbraco/prism/push/subscribe` (POST, DELETE)

**End-to-End Functional:** Not yet (awaiting backend). TypeScript implementation is production-ready.

**Team Dependencies:**
- Blathers (Backend): Implement 4 push endpoints
- Tom Nook (Services): Conditionally scaffold push code in `MobileBundleService.cs`

**Future Enhancements:**
1. Auto-inject permission request into biometric login flow
2. Generate Android notification channel setup code in bundle
3. Interactive CLI setup wizard (`npx prism-setup-push`)
4. Test Push button in tenant modal
5. Optional Firebase Messaging toggle


### 2026-06-21: Android Bootstrap Script Bug Fixes

**Task:** Fixed two bugs in `BuildBootstrapAndroidScript` in `MobileBundleService.cs` that caused `bootstrap-android.sh` to fail on macOS/Java 25 environments.

**Bug 1 — BSD sed INSERT syntax (macOS crash):**
- The generated script used `sed -i.bak '/<application/i\...'` which is GNU sed syntax. BSD sed (macOS) requires a newline after `\i`, not inline text.
- **Fix:** Replaced with `perl -i -pe 's|(<application)|    <uses-permission.../>\n$1|'` which works identically on macOS and Linux. Removed the now-unnecessary `.bak` cleanup line.

**Bug 2 — Gradle 8.11.1 / Java 25 incompatibility:**
- `@capacitor/android@7.0.0` ships Gradle 8.11.1, which only supports up to Java 23. Class file major version 69 (Java 25) causes a fatal Groovy compilation error during `npx cap sync android`.
- **Fix:** Added a Gradle wrapper upgrade step after `npx cap add android` and before `npx cap sync android`. Upgrades `gradle-wrapper.properties` to Gradle 8.14 (supports Java 25). Uses `sed -i.bak 's/.../.../'` substitution (safe on both platforms — only INSERT was problematic).

**Note on doctor-mobile.sh:** Checked `BuildDoctorScript` — no sed usage, no BSD-specific issues.

**Files changed:**
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — `BuildBootstrapAndroidScript` method only
