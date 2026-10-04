
## Push Notification Setup

This bundle was generated with **push notifications enabled**, using `@capacitor-firebase/messaging`
(not `@capacitor/push-notifications`) so both iOS and Android hand the server an FCM-compatible token —
the Prism backend sends via `FirebaseAdmin.Messaging`, which is FCM-only.

**Before running the bootstrap scripts**, get your Firebase config files and place them here:

- `resources/GoogleService-Info.plist` (Firebase Console → Project settings → your iOS app)
- `resources/google-services.json` (Firebase Console → Project settings → your Android app)

`bootstrap-ios.sh` then copies `GoogleService-Info.plist` into `ios/App/App/`, registers it as a Copy
Bundle Resources entry, injects `UIBackgroundModes` (remote-notification) into `Info.plist`, writes
`ios/App/App/App.entitlements` with `aps-environment: production`, and patches `AppDelegate.swift` with
the delegate methods the plugin needs to bridge the APNs token into an FCM token. `bootstrap-android.sh`
copies `google-services.json` into `android/app/` — the Firebase Gradle plugin picks it up automatically.

Ask your Prism site administrator for step-by-step Firebase Console instructions if you don't have these
files yet.