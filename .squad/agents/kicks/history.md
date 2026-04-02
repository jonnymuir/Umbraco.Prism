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
