# Push Notifications — Design Document

> **Note:** This is an **internal design document** for contributors and maintainers. For user-facing setup instructions, see [PUSH_SETUP.md](PUSH_SETUP.md).

**Author:** Tom Nook (Lead) / Kicks (Mobile Native Specialist)  
**Requested by:** Jonny Muir  
**Status:** Draft — Awaiting Jonny's review before implementation begins  
**Date:** 2026-07-14

---

## Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Technology Decision: FCM](#2-technology-decision-fcm)
3. [Device Registration Flow](#3-device-registration-flow)
4. [Subscription Management (Use Case 1A)](#4-subscription-management-use-case-1a)
5. [Broadcast Notifications (Use Case 1B)](#5-broadcast-notifications-use-case-1b)
6. [Backend-Triggered Notifications (Use Case 2)](#6-backend-triggered-notifications-use-case-2)
7. [Capacitor Integration](#7-capacitor-integration)
8. [Demo Plan](#8-demo-plan)
9. [Database Schema](#9-database-schema)
10. [Security Considerations](#10-security-considerations)
11. [Open Questions & Risks](#11-open-questions--risks)
12. [Proposed Implementation Phases](#12-proposed-implementation-phases)

---

## 1. Architecture Overview

### Split of Responsibility

Push notification logic is split between the NuGet package and the consuming site, following the same pattern as the rest of Prism:

| Layer | Lives in | Responsibility |
|---|---|---|
| Device registration API | `UmbracoPrism.Core` | Accept/store/remove FCM tokens from mobile |
| Push dispatch service | `UmbracoPrism.Core` | Send to FCM via FirebaseAdmin SDK |
| Content-subscription hooks | `UmbracoPrism.Core` | Umbraco `INotificationHandler` for publish events |
| Scheduled task runner | `UmbracoPrism.Core` | `IRecurringBackgroundTask` checking due notifications |
| FCM credential resolution | `UmbracoPrism.Core` (via Key Vault) | Same pattern as OIDC secrets |
| Consumer-triggered notifications | Consuming site | Calls `IPrismPushNotificationService` from their own code |
| Subscription UI | `UmbracoPrism.Client` | Lit web component `prism-push-subscribe` |
| Backoffice broadcast dashboard | `UmbracoPrism.Client` | New backoffice dashboard section |
| Capacitor plugin wiring | `UmbracoPrism.Client` | `@capacitor/push-notifications` integration |
| Demo content types | `UmbracoPrism.TestSite` | Seeders for Announcement content type + welcome flow |

### What the consumer configures

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

- `FcmServiceAccountSecretName` — the Key Vault secret name holding the Firebase service account JSON (same pattern as `SecretKeyName` for OIDC)
- `Enabled` — opt-in flag; when `false`, the push subsystem is a no-op. Defaults `false`.

### What Prism handles automatically when enabled

- FCM `FirebaseApp` initialisation on startup (via `PrismComposer`)
- Migration of `prismPushTokens` and `prismPushSubscriptions` tables (via `PrismMigrationPlan`)
- `ContentPublishedNotification` handler → subscription query → push dispatch
- `IRecurringBackgroundTask` for scheduled/queued notifications
- REST endpoints under `/umbraco/api/prism/push/`

---

## 2. Technology Decision: FCM

**Recommendation: Use Firebase Cloud Messaging (FCM) via the `FirebaseAdmin` .NET SDK.**

### Why FCM

- FCM works for both iOS (via APNs) and Android in one integration — no separate APNs HTTP/2 provider needed in .NET
- `@capacitor/push-notifications` has first-class FCM support (it's the primary transport on Android; it wraps APNs via FCM on iOS)
- `FirebaseAdmin` NuGet package is mature, actively maintained, and supports HTTP v1 API
- Token-based service account auth (not deprecated legacy server key)

### Concerns with FCM

| Concern | Mitigation |
|---|---|
| **Android: requires Google Play Services** | Acceptable — AOSP/non-GMS devices are out of scope for a standard consumer app |
| **iOS: APNs via FCM adds a hop** | For standard use (not ultra-low-latency), the latency is negligible. Direct APNs integration is the only alternative, which means maintaining two dispatch paths. Not worth it. |
| **Firebase project dependency** | Consumer must create a Firebase project. This is a one-time setup. Documented clearly. |
| **JSON service account in Key Vault** | The service account JSON is ~2KB. Fits in a Key Vault secret value. |

### Rejected Alternatives

- **OneSignal** — third-party SaaS; adds a data processor dependency; overkill for a NuGet package
- **Azure Notification Hubs** — good at scale, but adds Azure-specific coupling and complexity for what should be a general Umbraco package
- **Direct APNs + FCM split** — two dispatch paths, higher maintenance, no benefit for this stack
- **Web push (VAPID only)** — doesn't work from a Capacitor native shell; deferred to a future enhancement

---

## 3. Device Registration Flow

### Token Lifecycle

```
[Mobile App Starts]
     │
     ▼
Request push permission (iOS: explicit prompt; Android 13+: POST_NOTIFICATIONS)
     │
     ▼
Capacitor PushNotifications.register()
     │
     ▼
FCM returns device token
     │
     ▼
POST /umbraco/api/prism/push/register
  { "token": "...", "platform": "ios|android", "deviceId": "..." }
     │
     ▼
Stored in prismPushTokens (linked to authenticated member)
     │
     ▼
[Token refresh] → same endpoint, upsert by deviceId
[Logout]        → DELETE /umbraco/api/prism/push/register/{deviceId}
```

### Token Storage: `prismPushTokens` table

See [Section 9](#9-database-schema) for the full schema. Key design decisions:

- **Keyed by `DeviceId`** (client-generated UUID, same pattern as `prismDeviceCredentials`) — allows upsert on refresh without creating duplicates
- **Linked to `MemberKey`** (Umbraco member GUID, not Entra OID) — push notifications are member-scoped, not tenant-auth-scoped
- **`Platform` column** (`ios`/`android`) — allows platform-specific FCM payload formatting if needed
- **`LastSeenAt`** — updated on every registration call; tokens not updated in 90 days are treated as stale and skipped on dispatch (not deleted immediately — a separate cleanup task prunes them)
- **`IsActive` bool** — set to `false` on logout and on FCM `registration-token-not-registered` errors during dispatch

### Multiple devices per member

A single member can have many rows in `prismPushTokens` (phone + tablet + second device). Dispatch queries all active tokens for a `MemberKey` and sends to each. FCM handles the fan-out per token atomically.

### Token refresh

`@capacitor/push-notifications` fires `registration` on every app start if the token has changed. The mobile app always calls the register endpoint on start. The backend upserts by `DeviceId`, so stale tokens are naturally replaced.

### Logged-out devices

On explicit logout from the Prism mobile app, the client calls `DELETE /umbraco/api/prism/push/register/{deviceId}`. This sets `IsActive = false`. If the member is force-signed-out server-side (e.g. token revocation), a background pass will catch the `registration-token-not-registered` FCM error on next dispatch and deactivate the token.

---

## 4. Subscription Management (Use Case 1A)

### What members can subscribe to

Three subscription dimensions (all optional/combinable):

| Dimension | Example | Column |
|---|---|---|
| **Content node** | A specific Announcement node | `ContentNodeKey` (Umbraco GUID) |
| **Content type** | All Announcements | `ContentTypeAlias` |
| **Category tag** | All "Events" tagged content | `Category` |

For the initial implementation and demo, **content type alias** subscriptions are the primary use case (subscribe to all Announcements). Per-node subscriptions are the secondary use case.

### Subscription storage: `prismPushSubscriptions` table

See [Section 9](#9-database-schema). Key decisions:

- Unique constraint on `(MemberKey, ContentNodeKey, ContentTypeAlias, Category)` — prevents duplicate subscriptions
- Nullable columns allow `NULL` to mean "any" — a row with only `ContentTypeAlias = 'announcement'` matches any published Announcement

### Umbraco hook: `ContentPublishedNotification`

In `PrismComposer`, register:

```csharp
builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismPushContentPublishedHandler>();
```

`PrismPushContentPublishedHandler` logic:

1. For each published content item, determine its content type alias and any assigned categories
2. Query `prismPushSubscriptions` for matching rows (by `ContentNodeKey` OR `ContentTypeAlias` OR `Category`)
3. Deduplicate by `MemberKey`
4. For each unique `MemberKey`, query `prismPushTokens` for active tokens
5. Dispatch via `IPrismPushDispatchService` (batched FCM multicast)
6. Update `LastDispatchedAt` and handle token deactivation on FCM errors

### Subscribe/Unsubscribe API

```
POST   /umbraco/api/prism/push/subscriptions
DELETE /umbraco/api/prism/push/subscriptions/{id}
GET    /umbraco/api/prism/push/subscriptions          (for current member)
```

All endpoints require an authenticated member cookie/session. The subscribe endpoint accepts:

```json
{
  "contentNodeKey": "guid-or-null",
  "contentTypeAlias": "announcement",
  "category": null
}
```

### Subscribe UI

A Lit web component `<prism-push-subscribe>` rendered on the frontend:

```html
<prism-push-subscribe
  content-type-alias="announcement"
  label="Notify me of new announcements">
</prism-push-subscribe>
```

- Detects mobile context via `PrismMobile` UserAgent (same pattern already used throughout)
- On click: calls `PushNotifications.requestPermissions()` if not yet granted, then registers token, then calls subscribe API
- Shows subscribed/unsubscribed state
- Gracefully hides itself in non-mobile browsers (or shows a "mobile only" hint)

---

## 5. Broadcast Notifications (Use Case 1B)

### Backoffice Dashboard

Add a new dashboard to the existing Prism backoffice section (or a new "Notifications" section). A Lit component `<prism-push-broadcast>` renders a simple form:

- **Title** (text input)
- **Message** (textarea)
- **Deep link URL** (optional text input, e.g. `/announcements`)
- **Target** (radio: All members / Members with at least one device)
- **Send** button

On submit: `POST /umbraco/api/prism/push/broadcast` (Umbraco admin auth required).

### Broadcast API

```csharp
[HttpPost("broadcast")]
[Authorize(Policy = PrismPolicies.PrismAdmin)]  // reuses existing admin policy
public async Task<IActionResult> Broadcast([FromBody] PrismBroadcastRequest request)
```

The handler queries all active `prismPushTokens` and dispatches in batches of 500 (FCM multicast limit). For large member bases this runs asynchronously — the endpoint returns `202 Accepted` with a dispatch job ID, and the result can be polled.

### Auto-broadcast on publish events

`PrismPushContentPublishedHandler` also supports a "broadcast on publish" mode, configurable per content type:

```json
{
  "Prism": {
    "Push": {
      "BroadcastOnPublish": ["announcement"]
    }
  }
}
```

When a content type is in this list, publishing any node of that type triggers a broadcast to all members (not just subscribers). This is opt-in and defaults to empty.

---

## 6. Backend-Triggered Notifications (Use Case 2)

### Public API surface

Prism exposes `IPrismPushNotificationService` for consuming apps to call:

```csharp
public interface IPrismPushNotificationService
{
    /// <summary>Send a push notification to a specific member immediately.</summary>
    Task SendToMemberAsync(string memberKey, PrismPushPayload payload, CancellationToken ct = default);

    /// <summary>Send to multiple members (fan-out).</summary>
    Task SendToMembersAsync(IEnumerable<string> memberKeys, PrismPushPayload payload, CancellationToken ct = default);

    /// <summary>Send to all registered members.</summary>
    Task BroadcastAsync(PrismPushPayload payload, CancellationToken ct = default);

    /// <summary>Enqueue a notification to be sent at a future time.</summary>
    Task ScheduleForMemberAsync(string memberKey, PrismPushPayload payload, DateTimeOffset scheduledAt, CancellationToken ct = default);
}

public record PrismPushPayload(string Title, string Body, string? Url = null, IDictionary<string, string>? Data = null);
```

### Scheduled notifications: approach

**Recommendation: Custom `prismPushQueue` table + `IRecurringBackgroundTask`.**

Rationale for choosing Umbraco's built-in over Hangfire:
- No additional NuGet dependency in the consuming site
- `IRecurringBackgroundTask` is built into Umbraco 13+ (uses `IHostedService` under the hood)
- Sufficient for the use cases in scope (minute-resolution scheduling)
- Consumers who need cron-level precision can implement `IPrismPushNotificationService` themselves with Hangfire

A `PrismPushQueueRunner` runs every 60 seconds. It:
1. Queries `prismPushQueue` for rows where `ScheduledAt <= UtcNow` and `SentAt IS NULL`
2. Dispatches each (fan-out to member tokens)
3. Updates `SentAt` and `Status`

The `ScheduleForMemberAsync` API just inserts into this table.

### Use Case 2A: Scheduled events — Demo scenario

**"Content Expiry Warning"** — Editors are notified 7 days before content they own expires.

Implementation:
- An additional `IRecurringBackgroundTask` (`PrismContentExpiryNotifier`) runs once daily at 08:00
- Queries Umbraco's `IContentService.GetPagedDescendants()` for content with `ExpireDate` between now and +7 days
- For each item, resolves the content's creator/responsible editor (using Umbraco member/user) and sends a notification via `IPrismPushNotificationService`
- This is backoffice-user-targeted, not member-targeted (editors have devices too, via the same `PrismMobile` shell)

This is a real Umbraco pain point. Content expiry is commonly forgotten. Any agency or publisher site benefits.

### Use Case 2B: API flow triggers — Demo scenario

**"Member Welcome Notification"** — 1 minute after a member registers, they receive a personalised welcome push on their device.

Implementation in the TestSite `TestSiteComposer`:

```csharp
builder.AddNotificationAsyncHandler<MemberCreatedNotification, WelcomeNotificationHandler>();
```

`WelcomeNotificationHandler`:

```csharp
await _pushService.ScheduleForMemberAsync(
    memberKey: e.Member.Key.ToString(),
    payload: new PrismPushPayload(
        Title: "Welcome to Prism Demo!",
        Body: "You now have access to all member features. Tap to explore.",
        Url: "/members/dashboard"),
    scheduledAt: DateTimeOffset.UtcNow.AddMinutes(1));
```

This demonstrates the full pipeline: member registers → notification queued → 1 minute later → FCM dispatch → notification appears on device.

---

## 7. Capacitor Integration

### New package

Add `@capacitor/push-notifications` to `src/UmbracoPrism.Client/package.json`:

```json
"@capacitor/push-notifications": "^8.0.0"
```

### Mobile shell changes required (iOS)

**`ios/App/App/AppDelegate.swift`** — no changes needed if using Capacitor's push plugin; it handles `didRegisterForRemoteNotificationsWithDeviceToken` automatically.

**`ios/App/App/App.entitlements`** — add:
```xml
<key>aps-environment</key>
<string>development</string>  <!-- change to 'production' for App Store builds -->
```

**`ios/App/Podfile`** — Capacitor push plugin auto-links; run `pod install` after `npm install`.

**Apple Developer Portal** — Push Notifications capability must be enabled for the App ID. APNs key (p8) must be uploaded to the Firebase console.

### Mobile shell changes required (Android)

**`android/app/google-services.json`** — download from Firebase console and place here.

**`android/app/src/main/AndroidManifest.xml`** — Android 13+ requires:
```xml
<uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>
```

Capacitor push plugin handles FCM receiver registration automatically via `google-services.json`.

### Permission request flow

iOS is strict: permission must be requested at the right moment (not on first launch cold). The pattern:

```typescript
// After the member has logged in and reached the home screen
async function requestPushPermission(): Promise<void> {
  const { receive } = await PushNotifications.checkPermissions();
  if (receive === 'prompt') {
    const result = await PushNotifications.requestPermissions();
    if (result.receive === 'granted') {
      await PushNotifications.register();
    }
  } else if (receive === 'granted') {
    await PushNotifications.register();
  }
  // If denied: surface a settings deep-link (iOS: open app settings)
}
```

**Do NOT request permission on app cold start.** Request it only after the member is authenticated and has seen value from the app. Recommended trigger: after first successful login, or when member taps a "Enable notifications" prompt on the home screen.

### Token registration with backend

```typescript
PushNotifications.addListener('registration', async (token) => {
  await fetch('/umbraco/api/prism/push/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({
      token: token.value,
      platform: Capacitor.getPlatform(), // 'ios' | 'android'
      deviceId: await getOrCreateDeviceId(), // reuse existing DeviceId pattern
    }),
  });
});
```

### Foreground notification handling

When the app is in the foreground, FCM does not show a system notification automatically on iOS. Handle it:

```typescript
PushNotifications.addListener('pushNotificationReceived', (notification) => {
  // Show in-app banner (a Lit component <prism-in-app-notification>)
  // or navigate directly if the app is on a non-relevant page
});
```

### Notification tap → deep link

```typescript
PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
  const url = action.notification.data?.url;
  if (url) {
    // Use existing Capacitor navigation or window.location
    window.location.href = url;
  }
});
```

Deep link `url` values are relative paths (`/announcements/my-announcement`). The Capacitor WebView navigates to the Umbraco frontend route — no custom URL scheme needed unless native deep links to specific app screens are required (deferred to a later phase).

### Background vs killed-state notifications

FCM handles delivery in both cases. When the user taps the notification:
- From background: `pushNotificationActionPerformed` fires
- From killed state: the notification tap launches the app; `pushNotificationActionPerformed` fires after Capacitor initialises

Ensure listeners are registered before any async operations in `initializeApp()`.

---

## 8. Demo Plan

### Demo A: "Prism Announcements" (Use Cases 1A + 1B)

**Scenario:** The TestSite is a community portal. Members subscribe to push alerts for new site announcements. Editors can also blast all members with a broadcast notification from the backoffice.

**Document types to create (via seeder in `TestSiteComposer`):**

| Name | Alias | Properties |
|---|---|---|
| Announcement | `announcement` | Title (text), Summary (RTE), Body (RTE), PublishedDate (date) |
| Announcements Page | `announcementsPage` | Title (text), children: Announcement |

**Frontend pages (Razor/route-based):**
- `/announcements` — list of all Announcements with a prominent `<prism-push-subscribe content-type-alias="announcement">` component at the top
- `/announcements/{slug}` — detail view of a single announcement with a per-node subscribe option

**Demo flow:**
1. Developer installs TestSite, seeds content → three sample Announcements published
2. Member logs in on iOS/Android app
3. Member taps "Notify me of new announcements" → permission prompt → subscribed confirmation
4. Editor publishes a new Announcement in backoffice
5. Member's device receives notification: "New Announcement: [Title]" → taps → opens `/announcements/{slug}`
6. Backoffice broadcast: editor opens Prism → Notifications dashboard → fills form → "Send to all members" → all registered devices receive the blast notification

**Compelling because:** This is exactly what a club site, alumni network, or intranet portal would build. It's immediately relatable.

---

### Demo B: "Welcome Notification + Content Expiry Warning" (Use Cases 2A + 2B)

**Scenario A — Welcome notification (2B):**

1. New member registers on the TestSite
2. Welcome notification queued for T+1 minute
3. After 1 minute, member's device shows: "Welcome to Prism Demo Site 👋 — Tap to explore member features"
4. Tap → opens `/members/dashboard`

No additional content types needed. Just a `MemberCreatedNotification` handler in `TestSiteComposer` that calls `IPrismPushNotificationService.ScheduleForMemberAsync`.

**Scenario B — Content expiry warning (2A):**

In the TestSite, create one Announcement with `expireDate` set to 7 days from now. The `PrismContentExpiryNotifier` (daily task) picks this up and sends a notification to the Umbraco admin user's registered device: "⚠️ '[Announcement Title]' expires in 7 days — review it in the backoffice."

This requires the Umbraco admin user to also have a device token registered (i.e., the backoffice user also uses the Prism mobile app shell). For the demo, any registered editor/admin device receives this alert.

**Compelling because:** Content expiry amnesia is a real problem on every Umbraco site. Every editor has experienced discovering expired content too late.

---

## 9. Database Schema

### `prismPushTokens`

```csharp
[TableName("prismPushTokens")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismPushTokenSchema
{
    [Column("id")] [PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
    [Column("DeviceId")] [Length(64)] [Index(IndexTypes.UniqueNonClustered)] public string DeviceId { get; set; }
    [Column("MemberKey")] [Length(450)] [Index(IndexTypes.NonClustered)] public string MemberKey { get; set; }
    [Column("FcmToken")] [Length(512)] public string FcmToken { get; set; }
    [Column("Platform")] [Length(20)] public string Platform { get; set; }  // 'ios' | 'android'
    [Column("IsActive")] [Constraint(Default = "1")] public bool IsActive { get; set; }
    [Column("RegisteredAt")] [Constraint(Default = "getutcdate()")] public DateTime RegisteredAt { get; set; }
    [Column("LastSeenAt")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? LastSeenAt { get; set; }
}
```

### `prismPushSubscriptions`

```csharp
[TableName("prismPushSubscriptions")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismPushSubscriptionSchema
{
    [Column("id")] [PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
    [Column("MemberKey")] [Length(450)] [Index(IndexTypes.NonClustered)] public string MemberKey { get; set; }
    [Column("ContentNodeKey")] [Length(50)] [NullSetting(NullSetting = NullSettings.Null)] public string? ContentNodeKey { get; set; }
    [Column("ContentTypeAlias")] [Length(255)] [NullSetting(NullSetting = NullSettings.Null)] public string? ContentTypeAlias { get; set; }
    [Column("Category")] [Length(255)] [NullSetting(NullSetting = NullSettings.Null)] public string? Category { get; set; }
    [Column("CreatedAt")] [Constraint(Default = "getutcdate()")] public DateTime CreatedAt { get; set; }
}
```

Unique index on `(MemberKey, ContentNodeKey, ContentTypeAlias, Category)` to prevent duplicate subscriptions.

### `prismPushQueue`

```csharp
[TableName("prismPushQueue")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismPushQueueSchema
{
    [Column("id")] [PrimaryKeyColumn(AutoIncrement = true)] public int Id { get; set; }
    [Column("MemberKey")] [Length(450)] [Index(IndexTypes.NonClustered)] public string MemberKey { get; set; }
    [Column("PayloadJson")] public string PayloadJson { get; set; }  // JSON-serialised PrismPushPayload
    [Column("ScheduledAt")] [Index(IndexTypes.NonClustered)] public DateTime ScheduledAt { get; set; }
    [Column("CreatedAt")] [Constraint(Default = "getutcdate()")] public DateTime CreatedAt { get; set; }
    [Column("SentAt")] [NullSetting(NullSetting = NullSettings.Null)] public DateTime? SentAt { get; set; }
    [Column("Status")] [Length(50)] [Constraint(Default = "'pending'")] public string Status { get; set; }  // 'pending' | 'sent' | 'failed'
    [Column("ErrorMessage")] [NullSetting(NullSetting = NullSettings.Null)] public string? ErrorMessage { get; set; }
}
```

---

## 10. Security Considerations

The following are handed to **Copper** for threat modelling before Phase 1 implementation:

### FCM service account credentials

- The Firebase service account JSON is stored as a Key Vault secret (same pattern as OIDC client secrets)
- `SecretVaultService` caches the raw JSON for 1 hour — this is fine, but the `FirebaseApp` instance should be initialised once at startup (singleton), not per-request
- **Risk:** If Key Vault is unavailable at startup, `FirebaseApp` cannot initialise. The `Enabled` flag should degrade gracefully: push dispatch becomes a no-op with a warning log, not an exception that crashes the app

### Push token storage

- FCM tokens are not secrets in the classical sense (no private key), but they enable targeted message delivery to a specific device and reveal membership between user and device
- `prismPushTokens` should be treated with the same sensitivity as PII — tenant-scoped access, not globally queryable by default
- **Token enumeration risk:** The broadcast API must be `[Authorize(Policy = PrismPolicies.PrismAdmin)]` — not just authenticated member

### Subscribe endpoint

- Members can only subscribe/unsubscribe with their own `MemberKey` (derived from their authenticated session, not a body parameter)
- Prevent subscribing on behalf of another member (IDOR)

### Content expiry notifications

- The content expiry task runs as a background service with no user context — it queries Umbraco content directly. Ensure it cannot be triggered externally.
- Notifications to editors (backoffice users) should route via editor device tokens, which are separate from member device tokens — **needs clarification from Jonny** (see Open Questions)

### FCM payload

- Do not include sensitive member data in FCM notification payloads (they may be logged by FCM or visible in notification centre)
- The `data` field in `PrismPushPayload` should carry only routing info (URLs, content keys) — not PII or auth tokens

---

## 11. Open Questions & Risks

These need Jonny's input before implementation starts.

### Q1: Umbraco Members vs Entra-authenticated users

The Prism package currently authenticates users via Entra (Azure AD), not Umbraco native members. Push subscriptions are member-scoped by `MemberKey`. Clarification needed:

- **Are the device tokens linked to Umbraco members, Entra Object IDs, or both?**
- The design above uses `MemberKey` (Umbraco member GUID). Is that correct, or should it use the Entra OID from the JWT claims?
- Impact: if users don't have corresponding Umbraco member records, the subscription system needs to key off something else

### Q2: Editors on mobile — backoffice push recipients

For the content expiry demo, notifications are sent to "editors". Does the mobile app shell support the Umbraco backoffice, or is it purely member-facing? If editors don't use the mobile app, expiry warnings should go via email or a different channel.

- **Clarification:** Is the content expiry notification (2A) targeting Umbraco members or Umbraco backoffice users/editors?

### Q3: Web push

Is web push (PWA notifications in browser, for non-mobile users) in scope? FCM supports this via the JavaScript FCM SDK, but it requires a separate VAPID key, browser permission prompt, and service worker. It's architecturally compatible but doubles the implementation surface.

**Recommendation:** Defer to Phase 5 (post-MVP). Confirm with Jonny.

### Q4: Multi-tenancy and push tokens

In the current Prism multi-tenant model, each tenant has its own hostname and Entra configuration. Should push tokens be tenant-scoped (a member on Tenant A's device cannot receive notifications from Tenant B)?

**Recommendation:** Yes — add a `TenantId` column to `prismPushTokens` (same pattern as `prismDeviceCredentials`). The dispatch service filters by tenant. Needs Jonny to confirm the multi-tenancy requirement for push before we add the column.

### Q5: Firebase project — one per Prism installation or shared?

Each consuming Umbraco site needs its own Firebase project (for its own APNs certificate binding). This is correct and expected — Prism doesn't provide a shared FCM relay. Confirm this is acceptable for the NuGet package consumer experience.

### Q6: Notification volume / FCM rate limits

FCM HTTP v1 API has a rate limit of 600,000 messages/minute per project. For the broadcast endpoint with large member bases, batching is required (FCM multicast sends up to 500 tokens per request). The design handles this, but **confirm expected maximum member base size** for TestSite vs real-world deployments.

### Known Risks

| Risk | Severity | Mitigation |
|---|---|---|
| iOS push permission rejection rate is high (~40-60%) without good UX framing | Medium | Request permission at the right moment (post-login), provide clear value proposition in the prompt |
| FCM token churn — tokens can become invalid silently | Medium | Handle `registration-token-not-registered` errors in dispatch; deactivate tokens promptly |
| `IRecurringBackgroundTask` precision is ±60s | Low | Acceptable for welcome notification (T+1min) and daily expiry checks |
| Service account JSON rotation | Medium | Key Vault secret rotation will require `FirebaseApp` re-initialisation — either restart app, or implement hot-reload of Firebase credentials (complex). Recommend documenting manual restart as v1 process. |
| `PrismPushQueue` row accumulation if background task stops | Low | Add `prismPushQueue` cleanup pass (delete rows older than 30 days regardless of status) |

---

## 12. Proposed Implementation Phases

### Phase 1: Core plumbing — Device registration + FCM dispatch (no UI)

**Owner:** Blathers (backend) + Kicks (Capacitor)

- `prismPushTokens` migration
- `PrismPushTokensController` (register/unregister endpoints)
- `IPrismPushDispatchService` + `FcmPushDispatchService` (FirebaseAdmin, send to token list)
- `IPrismPushNotificationService` + `PrismPushNotificationService` (public API surface)
- `PrismPushOptions` configuration + Key Vault wiring
- `@capacitor/push-notifications` added to Client; permission + registration flow wired
- TestSite: manually call broadcast from a test endpoint to verify end-to-end

**Exit criteria:** Developer can register a device, call `IPrismPushNotificationService.SendToMemberAsync`, and receive a push notification on an iOS/Android device.

---

### Phase 2: Content subscriptions (Use Case 1A)

**Owner:** Blathers (backend) + Isabelle (Lit component) + Brewster (content seeding)

- `prismPushSubscriptions` migration
- `PrismPushSubscriptionsController` (CRUD)
- `PrismPushContentPublishedHandler` (Umbraco notification handler)
- `<prism-push-subscribe>` Lit web component
- TestSite: Announcement document type seeder + announcements page with subscribe component

**Exit criteria:** Member subscribes to Announcements; publishing a new Announcement triggers a push to all subscribers.

---

### Phase 3: Broadcast dashboard (Use Case 1B)

**Owner:** Isabelle (backoffice UI) + Blathers (broadcast endpoint)

- Broadcast controller endpoint (`POST /umbraco/api/prism/push/broadcast`)
- `<prism-push-broadcast>` backoffice dashboard Lit component
- Wire into existing Prism backoffice section as a new "Notifications" dashboard tab
- `BroadcastOnPublish` config option

**Exit criteria:** Editor fills broadcast form in backoffice → all registered devices receive the notification.

---

### Phase 4: Backend-triggered notifications + scheduled queue (Use Case 2)

**Owner:** Blathers (backend) + Brewster (Umbraco integration) + TestSite seeding

- `prismPushQueue` migration
- `PrismPushQueueRunner` (`IRecurringBackgroundTask`, 60s interval)
- `ScheduleForMemberAsync` implementation
- TestSite: `WelcomeNotificationHandler` (MemberCreatedNotification → schedule T+1min)
- TestSite/Core: `PrismContentExpiryNotifier` (daily task, 7-day expiry warning)

**Exit criteria:** Register as new member → 1 minute later receive welcome push. Set content expiry to T+7 days → next task run sends expiry warning.

---

### Phase 5: Polish + Web push (optional)

**Owner:** Kicks + Isabelle

- In-app foreground notification banner (`<prism-in-app-notification>`)
- Deep link routing improvements (Universal Links / App Links if needed)
- Web push via FCM web SDK + service worker (if confirmed in scope)
- Token cleanup background task (prune stale tokens)
- Copper security review of full feature

---

## Appendix: Service Registration (sketch)

```csharp
// In PrismComposer.Compose(), when push is enabled:
if (pushEnabled)
{
    builder.Services.AddSingleton<IPrismPushDispatchService, FcmPushDispatchService>();
    builder.Services.AddScoped<IPrismPushNotificationService, PrismPushNotificationService>();
    builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismPushContentPublishedHandler>();
    builder.Services.AddSingleton<PrismPushQueueRunner>();
    builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PrismPushQueueRunner>());
}
```

`FcmPushDispatchService` initialises `FirebaseApp` lazily on first dispatch call (not at startup) to avoid blocking app boot when Key Vault is slow. This trades off cold-start latency for reliability.

---

*Document ready for Jonny's review. Once Q1–Q5 are answered, Blathers and Kicks can begin Phase 1 immediately.*
