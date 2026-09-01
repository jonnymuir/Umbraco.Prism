# Prism Mobile: Push Notifications Design

> **Internal Design Document:** This document is for contributors and maintainers. For setup instructions, see [../PUSH_SETUP.md](../PUSH_SETUP.md).

**Author:** Kicks (Mobile Native Specialist)  
**Date:** 2026-07-14  
**Status:** Design Proposal  
**Context:** Umbraco.Prism Capacitor mobile app needs push notification support

---

## Executive Summary

This document defines the mobile-side architecture for push notifications in Prism Mobile. The design recommends **`@capacitor/push-notifications`** as the primary plugin, provides clear iOS/Android setup requirements, defines token lifecycle management, and outlines a zero-friction consumer setup path.

**Top 3 Recommendations:**

1. **Use `@capacitor/push-notifications` (official Capacitor plugin)**, lighter, APNs-native for iOS, sufficient for standard notification needs. Reserve `@capacitor-firebase/messaging` only if consumers need Firebase Analytics or data-only messages.

2. **Request permission AFTER first biometric login**: not on app launch. This aligns with Apple's HIG (provide context before requesting), reduces cold-start permission prompts, and ensures notifications are tied to an authenticated user.

3. **Make push notifications a consumer opt-in, not a Prism default**: add a `PushNotificationsEnabled` boolean to `PrismMobileBundleRequest`. Generate push scaffolding in the bundle only if enabled. This keeps the base bundle lean and allows tenants to ship without push if they don't need it.

---

## 1. Capacitor Push Notification Options

### Comparison Table

| Aspect | `@capacitor/push-notifications` | `@capacitor-firebase/messaging` |
|--------|--------------------------------|--------------------------------|
| **Plugin Maintainer** | Ionic (official) | Capacitor Community |
| **iOS Delivery** | APNs (native) | APNs via Firebase proxy |
| **Android Delivery** | FCM (Google Play Services) | FCM (Firebase SDK) |
| **Bundle Size Impact** | +5-10MB | +20-50MB (full Firebase SDK) |
| **FCM Setup** | Requires `google-services.json` + native FCM libs | Same + full Firebase SDK |
| **APNs Setup** | APNs p8 key or p12 cert (direct) | APNs via Firebase Console proxy |
| **Data-Only Messages** | Limited (requires payload workaround) | ✅ Full support via `data` key |
| **Silent Notifications** | Basic (background mode required) | Advanced (FCM data messages) |
| **Rich Media** | Requires native Notification Service Extension | Same + Firebase CDN image support |
| **Firebase Analytics** | ❌ | ✅ Auto-tracked impressions/opens |
| **Firebase Console** | Not needed | Required for advanced targeting |
| **Topic Subscriptions** | Manual (custom backend) | ✅ Built-in Firebase Topics API |
| **Complexity** | Medium | High |
| **Best For** | Standard notifications, APNs-first, smaller bundles | Firebase-first backends, analytics, data sync |

### Recommendation: `@capacitor/push-notifications`

**Rationale:**

1. **Smaller footprint**: Prism Mobile is already bundling biometric plugins. Adding 20-50MB for Firebase SDK is excessive if the backend only needs basic notification delivery.
2. **APNs-native on iOS**: Direct APNs integration is simpler for iOS apps; no Firebase proxy layer.
3. **Standard notification needs**: Most Prism tenants want "send a notification when X happens", not advanced Firebase Analytics or topic-based A/B testing.
4. **Capacitor-first**: Official Ionic plugin with stronger long-term maintenance guarantees.

**When to use `@capacitor-firebase/messaging` instead:**

- Backend is already Firebase-first (Firestore, Remote Config, etc.)
- Need data-only messages for background sync (e.g., silent cache updates)
- Require Firebase Analytics for notification engagement tracking
- Want Firebase Console UI for targeting/segmentation without custom backend logic

**Decision:** Default to `@capacitor/push-notifications`. Document Firebase alternative in README for advanced consumers.

---

## 2. iOS vs Android Platform Requirements

### iOS (APNs)

#### APNs Certificate/Key Setup

**Recommended: APNs Authentication Key (p8 token)**

1. Apple Developer Account → Certificates, Identifiers & Profiles → Keys
2. Create new key with **Apple Push Notifications service (APNs)** capability
3. Download `.p8` file (save securely, cannot re-download)
4. Note **Key ID** and **Team ID**
5. Backend uses p8 key + Key ID + Team ID to sign push requests

**Alternative: APNs Certificate (p12)**

1. Create App ID with Push Notifications capability
2. Generate APNs Certificate (Sandbox or Production)
3. Download `.cer`, import to Keychain, export as `.p12`
4. Backend uses p12 cert to authenticate with APNs

**Why p8 is preferred:**
- Never expires (p12 certs expire annually)
- One key for all apps
- Simpler renewal process

#### iOS App Entitlements

**Consumer must add to `ios/App/App/App.entitlements`:**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <!-- APNs environment (development or production) -->
    <key>aps-environment</key>
    <string>production</string>
    
    <!-- Background notification handling -->
    <key>UIBackgroundModes</key>
    <array>
        <string>remote-notification</string>
    </array>
