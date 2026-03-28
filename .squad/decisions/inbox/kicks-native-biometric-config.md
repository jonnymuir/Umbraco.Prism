# Native Biometric Platform Configuration

**Date:** 2025-01-25  
**Author:** Kicks (Mobile Native Specialist)  
**Context:** Issues #20, #21 — iOS and Android biometric platform config in MobileBundleService

## Decision

The `MobileBundleService` now conditionally injects platform-specific biometric configuration into generated mobile app bundles when the `BiometricAuthEnabled` flag is set to true.

### iOS Configuration
- **Info.plist Key:** `NSFaceIDUsageDescription` with usage string
- **Injection Method:** `plutil -insert` command in bootstrap-ios.sh script
- **When:** After `npx cap add ios` but before app build/run
- **Rationale:** FaceID requires explicit usage description in Info.plist for App Store approval; TouchID does not

### Android Configuration
- **Manifest Permission:** `android.permission.USE_BIOMETRIC`
- **Injection Method:** `sed` insertion before `<application>` tag in bootstrap-android.sh script
- **When:** After `npx cap add android` but before app build/run
- **API Level:** Targets API 28+ (BiometricPrompt API); no need for deprecated `USE_FINGERPRINT` permission

### Plugin Dependencies
When `BiometricAuthEnabled` is true, package.json includes:
- `@aparajita/capacitor-biometric-auth@^7.0.0` — biometric authentication prompts
- `@aparajita/capacitor-secure-storage@^7.0.0` — secure Keychain/Keystore access

**Plugin Selection Rationale:** `@aparajita` packages chosen over `@capacitor-community` alternatives for:
- Capacitor 7 compatibility
- Active maintenance
- Superior iOS Keychain and Android Keystore mapping
- Consistent API surface from same author

## Implementation Details

### Bootstrap Script Pattern
Both iOS and Android bootstrap scripts follow this pattern:
1. Check if the platform-specific file exists
2. Check if the required entry is already present (idempotent)
3. If not present, inject using platform-appropriate tool (`plutil` for iOS plist, `sed` for Android XML)
4. Provide clear feedback to developer

This approach ensures the scripts can be run multiple times without duplication and gracefully handle cases where the platform hasn't been added yet.

## Future Considerations

- If the tenant disables biometric auth after a bundle is generated, developers must manually remove the permissions or regenerate the bundle
- The `BiometricAuthEnabled` flag is currently a simple boolean; future enhancements might allow for platform-specific toggles (iOS-only, Android-only)
- No Capacitor config changes needed — plugins auto-register via Capacitor's discovery mechanism

## Testing Notes

The configuration injection happens during the bootstrap script phase, which occurs on the developer's machine after bundle extraction. This means:
- No server-side testing needed for the injection itself
- Testing requires full Capacitor app generation and platform addition
- Verification: check generated Info.plist and AndroidManifest.xml after running bootstrap scripts
