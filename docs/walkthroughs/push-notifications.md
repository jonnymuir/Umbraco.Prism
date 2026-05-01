# Walkthrough — End-to-End Push Notifications

A complete guide to the Prism push notification system: from VAPID key generation and browser subscription through to sending a notification from the Umbraco backoffice and receiving it on a device.

> **Note:** This walkthrough consolidates guidance from [`docs/PUSH_SETUP.md`](../PUSH_SETUP.md), [`docs/notifications-design.md`](../notifications-design.md), and the architecture docs in `docs/design/notifications-*.md`. Architecture decisions and technology rationale are explained in those documents — this walkthrough focuses on the operational steps. Where relevant, links point back to the source material rather than repeating it.

> **Prerequisites:** Stack running. See [Codespaces](../../README.md#try-it-now--no-install-required) or [local setup](../../README.md#try-the-demo--local-setup). For native mobile push, also complete the native setup steps in [`docs/PUSH_SETUP.md`](../PUSH_SETUP.md).

---

## Overview

Prism supports two push notification transports:

| Transport | Platform | Technology |
|---|---|---|
| **Web push (VAPID)** | Desktop and mobile browsers | W3C Push API + service worker |
| **Native push (FCM → APNs/FCM)** | Capacitor iOS/Android apps | Firebase Cloud Messaging via `@capacitor/push-notifications` |

This walkthrough covers the **web push** path end-to-end, then explains where native push diverges. The technology decision to use FCM (rather than VAPID alone or OneSignal) is documented in [`docs/notifications-design.md#2-technology-decision-fcm`](../notifications-design.md#2-technology-decision-fcm) — the short answer is that FCM covers both iOS and Android in one integration.

---

## Part 1: Architecture at a Glance

```
User browser / Capacitor app
  ↓  subscribe
Prism backend (UmbracoPrism.Core)
  ↓  stores token
prismPushTokens table (Umbraco DB)
  ↓
Trigger: Operator (backoffice) or ContentPublished notification handler
  ↓
IPrismPushNotificationService.SendAsync()
  ↓
FCM HTTP v1 API (via FirebaseAdmin SDK)
  ↓
FCM → APNs (iOS) / FCM direct (Android) / Web Push (browser)
  ↓
Device receives notification
```

Push logic is split between the package and the consuming site — see the full split of responsibility table in [`docs/notifications-design.md#1-architecture-overview`](../notifications-design.md#1-architecture-overview).

---

## Part 2: Backend Configuration

### Step 1: Enable Push in Your Configuration

Add the following to `appsettings.json` (or user secrets for local dev):

```json
{
  "Prism": {
    "Push": {
      "FcmServiceAccountSecretName": "prism-fcm-service-account",
      "Enabled": true
    }
  }
}
```

- **`FcmServiceAccountSecretName`** — the Azure Key Vault secret name holding your Firebase service account JSON. For local dev without Key Vault, you can provide the raw JSON directly via user secrets (see below).
- **`Enabled`** — when `false`, the push subsystem is a no-op. Defaults to `false`.

💡 **What's happening:** On startup, `PrismComposer` reads this configuration. If `Enabled: true`, it initialises a `FirebaseApp` using the service account JSON resolved from Key Vault (or user secrets). It also runs the migration plan that creates `prismPushTokens` and `prismPushSubscriptions` tables in the Umbraco database.

### Step 2: Firebase Service Account (Local Dev Without Key Vault)

For local development, you can skip Key Vault and provide the Firebase service account JSON directly via .NET User Secrets:

```bash
cd src/UmbracoPrism.TestSite
dotnet user-secrets set "Prism:Push:FcmServiceAccountJson" "$(cat path/to/your-service-account.json)"
```

Then update `appsettings.Development.json`:

```json
{
  "Prism": {
    "Push": {
      "Enabled": true
    }
  }
}
```

> ⚠️ **Never commit the service account JSON** to source control. It grants write access to your Firebase project.

### Step 3: Generate VAPID Keys (Web Push)

Web push requires a VAPID (Voluntary Application Server Identification) key pair. Generate one:

```bash
npx web-push generate-vapid-keys
```

Output:

```
Public Key:  BHxyz...
Private Key: abc123...
```

Add these to your configuration:

```json
{
  "Prism": {
    "Push": {
      "VapidPublicKey":  "BHxyz...",
      "VapidPrivateKey": "abc123...",
      "VapidSubject":    "mailto:admin@your-site.com",
      "Enabled": true
    }
  }
}
```

💡 **What's happening:** VAPID keys identify your server to the browser's push service (e.g., Mozilla's or Google's push relay). The public key is sent to the browser when it subscribes; the private key signs each push request. The `VapidSubject` is a contact address the push service can use if there are problems with your push traffic.

---

## Part 3: Browser Subscription

### Step 4: Subscribe in the Notification Preferences UI

1. Log into the TestSite at `https://localhost:44345`.
   - Credentials: `demo@prism.local` / `password`

2. Navigate to your profile or notification preferences. Look for a **"Enable push notifications"** toggle or button.

<!-- TODO: capture 01-notification-prefs.png via TestSite → notification preferences page after login -->
<!-- pending capture -->

3. Click **Enable push notifications**.

4. Your browser shows a permission prompt: **"Allow [site] to send notifications?"**

5. Click **Allow**.

<!-- TODO: capture 02-browser-permission.png via browser permission prompt (manual only — browser UI cannot be automated) -->
<!-- pending capture -->

6. ✅ **What you can do:** If you dismiss or deny the prompt, the toggle shows as "Blocked". To re-trigger it during development, clear the notification permission in browser settings (DevTools → Application → Permissions, or site settings).

7. 💡 **What's happening (client side):**
   - The `prism-push-subscribe` Lit web component (registered in `src/UmbracoPrism.Client/`) calls `Notification.requestPermission()`.
   - If granted, it calls `serviceWorkerRegistration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: VAPID_PUBLIC_KEY })` where `VAPID_PUBLIC_KEY` is the public VAPID key fetched from `GET /umbraco/api/prism/push/vapid-public-key`.
   - The resulting `PushSubscription` object (containing the endpoint URL and encryption keys) is serialized and POSTed to `POST /umbraco/api/prism/push/subscribe`.

8. 💡 **What's happening (server side):**
   - `PrismPushController.Subscribe()` validates the bearer token, extracts the user ID and tenant ID from claims, and stores the subscription in the `prismPushSubscriptions` table.
   - The subscription is associated with the user's current device (identified by the endpoint URL).

### Step 5: Subscribe to Topics (Optional)

The Prism notification system supports topic-based subscriptions (e.g., "Planning Notifications", "Service Updates"). From the notification preferences UI:

1. Find the **Topics** section below the master toggle.
2. Check or uncheck individual topics.
3. Changes are saved immediately via `POST /umbraco/api/prism/push/subscriptions`.

💡 **What's happening:** Topic subscriptions are stored in `prismPushSubscriptions` with a `genre` column. When a notification is sent for a genre, only subscribers to that genre receive it — the backend queries `prismPushTokens JOIN prismPushSubscriptions WHERE genre = :genre AND tenantId = :tenantId`.

---

## Part 4: Sending a Notification

### Option A: Operator-Triggered (Umbraco Backoffice)

1. Log into the Umbraco backoffice at `https://localhost:44345/umbraco`.
   - Username: `admin@prism.local`, Password: `PrismLocal!12345`

2. Navigate to **Content** and find the **Announcements** section (or the **Send Notification** dashboard).

3. Publish or create an announcement content item.

<!-- TODO: capture 03-backoffice-send-notification.png via backoffice → Announcements section -->
<!-- pending capture -->

4. 💡 **What's happening:** Publishing the content node triggers Umbraco's `ContentPublishedNotification`. Prism's `INotificationHandler<ContentPublishedNotification>` intercepts this event, checks the content type (e.g., `announcement`), builds a push payload, and calls `IPrismPushNotificationService.SendAsync()`.

   The service queries all subscribers for the relevant tenant and topic, then dispatches via the `FirebaseAdmin` SDK. FCM routes the message to the appropriate platform (APNs for iOS devices, FCM direct for Android, Web Push relay for browsers).

### Option B: API-Triggered (MockBusinessApp Demo)

The MockBusinessApp exposes a test endpoint for sending notifications without the backoffice:

```bash
curl -X POST https://localhost:7245/api/test/send-notification \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {your-token}" \
  -d '{
    "title": "Test Notification",
    "body": "This is a test push notification from the demo app.",
    "tenantId": "{your-tenant-id}",
    "genre": "service-updates"
  }'
```

💡 **What's happening:** This calls `IPrismPushNotificationService.SendAsync()` directly, bypassing the content publishing flow. Useful for testing the full delivery path during development.

### Option C: Consumer-Triggered (Your Own Code)

In your own site code, inject `IPrismPushNotificationService` and call it:

```csharp
public class MyService(IPrismPushNotificationService pushService, IPrismContext prismContext)
{
    public async Task NotifyUserAsync(string userId, string title, string body)
    {
        await pushService.SendToUserAsync(
            tenantId: prismContext.CurrentTenant!.TenantId,
            userId: userId,
            title: title,
            body: body,
            cancellationToken: default);
    }
}
```

---

## Part 5: Receiving a Notification in the Browser

After a notification is dispatched, the browser's push service delivers it to the registered service worker:

1. The **service worker** (`/sw.js`, registered by the Prism client bundle) receives the `push` event.
2. It calls `self.registration.showNotification(title, options)` with the payload.
3. If the browser is in the foreground, the notification appears as an OS-level notification toast.
4. If the browser is in the background or closed, the notification still appears (service workers run independently).

<!-- TODO: capture 04-browser-notification.png via OS notification toast (manual only) -->
<!-- pending capture -->

5. ✅ **Clicking the notification:** The service worker's `notificationclick` event handler navigates the browser to the notification's action URL (e.g., `/announcements/{slug}`).

---

## Part 6: Native Mobile Push (Capacitor)

For native iOS/Android apps built with Capacitor, the push flow uses `@capacitor/push-notifications` instead of the Web Push API.

The high-level difference:

| Step | Web | Native (Capacitor) |
|---|---|---|
| Request permission | `Notification.requestPermission()` | `PushNotifications.requestPermissions()` |
| Get device token | `PushManager.subscribe()` → endpoint URL | `PushNotifications.register()` → FCM registration token |
| Register with backend | POST subscription object | POST FCM token |
| Receive notification | Service worker `push` event | `PushNotifications.addListener('pushNotificationReceived', ...)` |
| Notification tap | `notificationclick` in SW | `PushNotifications.addListener('pushNotificationActionPerformed', ...)` |

The Prism client module `src/UmbracoPrism.Client/src/backoffice/push-notifications.ts` wraps `@capacitor/push-notifications` with graceful web degradation — `Capacitor.isNativePlatform()` gates all native API calls so the same code works in both environments.

For native platform setup steps (APNs entitlements, `google-services.json`, Gradle configuration), see **[docs/PUSH_SETUP.md](../PUSH_SETUP.md)** — that document is the authoritative native setup guide.

For the Capacitor app structure and how to build for iOS/Android, see the **[Building a Mobile App walkthrough](building-a-mobile-app.md)**.

---

## Troubleshooting

| Problem | First check |
|---|---|
| Browser permission prompt doesn't appear | Check `Notification.permission` in the browser console — if `"denied"`, the user previously blocked it. Reset via browser site settings. |
| Subscription POST returns 401 | Bearer token is missing or expired. Sign in again. |
| Notification dispatched but not received | Check FCM dashboard for delivery status. Verify the device token hasn't expired (tokens rotate; Prism re-registers on each login). |
| iOS notifications not arriving | Must test on a physical device (simulator doesn't support APNs). Verify `aps-environment` entitlement is set. See [PUSH_SETUP.md](../PUSH_SETUP.md#ios-setup-apns). |
| Android token registration fails | Verify `google-services.json` is in `android/app/`. Run `npx cap sync android`. |

---

## Decision Points — Where to Read More

The following decisions are already documented in the design docs — don't re-derive them, follow the links:

| Question | Where answered |
|---|---|
| Why FCM over direct APNs + separate Web Push? | [`docs/notifications-design.md#2-technology-decision-fcm`](../notifications-design.md#2-technology-decision-fcm) |
| Why not OneSignal or Azure Notification Hubs? | [`docs/notifications-design.md#2-technology-decision-fcm`](../notifications-design.md#2-technology-decision-fcm) (rejected alternatives) |
| How are tokens stored and what's the schema? | [`docs/design/notifications-backend.md`](../design/notifications-backend.md) |
| How does the service worker interact with the Capacitor shell? | [`docs/design/notifications-mobile.md`](../design/notifications-mobile.md) |
| Architecture diagram (full component map) | [`docs/design/notifications-architecture.md`](../design/notifications-architecture.md) |

---

[← Back to Walkthroughs](README.md)