</dict>
</plist>
```

**MobileBundleService Auto-Injection:**

When `PushNotificationsEnabled` is true, `bootstrap-ios.sh` should:

1. Check if `ios/App/App/App.entitlements` exists
2. If missing, create from template: `resources/ios-entitlements-push.xml`
3. If exists, warn user to manually add `aps-environment` and `UIBackgroundModes` if missing

**Xcode Capabilities:**

Consumer must manually enable in Xcode:
1. Open `ios/App/App.xcworkspace`
2. Select App target → Signing & Capabilities
3. Add **Push Notifications** capability
4. Add **Background Modes** capability → check **Remote notifications**

*Why manual?* Capacitor doesn't auto-sync entitlements. Xcode project must be opened at least once.

#### iOS Info.plist Changes

**None required for basic push.** iOS handles permission UI automatically via `UNUserNotificationCenter`.

*Optional:* Add custom permission rationale if using rich notifications (Notification Service Extension).

---

### Android (FCM)

#### FCM Setup

1. **Firebase Console → Add Android App**
   - Package name: matches `appId` in `capacitor.config.ts` (e.g., `com.prism.yourapp`)
   - Download `google-services.json`

2. **Place `google-services.json` in:**
   ```
   android/app/google-services.json
   ```

3. **Consumer must run after download:**
   ```bash
   npx cap sync android
   ```

**MobileBundleService Generated Guidance:**

- `README.md` must include step-by-step Firebase setup
- `AGENT_PROMPT.md` must include Firebase Console instructions for AI assistants helping consumers
- `resources/android-firebase-setup.md` provides detailed walkthrough with screenshots (optional)

#### Android Permissions

**Required in `AndroidManifest.xml`:**

```xml
<!-- FCM requires internet permission (usually auto-added by Capacitor) -->
<uses-permission android:name="android.permission.INTERNET" />

<!-- Android 13+ (API 33+) requires explicit notification permission -->
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />

<!-- Wake device for notifications (optional, for high-priority notifications) -->
<uses-permission android:name="android.permission.WAKE_LOCK" />
```

**Auto-Injection Strategy:**

`bootstrap-android.sh` should:

1. Check Android API level in `build.gradle` (`targetSdkVersion`)
2. If `>= 33`, inject `POST_NOTIFICATIONS` permission if missing
3. Use `perl` regex (consistent with biometric auth injection pattern)

#### Android Notification Channels (API 26+)

**Default channel created at runtime:**

```typescript
// In app startup (e.g., App.tsx or main.ts)
import { PushNotifications } from '@capacitor/push-notifications';

async function setupAndroidNotificationChannel() {
  if ((await Device.getInfo()).platform === 'android') {
    await PushNotifications.createChannel({
      id: 'prism-default',
      name: 'Prism Notifications',
      description: 'Default notification channel',
      importance: 5, // IMPORTANCE_HIGH
      sound: 'default',
      vibration: true,
      visibility: 1 // VISIBILITY_PUBLIC
    });
  }
}
```

**Consumer configuration:**

- Prism should generate this channel setup in the bundle's `www/index.html` bootstrap if `PushNotificationsEnabled` is true
- Allow consumers to customize channel name/settings via `PrismMobileBundleRequest.NotificationChannelName` (optional)

---

## 3. Device Token Lifecycle

### When to Request Permission

**Recommended Flow: Post-Login Permission Request**

```
App Launch
    |
    v
[Check for biometric credential]
    |
    |-- NO CREDENTIAL --> Fall back to Entra OIDC login
    |                         |
    |                         v
    |                    [User completes OIDC]
    |                         |
    |                         v
    |                    [Biometric enrollment prompt] (existing)
    |                         |
    |                         v
    |-- YES CREDENTIAL --> [Biometric unlock succeeds]
    |
    v
[Check if push permission granted]
    |
    |-- GRANTED --> Skip prompt, ensure token is registered
    |
    |-- DENIED --> Skip (user previously denied)
    |
    |-- NOT DETERMINED --> Show push permission prompt
                               |
                               v
                          [User taps "Enable"]
                               |
                               v
                          [Call PushNotifications.requestPermissions()]
                               |
                               v
                          [On success: register token with backend]
```

**Why post-login?**

1. **Contextual permission**: Apple HIG strongly discourages permission prompts on cold app launch. Requesting after successful login provides clear context: "Get notified about your account activity."
2. **User is authenticated**: Push token can immediately be associated with a `PrismMemberCookie` session; no orphaned tokens.
3. **Reduces friction**: New users see one permission at a time (biometric first, then push), not a wall of prompts.
4. **Consistent with biometric flow**: Biometric enrollment already happens post-OIDC. Push follows the same timing.

**Storage of permission state:**

```typescript
// After permission result
await Preferences.set({
  key: 'prism-push-permission-state',
  value: result.receive // 'granted' | 'denied' | 'prompt'
});
```

On subsequent launches, check this state before showing UI.

---

### Token Registration with Backend

**API Contract:**

```
POST /umbraco/prism/mobile/push/register
Authorization: PrismMemberCookie (required)
Content-Type: application/json

{
  "deviceToken": "abc123...",
  "platform": "ios" | "android",
  "deviceId": "unique-device-id",
  "tenantHostname": "portal.example.com"
}

