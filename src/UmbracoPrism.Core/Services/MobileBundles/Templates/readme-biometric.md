
## Biometric Login Setup

This bundle was generated with **biometric authentication enabled**. The bootstrap scripts automatically inject
required platform entitlements, but you should verify the following prerequisites.

### iOS

- The `NSFaceIDUsageDescription` key is injected into `Info.plist` by `bootstrap-ios.sh`.
- Face ID / Touch ID must be enrolled on the device or simulator.
- **Simulator note:** `BiometricAuth.checkBiometry()` returns `isAvailable: false` on the iOS Simulator because
  it has no biometric hardware. Test biometric flows on a physical device or use the Simulator's
  *Features → Face ID → Enrolled* menu to enable a simulated match.

### Android

- The `USE_BIOMETRIC` permission is injected into `AndroidManifest.xml` by `bootstrap-android.sh`.
- A fingerprint or biometric credential must be enrolled on the device/emulator.
- **Emulator note:** enroll a simulated fingerprint via `adb emu finger touch 1`, then authenticate
  with the same command when prompted.

### Plugin packages

The following Capacitor plugins are included in `package.json`:

- `@aparajita/capacitor-biometric-auth` — biometric prompt and availability checks.
- `@aparajita/capacitor-secure-storage` — hardware-backed secure storage for tokens.

Both plugins auto-register via Capacitor's plugin discovery; no `capacitor.config.ts` changes are needed.