# Prism Mobile — Push Notifications Setup

This document provides step-by-step instructions for configuring native push notification support in iOS and Android Capacitor projects.

## Prerequisites

- Mobile bundle generated with `pushNotificationsEnabled: true`
- `@capacitor/push-notifications` plugin installed (included in bundle)
- Xcode 15+ for iOS builds
- Android Studio for Android builds

---

## iOS Setup (APNs)

### 1. Enable Push Notifications Capability

1. Open the iOS project in Xcode:
   ```bash
   npx cap open ios
   ```

2. Select the **App** target in the project navigator.

3. Go to **Signing & Capabilities** tab.

4. Click **+ Capability** and add **Push Notifications**.

### 2. Configure App Entitlements

Open `ios/App/App/App.entitlements` and add the APNs environment key:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <!-- Existing keys (e.g., keychain access groups) -->
    
    <!-- APNs Environment -->
    <key>aps-environment</key>
    <string>development</string>  <!-- Use "production" for App Store builds -->
</dict>
</plist>
```

**Note:** Switch `development` to `production` when building for App Store distribution.

### 3. APNs Authentication (Backend Configuration)

Your Prism backend must be configured to send push notifications via APNs. Two options:

#### Option A: APNs Authentication Key (p8) — Recommended

1. Go to **Apple Developer Account** → **Certificates, Identifiers & Profiles** → **Keys**.
2. Create a new key with **Apple Push Notifications service (APNs)** capability.
3. Download the `.p8` file and note the **Key ID** and **Team ID**.
4. Provide these to your backend team/admin to configure the Prism notification service.

**Advantages:**
- Never expires (no annual renewal)
- One key works for all apps
- Simpler to manage

#### Option B: APNs Certificate (p12) — Legacy

1. Create an App ID with Push Notifications capability.
2. Generate an APNs Certificate (Sandbox or Production) in the Apple Developer portal.
3. Download `.cer`, import to Keychain Access, and export as `.p12`.
4. Provide the `.p12` file to your backend team/admin.

**Note:** Certificates expire annually and must be renewed.

### 4. Test on Device

Push notifications **do not work on iOS Simulator** — you must test on a physical device.

1. Connect your iPhone/iPad.
2. Select your device in Xcode's target dropdown.
3. Build and run (⌘R).
4. After biometric login, you should see a permission prompt for notifications.

---

## Android Setup (FCM)

### 1. Firebase Project Setup

1. Go to [Firebase Console](https://console.firebase.google.com/).
2. Create a new project or select an existing one.
3. Add an Android app to your project:
   - **Android package name**: Must match your `appId` (e.g., `com.prism.portal`)
   - **App nickname**: Optional
   - **Debug signing certificate SHA-1**: Optional (needed for advanced features)

4. Download `google-services.json`.

### 2. Place `google-services.json` in Android Project

Copy the downloaded `google-services.json` file to:

```
android/app/google-services.json
```

**Directory structure:**
```
android/
  app/
    google-services.json  ← Place here
    src/
    build.gradle
  build.gradle
  capacitor.config.json
```

### 3. Verify Gradle Configuration

The Capacitor CLI should have already applied the `google-services` plugin. Verify in `android/app/build.gradle`:

```gradle
// Bottom of the file
apply plugin: 'com.google.gms.google-services'
```

And in `android/build.gradle` (project-level):

```gradle
buildscript {
    dependencies {
        classpath 'com.google.gms:google-services:4.4.0'  // or latest version
    }
}
```

**If missing**, add them manually.

### 4. Sync Capacitor

After placing `google-services.json`:

```bash
npx cap sync android
```

### 5. Test on Device or Emulator

Android allows push notifications on emulators (if Google Play Services are installed).

1. Open Android Studio:
   ```bash
   npx cap open android
   ```

2. Select a device/emulator with Google Play Services.

3. Run the app.

4. After biometric login, push notification permission is automatically granted (no prompt on Android 12 and below; Android 13+ shows a prompt).

---

## Backend Configuration

Ensure your Prism backend is configured with:

### iOS Backend Requirements

- **APNs Authentication Key (p8)** or **APNs Certificate (p12)**
- **Team ID** (from Apple Developer account)
- **Key ID** (if using p8)
- **Bundle ID** (must match your iOS app's bundle identifier)

### Android Backend Requirements

- **Firebase Server Key** or **Service Account JSON** (from Firebase Console → Project Settings → Cloud Messaging)
- This key is used by the Prism backend to send FCM messages.

**Configuration location:**  
Consult your Prism backend documentation or admin for where to configure these values (typically environment variables or Key Vault secrets).

---

## Troubleshooting

### iOS: "No valid APNs token" error

- Ensure **aps-environment** is set in `App.entitlements`.
- Verify Push Notifications capability is enabled in Xcode.
- Must test on a **physical device** (simulator not supported).
- Check that the backend has valid APNs credentials (p8 or p12).

### Android: "Token registration failed"

- Verify `google-services.json` is in `android/app/`.
- Ensure the **package name** in Firebase Console matches your `appId`.
- Run `npx cap sync android` after adding the file.
- Check that Google Play Services is installed on the device/emulator.

### Push notifications not appearing

- Check that permissions were granted (iOS prompts user, Android auto-grants on Android 12 and below).
- Ensure the device token was successfully registered with the backend (check app logs).
- Verify the backend is sending notifications to the correct FCM/APNs endpoint.
- For iOS, check that the APNs environment (`development` vs `production`) matches your build type.

### Testing permission prompt

To re-trigger the permission prompt during development:

**iOS:**
```bash
# Reset app permissions (requires device to be connected)
xcrun simctl privacy booted reset all <bundle-id>
```

**Android:**
```bash
# Clear app data
adb shell pm clear <package-name>
```

---

## Next Steps

Once native configuration is complete:

1. **Test token registration** — Check that device tokens appear in your Prism backend database after login.
2. **Test notification delivery** — Use the Prism Notification Service API to send a test notification.
3. **Subscribe to genres** — Use `PrismPushNotifications.subscribeToGenre()` to opt users into content categories.
4. **Handle notification taps** — Add listeners via `PrismPushNotifications.addNotificationActionListener()` to navigate to relevant content when users tap notifications.

For API usage examples, see `src/backoffice/push-notifications.ts` in the Prism Client codebase.