Response:
200 OK
{
  "registered": true,
  "expiresAt": "2027-07-14T12:00:00Z"
}
```

**Backend responsibilities:**

1. Extract user OID from `PrismMemberCookie` claims
2. Store `PrismPushTokenRecord` in database:
   - `DeviceToken` (hashed, never plain text)
   - `Platform` (ios/android)
   - `DeviceId` (unique device identifier)
   - `UserOid` (Entra object ID)
   - `TenantId` (from cookie context)
   - `CreatedAt`, `UpdatedAt`, `LastSeenAt`
   - `Revoked` (boolean, for manual revocation)

3. Return success response

**Client-side implementation:**

```typescript
// In www/index.html or biometric-bridge.ts
async function registerPushToken(token: string) {
  const deviceInfo = await window.Capacitor.nativePromise('Device', 'getInfo', {});
  const deviceId = await window.Capacitor.nativePromise('Device', 'getId', {});
  
  const response = await fetch('/umbraco/prism/mobile/push/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include', // Send PrismMemberCookie
    body: JSON.stringify({
      deviceToken: token,
      platform: deviceInfo.platform,
      deviceId: deviceId.identifier,
      tenantHostname: window.location.hostname
    })
  });
  
  if (response.ok) {
    console.log('Push token registered');
    await window.Capacitor.nativePromise(
      'Preferences',
      'set',
      { key: 'prism-push-token-registered', value: 'true' }
    );
  }
}
```

---

### Token Refresh (FCM Token Rotation)

**Android FCM tokens rotate periodically** (typically every 60 days, or on app reinstall).

**Handling rotation:**

```typescript
// Listen for token updates
PushNotifications.addListener('registration', async (token) => {
  const storedToken = await getStoredToken();
  
  if (storedToken !== token.value) {
    console.log('Token changed, updating backend');
    
    // Update backend
    await registerPushToken(token.value);
    
    // Update local storage
    await Preferences.set({
      key: 'prism-push-token',
      value: token.value
    });
  }
});

async function getStoredToken(): Promise<string> {
  const { value } = await Preferences.get({ key: 'prism-push-token' });
  return value || '';
}
```

**Backend token update strategy:**

- `POST /umbraco/prism/mobile/push/register` should be **idempotent**
- If a record exists for `DeviceId + UserOid + TenantId`, update `DeviceToken` and `UpdatedAt`
- If token differs, invalidate old token (set `Revoked = true` on old record)
- Insert new token record

**Token staleness detection:**

Backend should track `LastSeenAt` timestamp. If a token hasn't been updated in >90 days, mark as stale and exclude from push targeting.

---

### Logout / Revocation Flow

**User-initiated logout:**

```typescript
async function handleLogout() {
  // 1. Revoke push token on backend
  await fetch('/umbraco/prism/mobile/push/revoke', {
    method: 'DELETE',
    credentials: 'include'
  });
  
  // 2. Clear local token
  await Preferences.remove({ key: 'prism-push-token' });
  await Preferences.remove({ key: 'prism-push-token-registered' });
  
  // 3. Delete biometric credential
  await deleteBiometricCredential();
  
  // 4. Clear session cookie
  // (handled by backend logout endpoint)
}
```

**Backend revocation:**

```
DELETE /umbraco/prism/mobile/push/revoke
Authorization: PrismMemberCookie (required)

Response: 204 No Content
```

Backend logic:
1. Extract `UserOid` from cookie claims
2. Find all `PrismPushTokenRecord` for `UserOid + TenantId`
3. Set `Revoked = true` for matching records
4. Return success

**Admin-side revocation (optional):**

Tenant admins should be able to revoke all push tokens for a specific user from the backoffice (e.g., on account suspension).

---

## 4. Notification Handling in the App

### Foreground Notifications

**Behavior:**

When a notification arrives while the app is open (WebView is active), the system **does not** display the notification banner by default. The app receives the payload via the `pushNotificationReceived` listener.

**Recommended UX:**

Display a non-intrusive in-app banner (e.g., Snackbar/Toast) at the top of the WebView:

```typescript
PushNotifications.addListener('pushNotificationReceived', (notification) => {
  // Inject banner into WebView
  showInAppNotificationBanner({
    title: notification.title,
    body: notification.body,
    onTap: () => {
      // Navigate to notification target
      navigateToPage(notification.data?.page);
    }
  });
  
  // Track analytics
  trackNotificationReceived(notification.id);
});

