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