function showInAppNotificationBanner(notification: any) {
  // Option 1: Inject HTML into WebView
  const banner = document.createElement('div');
  banner.className = 'prism-notification-banner';
  banner.innerHTML = `
    <strong>${notification.title}</strong>
    <p>${notification.body}</p>
  `;
  banner.addEventListener('click', notification.onTap);
  document.body.appendChild(banner);
  
  setTimeout(() => banner.remove(), 5000);
}
```

**Styling:**

`www/mobile-overrides.css` should include:

```css
.prism-notification-banner {
  position: fixed;
  top: env(safe-area-inset-top, 0px);
  left: 0;
  right: 0;
  background: var(--prism-notification-bg, #1e293b);
  color: var(--prism-notification-text, #f8fafc);
  padding: 12px 16px;
  box-shadow: 0 4px 8px rgba(0,0,0,0.15);
  z-index: 9999;
  animation: slideDown 0.3s ease-out;
}

@keyframes slideDown {
  from { transform: translateY(-100%); }
  to { transform: translateY(0); }
}
```

**Alternative: Let iOS/Android handle foreground display**

If you want the system notification banner even when the app is open:

```typescript
// iOS only: Show notification in foreground
// Requires native code in AppDelegate.swift:
/*
func userNotificationCenter(_ center: UNUserNotificationCenter,
                          willPresent notification: UNNotification,
                          withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
    completionHandler([.banner, .sound, .badge])
}
*/
```

This requires consumer to add native code, so **not recommended for generated bundles**. Stick with in-app banner pattern.

---

### Background/Killed App Notifications

**Behavior:**

When the app is in the background or killed, the OS displays the notification banner. Tapping the notification launches the app.

**Handling the tap:**

```typescript
PushNotifications.addListener('pushNotificationActionPerformed', (notification) => {
  const data = notification.notification.data;
  
  // Navigate to deep link target
  if (data?.page) {
    navigateToPage(data.page, data.params);
  }
  
  // Track analytics
  trackNotificationOpened(notification.notification.id);
});
```

**Cold start handling:**

If the app was killed and the user taps a notification, the app launches. The `pushNotificationActionPerformed` listener fires **after** Capacitor initializes.

**Startup sequence:**

```
1. App launches (cold start)
2. Capacitor initializes
3. www/index.html loads
4. Biometric auth attempt (if credential exists)
5. pushNotificationActionPerformed fires
6. App navigates to notification target
```

**Edge case:** If the user taps a notification but biometric auth fails, the app should:
1. Fall back to OIDC login
2. Store the notification data temporarily (`Preferences.set('pending-notification', JSON.stringify(data))`)
3. After successful login, check for `pending-notification` and navigate

---

### Deep Linking

**Payload format:**

```json
{
  "notification": {
    "title": "Order Ready",
    "body": "Your order #12345 is ready for pickup"
  },
  "data": {
    "page": "orders",
    "id": "12345",
    "params": "{\"returnTo\":\"home\"}"
  }
}
```

**Client-side navigation:**

```typescript
function navigateToPage(page: string, params?: any) {
  const baseUrl = window.location.origin;
  
  switch (page) {
    case 'orders':
      window.location.href = `${baseUrl}/orders/${params?.id || ''}`;
      break;
    case 'profile':
      window.location.href = `${baseUrl}/profile`;
      break;
    case 'notifications':
      window.location.href = `${baseUrl}/notifications`;
      break;
    default:
      window.location.href = baseUrl;
  }
}
```

**Alternative: Use Capacitor App URL scheme**

If the Umbraco site supports deep links (e.g., `prismapp://orders/12345`):

```typescript
import { App } from '@capacitor/app';

App.addListener('appUrlOpen', (event) => {
  const url = new URL(event.url);
  
  if (url.protocol === 'prismapp:') {
    const path = url.pathname; // "/orders/12345"
    window.location.href = `${window.location.origin}${path}`;
  }
});
```

Backend can then send:

```json
{
  "data": {
    "deeplink": "prismapp://orders/12345"
  }
}
```

**Recommendation:** Start with simple `page + id` pattern. Add URL scheme only if needed for cross-app linking.

---

### Silent/Data Notifications

**Use case:**

Background data sync (e.g., "New messages available, refresh cache") without showing a user-facing notification.

**Implementation:**

**iOS:**

Requires `content-available: 1` in payload + `remote-notification` background mode (already added in entitlements above).

**Payload:**

```json
{
  "aps": {
    "content-available": 1,
    "sound": ""
  },
  "data": {
    "type": "sync",
    "syncToken": "abc123"
  }
}
```

**Handling:**

```typescript
PushNotifications.addListener('pushNotificationReceived', async (notification) => {
  const data = notification.data;
  
  if (data.type === 'sync') {
    // Perform background sync
    await syncDataWithServer(data.syncToken);
    
    // No UI shown to user
  }
});
```

**Android:**

Use FCM data-only message (no `notification` key):

```json
{
  "data": {
    "type": "sync",
    "syncToken": "abc123"
  }
}
```

The app receives this via `pushNotificationReceived` even if in background (requires `remote-notification` background mode).

**Limitations:**

- iOS limits background execution time (~30 seconds)
- Android may throttle background work (Doze mode)
- Not suitable for long-running tasks (use WorkManager on Android, BGTaskScheduler on iOS via native code)

**Recommendation:**

Silent notifications are **advanced** functionality. Do not include in v1 Prism Mobile push implementation. Document as "Future Enhancement" for consumers who need it.

---

## 5. Capacitor Plugin Architecture

### Should This Be a Prism Plugin or Consumer Configuration?

**Analysis:**

| Approach | Pros | Cons |
|----------|------|------|
| **New Prism plugin** (`@umbracoprism/capacitor-push`) | - Encapsulates Prism-specific logic<br>- Can bundle token registration API calls<br>- Easier version control | - Adds dependency<br>- Consumers must install + sync<br>- Duplicates @capacitor/push-notifications |
| **Consumer configuration** (scaffolding in generated bundle) | - No extra dependency<br>- Consumer owns the code<br>- Easier to customize | - More consumer friction (must edit generated code)<br>- Harder to update/patch |
| **Hybrid:** Prism Web Component + Official Plugin | - Web component handles UI/registration logic<br>- Official plugin handles native bridging<br>- Prism can update component without native changes | - Adds complexity<br>- Web component must load in WebView | 

**Recommendation: Consumer Configuration (Scaffolding)**

**Rationale:**

1. **Minimal abstraction**: Push notifications are relatively simple. The official `@capacitor/push-notifications` plugin already handles 90% of the work. Wrapping it in a Prism plugin adds little value.

2. **Consumer ownership**: Many consumers will want to customize notification handling (custom banners, analytics integration, deep linking logic). Giving them the scaffolding code directly makes customization trivial.

3. **No version lock-in**: If Capacitor updates the push-notifications API, consumers can update their `package.json` independently without waiting for a Prism plugin release.

4. **Prism backend integration is backend-side**: The Prism-specific logic (token registration, revocation endpoints) lives in the backend (`BiometricController` or new `PushNotificationController`). The client just calls standard REST endpoints.

**What Prism provides in the bundle:**

When `PushNotificationsEnabled` is true in `PrismMobileBundleRequest`:

1. **`package.json`**, includes `@capacitor/push-notifications: ^8.0.0`
2. **`www/index.html`**, includes push token registration logic (similar to biometric enrollment flow)
3. **`README.md`**, step-by-step setup guide (APNs key, FCM setup, entitlements)
4. **`AGENT_PROMPT.md`**, AI-friendly setup instructions
5. **`resources/ios-entitlements-push.xml`**, template entitlements file
6. **`resources/android-firebase-setup.md`**, Firebase Console walkthrough
7. **`scripts/bootstrap-ios.sh`**, auto-inject entitlements (if missing)
8. **`scripts/bootstrap-android.sh`**, auto-inject `POST_NOTIFICATIONS` permission

**What the consumer must do:**

1. Generate APNs p8 key (iOS) and FCM `google-services.json` (Android)
2. Run `npm install && npx cap sync`
3. Open Xcode and enable Push Notifications capability (manual step)
4. Deploy to device and test

**Estimated setup time:** 20-30 minutes (vs. 2-3 hours if starting from scratch).

---

## 6. Permission Strategy

### iOS Permission Best Practices

**iOS requires explicit permission prompt** via `UNUserNotificationCenter`. The OS shows a system dialog with "Allow" / "Don't Allow" buttons.

**Best Practice Timing:**

1. **Do NOT request on first app launch**: Apple HIG discourages "permission walls" before the user understands the app's value.
2. **Do request after user action**: e.g., after successful login, or when user taps "Enable Notifications" in settings.
3. **Provide context**: Show a **pre-permission explainer** before calling `requestPermissions()`.

**Pre-Permission Explainer Pattern:**

```typescript
async function promptForPushPermission() {
  // 1. Check if already determined
  const currentPermission = await checkNotificationPermission();
  
  if (currentPermission === 'granted' || currentPermission === 'denied') {
    return; // Don't re-prompt
  }
  
  // 2. Show custom UI explaining benefits
  const userWantsNotifications = await showNotificationExplainer({
    title: "Stay Updated",
    message: "Get notified about important account activity and updates.",
    benefits: [
      "Order status updates",
      "Account security alerts",
      "Exclusive offers"
    ],
    primaryButton: "Enable Notifications",
    secondaryButton: "Not Now"
  });
  
  // 3. Only call system prompt if user taps "Enable"
  if (userWantsNotifications) {
    const result = await PushNotifications.requestPermissions();
    
    if (result.receive === 'granted') {
      await PushNotifications.register();
    }
  }
}
```

**Storage of user choice:**

```typescript
await Preferences.set({
  key: 'prism-push-explainer-shown',
  value: 'true'
});
```

Check this key on subsequent launches. If user tapped "Not Now", **wait 7-14 days** before showing again (avoid nagging).

---

### Android 13+ Notification Permission

**Android 13 (API 33+) requires runtime permission** for `POST_NOTIFICATIONS`.

**Differences from iOS:**

- Android shows a system dialog (like iOS) but only on **Android 13+**
- Android <13 grants notification permission by default (no prompt needed)
- Users can revoke permission in Settings at any time

**Handling Android <13:**

```typescript
import { Device } from '@capacitor/device';

async function requestPushPermission() {
  const deviceInfo = await Device.getInfo();
  
  if (deviceInfo.platform === 'android') {
    const androidVersion = parseInt(deviceInfo.osVersion || '0', 10);
    
    if (androidVersion >= 13) {
      // Android 13+ requires explicit permission
      const result = await PushNotifications.requestPermissions();
      
      if (result.receive !== 'granted') {
        handlePermissionDenied();
        return;
      }
    } else {
      // Android <13 auto-grants permission
      // No permission prompt needed
    }
  } else {
    // iOS
    const result = await PushNotifications.requestPermissions();
    
    if (result.receive !== 'granted') {
      handlePermissionDenied();
      return;
    }
  }
  
  // Register for push
  await PushNotifications.register();
}
```

**Manifest Declaration:**

```xml
<!-- Android 13+ (API 33+) -->
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
```

This permission is **not auto-granted**. The app must call `requestPermissions()` at runtime.

---

### Permission Denied Gracefully

**Scenario:** User taps "Don't Allow" on the permission prompt.

**Handling:**

1. **Do NOT block app functionality**: Prism Mobile must remain fully functional without push notifications.
2. **Store denial state**: Avoid re-prompting immediately.
3. **Provide settings deep link**: Show UI with "Open Settings" button if user changes their mind.

```typescript
async function handlePermissionDenied() {
  // 1. Store denial
  await Preferences.set({
    key: 'prism-push-permission-denied-at',
    value: new Date().toISOString()
  });
  
  // 2. Log analytics
  analytics.track('push_permission_denied');
  
  // 3. Show educational UI (optional)
  showNotificationDeniedBanner({
    message: "Notifications are disabled. Enable them in Settings to receive updates.",
    action: "Open Settings",
    onAction: openAppSettings
  });
}

async function openAppSettings() {
  const { App } = await import('@capacitor/app');
  
  // iOS: Opens Settings > App > Notifications
  // Android: Opens App Settings > Notifications
  await App.openUrl({ url: 'app-settings:' });
}
```

**Re-Prompt Strategy:**

- Do NOT re-prompt until user explicitly taps "Enable Notifications" in app settings
- OR wait 14+ days and show pre-permission explainer again (with "Don't ask again" option)

**Apple App Store Review Consideration:**

Apps that repeatedly nag users for permissions risk rejection. Always provide clear value and respect "Not Now" decisions.

---

## 7. Consumer Setup Guide (5-10 Steps)

### Quick Start for Prism Mobile Push Notifications

**Prerequisites:**
- Prism Mobile bundle generated with `PushNotificationsEnabled: true`
- Apple Developer Account (iOS)
- Google Firebase Project (Android)

---

### Step 1: Generate APNs Key (iOS)

1. Log in to [Apple Developer Console](https://developer.apple.com/account/)
2. Navigate to **Certificates, Identifiers & Profiles** → **Keys**
3. Click **+** to create a new key
4. Name: "Prism Push Notifications"
5. Check **Apple Push Notifications service (APNs)**
6. Click **Continue** → **Register**
7. Download `.p8` file (save securely, cannot re-download)
8. **Note the Key ID and Team ID** (needed for backend)

**Store these securely:**
- `AuthKey_ABC123.p8` (the key file)
- Key ID: `ABC123`
- Team ID: `DEF456`

**Backend configuration:**

Add to backend environment variables or secrets:

```bash
APNS_KEY_ID=ABC123
APNS_TEAM_ID=DEF456
APNS_KEY_PATH=/path/to/AuthKey_ABC123.p8
```

---

### Step 2: Setup Firebase Cloud Messaging (Android)

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project (or use existing)
3. Click **Add app** → **Android**
4. **Android package name:** Must match your `appId` in `capacitor.config.ts` (e.g., `com.prism.myapp`)
5. Download `google-services.json`
6. Place `google-services.json` in:
   ```
   android/app/google-services.json
   ```

**Backend configuration:**

For FCM v1 API (recommended), you need a service account key:

1. Firebase Console → Project Settings → Service Accounts
2. Click **Generate new private key**
3. Download JSON file
4. Store securely on backend server

```bash
FCM_SERVICE_ACCOUNT_PATH=/path/to/service-account-key.json
```

---

### Step 3: Install Dependencies

```bash
npm install
npx cap sync
```

This installs `@capacitor/push-notifications` and syncs native projects.

---

### Step 4: Configure iOS Entitlements

**Option A: Use Auto-Generated Template (Recommended)**

```bash
bash scripts/bootstrap-ios.sh
```

This script:
- Creates `ios/App/App/App.entitlements` if missing
- Adds `aps-environment` and `UIBackgroundModes` if missing
- Warns if manual edits are needed

**Option B: Manual Setup**

1. Open `ios/App/App.xcworkspace` in Xcode
2. Select **App** target
3. Go to **Signing & Capabilities** tab
4. Click **+ Capability**
5. Add **Push Notifications**
6. Add **Background Modes** → check **Remote notifications**

Xcode auto-generates `App.entitlements`. Verify it contains:

```xml
<key>aps-environment</key>
<string>production</string>
<key>UIBackgroundModes</key>
<array>
    <string>remote-notification</string>
</array>
```

---

### Step 5: Configure Android Permissions

**Option A: Use Auto-Generated Script**

```bash
bash scripts/bootstrap-android.sh
```

This script:
- Injects `POST_NOTIFICATIONS` permission if `targetSdkVersion >= 33`
- Adds `INTERNET` and `WAKE_LOCK` permissions if missing

**Option B: Manual Setup**

Edit `android/app/src/main/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
<uses-permission android:name="android.permission.WAKE_LOCK" />
```

---

### Step 6: Test Permission Request (iOS Device Required)

**iOS Simulator does NOT support push notifications.** You must use a physical device.

1. Build and run on device:
   ```bash
   npx cap run ios --target="YourDeviceName"
   ```

2. Sign in to the app (triggers permission prompt after biometric enrollment)

3. Verify:
   - Permission dialog appears
   - Tapping "Allow" registers device token
   - Check browser console for: `"Push registration success, token: ..."`

**Troubleshooting:**

- **"No APNs certificate"** → Verify entitlements and Xcode capability setup
- **"Permission denied"** → Check `aps-environment` matches backend (production vs development)
- **"No token received"** → Check device has internet connection and APNs is reachable

---

### Step 7: Test Permission Request (Android)

**Android Emulator supports push notifications** (API 33+).

1. Run emulator:
   ```bash
   npx cap run android
   ```

2. Sign in to the app

3. Verify:
   - Permission dialog appears (Android 13+) or auto-granted (Android <13)
   - Device token appears in console

**Troubleshooting:**

- **"google-services.json not found"** → Verify file is in `android/app/` and `npx cap sync` was run
- **"FCM token is null"** → Check emulator has Google Play Services (use Google Play system image, not AOSP)
- **"Permission denied"** → Check `POST_NOTIFICATIONS` in manifest

---

### Step 8: Test Backend Token Registration

1. Open browser DevTools → Network tab

2. Sign in to app (triggers permission request)

3. After granting permission, verify:
   - `POST /umbraco/prism/mobile/push/register` request sent
   - Response: `200 OK { "registered": true, ... }`
   - Token stored in backend database (`PrismPushTokenRecord`)

**SQL check (dev environment):**

```sql
SELECT * FROM prismPushTokens
WHERE UserOid = '<your-entra-oid>';
```

---

### Step 9: Send Test Notification

**Backend test endpoint (dev only):**

```http
POST /umbraco/prism/mobile/push/test
Authorization: PrismMemberCookie
Content-Type: application/json

{
  "title": "Test Notification",
  "body": "This is a test push notification",
  "data": {
    "page": "home"
  }
}
```

**Expected behavior:**

- **App in foreground:** In-app banner appears (custom UI)
- **App in background:** System notification banner appears
- **Tap notification:** App opens and navigates to `data.page`

---

### Step 10: Production Deployment

**iOS:**

1. Change `aps-environment` in entitlements from `development` to `production`
2. Upload to App Store Connect
3. Verify APNs key is registered for Production environment

**Android:**

1. Ensure `google-services.json` matches production Firebase project
2. Build signed APK/AAB
3. Upload to Google Play Console

**Backend:**

1. Update APNs key environment variables to use production key
2. Update FCM service account key to production Firebase project
3. Ensure `/umbraco/prism/mobile/push/register` endpoint is accessible from production tenant hostnames

---

### Estimated Setup Time

- **iOS setup:** 15-20 minutes (APNs key + Xcode config)
- **Android setup:** 10-15 minutes (Firebase + manifest)
- **Testing:** 10-15 minutes (device testing + backend verification)
- **Total:** ~40-50 minutes (for first-time setup)

**Repeat setup for new app:** ~15 minutes (reuse APNs key, new Firebase project for new app)

---

## 8. Optional Enhancements (Future Considerations)

### Rich Notifications (Images, Actions)

**iOS:** Requires Notification Service Extension (native code)  
**Android:** Requires custom notification layout (native code)

**Not recommended for generated bundles.** Document as "Advanced: Custom Native Code Required."

---

### Notification Categories & Actions

**Use case:** Actionable notifications (e.g., "Reply", "Dismiss", "Mark as Read")

**iOS:** Requires `UNNotificationCategory` registration  
**Android:** Requires notification actions in payload

**Implementation complexity:** Medium (requires native code changes)

**Recommendation:** Document as "Advanced Feature" for consumers who need it.

---

### Badge Count Management

**iOS:** APNs payload can set badge count  
**Android:** Requires notification channel badge support (API 26+)

**Client-side badge clearing:**

```typescript
import { PushNotifications } from '@capacitor/push-notifications';

// Clear badge count
await PushNotifications.removeAllDeliveredNotifications();

// Or set badge count (iOS only)
// Requires native code in AppDelegate
```

**Recommendation:** Include in v1 as optional feature (consumer can enable via backend payload).

---

### Topics & Targeting

**Use case:** Subscribe users to notification topics (e.g., "sports", "news", "promotions")

**Implementation:**

- **@capacitor/push-notifications:** Manual backend implementation (consumer maintains topic → token mapping)
- **@capacitor-firebase/messaging:** Built-in Firebase Topics API

**Recommendation:** Document pattern for manual implementation; suggest Firebase if advanced targeting is needed.

---

## Appendix A: MobileBundleService Changes

### New `PrismMobileBundleRequest` Properties

```csharp
public class PrismMobileBundleRequest
{
    // Existing properties...
    
    /// <summary>
    /// Whether to include push notification scaffolding in the generated bundle.
    /// </summary>
    public bool? PushNotificationsEnabled { get; set; }
    
    /// <summary>
    /// Optional custom name for the Android notification channel.
    /// Defaults to "Prism Notifications" if not provided.
    /// </summary>
    public string? NotificationChannelName { get; set; }
}
```

### Files Generated When `PushNotificationsEnabled = true`

1. **`package.json`**, add `@capacitor/push-notifications: ^8.0.0`

2. **`www/index.html`**, add push permission request logic (inline):
   ```javascript
   // After biometric enrollment flow
   async function requestPushPermission() {
     const hasAsked = await window.Capacitor.nativePromise('Preferences', 'get', { key: 'prism-push-permission-asked' });
     
     if (!hasAsked.value) {
       const result = await window.Capacitor.nativePromise('PushNotifications', 'requestPermissions', {});
       await window.Capacitor.nativePromise('Preferences', 'set', { key: 'prism-push-permission-asked', value: 'true' });
       
       if (result.receive === 'granted') {
         await window.Capacitor.nativePromise('PushNotifications', 'register', {});
       }
     }
   }
   ```

3. **`README.md`**, add "Push Notifications Setup" section with 10-step guide

4. **`AGENT_PROMPT.md`**, add "Configuring Push Notifications" section

5. **`resources/ios-entitlements-push.xml`**, template entitlements file

6. **`resources/android-firebase-setup.md`**, Firebase Console walkthrough

7. **`scripts/bootstrap-ios.sh`**, auto-inject entitlements check

8. **`scripts/bootstrap-android.sh`**, auto-inject `POST_NOTIFICATIONS` permission

---

## Appendix B: Backend API Endpoints

### POST /umbraco/prism/mobile/push/register

**Purpose:** Register or update a device push token

**Request:**
```json
{
  "deviceToken": "abc123...",
  "platform": "ios" | "android",
  "deviceId": "unique-device-id",
  "tenantHostname": "portal.example.com"
}
```

**Response:**
```json
{
  "registered": true,
  "expiresAt": "2027-07-14T12:00:00Z"
}
```

**Authorization:** `PrismMemberCookie` required (extracts `UserOid` from claims)

**Logic:**
1. Extract `UserOid` and `TenantId` from cookie
2. Hash `deviceToken` (store hash, not plaintext)
3. Check if record exists for `DeviceId + UserOid + TenantId`
4. If exists: update `DeviceToken`, `UpdatedAt`, set `Revoked = false`
5. If new: insert new `PrismPushTokenRecord`
6. Return success

---

### DELETE /umbraco/prism/mobile/push/revoke

**Purpose:** Revoke all push tokens for the current user

**Authorization:** `PrismMemberCookie` required

**Response:** `204 No Content`

**Logic:**
1. Extract `UserOid` and `TenantId` from cookie
2. Find all `PrismPushTokenRecord` for `UserOid + TenantId`
3. Set `Revoked = true` for all matching records
4. Return success

---

### POST /umbraco/prism/mobile/push/send (Admin-side)

**Purpose:** Send push notification to specific user(s)

**Request:**
```json
{
  "userOids": ["oid1", "oid2"],
  "tenantId": "tenant-id",
  "notification": {
    "title": "Order Ready",
    "body": "Your order #12345 is ready",
    "data": {
      "page": "orders",
      "id": "12345"
    }
  }
}
```

**Authorization:** Backoffice admin only

**Logic:**
1. Find all non-revoked tokens for `userOids + tenantId`
2. Group tokens by platform (iOS → APNs, Android → FCM)
3. Send notification via APNs/FCM APIs
4. Log delivery status
5. Return success

---

## Appendix C: Database Schema

### Table: `prismPushTokens`

```sql
CREATE TABLE prismPushTokens (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DeviceTokenHash TEXT NOT NULL, -- SHA256 hash of device token
    Platform TEXT NOT NULL, -- 'ios' or 'android'
    DeviceId TEXT NOT NULL,
    UserOid TEXT NOT NULL,
    TenantId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    LastSeenAt TEXT, -- Last time token was used/refreshed
    Revoked INTEGER NOT NULL DEFAULT 0, -- 1 = revoked
    UNIQUE(DeviceId, UserOid, TenantId)
);

CREATE INDEX idx_prismPushTokens_userOid ON prismPushTokens(UserOid);
CREATE INDEX idx_prismPushTokens_tenantId ON prismPushTokens(TenantId);
CREATE INDEX idx_prismPushTokens_revoked ON prismPushTokens(Revoked);
```

**Why hash `DeviceToken`?**

- Security: If database is compromised, attacker cannot use raw tokens
- Privacy: Tokens are sensitive and should be treated like passwords

**Why store `DeviceId`?**

- Allows revoking tokens for specific devices (e.g., "Sign out on this device")
- Prevents token reuse across devices

---

## Summary of Key Decisions

1. **Plugin:** `@capacitor/push-notifications` (official Capacitor plugin)
2. **Timing:** Request permission **after biometric login** (post-authentication)
3. **Architecture:** Consumer configuration (scaffolding in generated bundle, not a Prism plugin)
4. **iOS:** APNs p8 key (recommended over p12 cert)
5. **Android:** FCM via `google-services.json` + service account key
6. **Foreground UX:** In-app banner (custom UI), not system notification
7. **Deep linking:** Simple `page + id` pattern in notification `data` payload
8. **Token lifecycle:** Auto-registration on permission grant, auto-refresh on rotation
9. **Revocation:** Logout revokes all tokens for user
10. **Consumer friction:** 40-50 minutes for first-time setup, 15 minutes for repeat apps

---

**Next Steps:**

1. Review this design with Blathers (backend API implementation)
2. Review with Copper (security audit of token storage/hashing)
3. Review with Tom Nook (architectural approval)
4. Implementation: Update `MobileBundleService.cs` to generate push scaffolding
5. Testing: Create test tenant with push enabled, verify iOS + Android flows

**Questions for Team:**

1. Should push notifications be **opt-in** (default `PushNotificationsEnabled: false`) or **opt-out**?
2. Should Prism provide a "Test Push" UI in the backoffice tenant management screen?
3. Should we support **admin-initiated push broadcasts** (send to all users of a tenant) in v1?

---

**End of Design Document**
