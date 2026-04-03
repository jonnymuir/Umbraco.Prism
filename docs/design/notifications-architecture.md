# Prism Mobile — Push Notifications (Architecture Design)

> **Internal Design Document:** This document is for contributors and maintainers. For setup instructions, see [../PUSH_SETUP.md](../PUSH_SETUP.md).

## Product Goal

Enable Umbraco sites using Prism to send push notifications to members via the Capacitor mobile app. Two primary use cases:

1. **Content-driven notifications** — triggered when content is published or changed in the Umbraco backoffice, targeting members who have subscribed to specific content nodes or categories, or broadcast globally.
2. **Backend-triggered notifications** — fired programmatically from developer code, background tasks, or business logic events.

The feature must feel native to Umbraco, be easy for package consumers to extend, and work within the existing Prism architecture (tenant-scoped, Entra-authenticated, mobile-first).

---

## Constraints and Assumptions

- Push notifications are **mobile-only** (Capacitor app). Web push is out of scope for v1 but the architecture should not preclude it.
- Device token registration reuses the existing `PrismMemberCookie` auth flow — no new auth scheme required.
- Notifications are **tenant-scoped**. A notification sent by Tenant A never reaches Tenant B's devices.
- The Umbraco site operator (package consumer) owns the push provider credentials (FCM project, APNs keys). Prism does not operate a shared push infrastructure.
- Prism does not use Umbraco Members for identity — users are Entra OIDs. Subscriptions and device tokens are keyed by Entra OID + tenant.
- The existing `prismDeviceCredentials` table already stores `DeviceId`, `TenantId`, `UserId`, and `Platform` — the notification system extends this rather than duplicating it.

---

## Glossary

| Term | Meaning |
|---|---|
| `PushToken` | FCM/APNs device token obtained from the OS push service and registered with the Prism backend |
| `PrismNotificationSubscription` | A record linking a user+device to a content node or topic they want notifications about |
| `NotificationTopic` | A logical grouping (e.g., `content:{nodeId}`, `global`, `category:{alias}`) used for routing |
| `IPrismNotificationService` | The developer-facing API for sending notifications programmatically |
| `PrismNotificationHandler` | Umbraco notification handler that listens for content events and triggers push delivery |

---

## 1. Overall Design

### Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     UMBRACO BACKOFFICE                       │
│                                                             │
│  Content publish/unpublish ──► Umbraco Notification System  │
│  Custom backoffice UI ──────► "Send Global Notification"    │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                   PRISM NOTIFICATION LAYER                   │
│                                                             │
│  PrismContentNotificationHandler                            │
│    (listens to ContentPublishedNotification)                │
│                                                             │
│  IPrismNotificationService                                  │
│    (public API for developers — send to user/topic/all)     │
│                                                             │
│  NotificationRouter                                         │
│    (resolves topic → device tokens via subscription store)  │
│                                                             │
│  NotificationDeliveryService                                │
│    (batches, retries, handles token expiry)                 │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                   PUSH GATEWAY ADAPTER                       │
│                                                             │
│  IPrismPushGateway (interface)                              │
│    ├── FcmPushGateway (default — Firebase Cloud Messaging)  │
│    └── (extensible: consumer can swap in APNs, OneSignal…)  │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                     MOBILE APP (Capacitor)                   │
│                                                             │
│  @capacitor/push-notifications plugin                       │
│    → Receives push, displays native notification            │
│    → On tap: deep-link to relevant content                  │
│    → On registration: sends PushToken to Prism backend      │
└─────────────────────────────────────────────────────────────┘
```

### Design Principles

1. **Adapter pattern for push gateway.** The gateway is behind `IPrismPushGateway`. Prism ships FCM as default. Consumers can register their own implementation if they use APNs direct, Azure Notification Hubs, or OneSignal.

2. **Subscriptions are first-class.** Rather than ad-hoc "send to this list of users," subscriptions are stored records that create a durable relationship between a member and a notification topic. This enables self-service subscribe/unsubscribe in the mobile app.

3. **Tenant-scoped everything.** Every push token, subscription, and notification record is scoped to a tenant. No cross-tenant leakage.

4. **Async delivery with batching.** Notifications are enqueued and delivered asynchronously. FCM supports batching (up to 500 tokens per request). The delivery service handles this transparently.

5. **Idempotent token registration.** The device can re-register its push token on every app launch (tokens rotate). The backend upserts, never duplicates.

---

## 2. Push Notification Provider Evaluation

### Recommendation: Firebase Cloud Messaging (FCM) via HTTP v1 API

| Provider | Pros | Cons | Verdict |
|----------|------|------|---------|
| **FCM (HTTP v1)** | Cross-platform (iOS + Android); free; massive ecosystem; Capacitor plugin exists (`@capacitor/push-notifications`); topic-based messaging built-in; reliable delivery | Requires Google Services account; iOS delivery goes through APNs anyway (FCM wraps it); vendor lock-in to Google | **✅ Recommended** |
| **APNs Direct** | No intermediary for iOS; Apple-native | iOS only — need separate Android solution; certificate management is painful; no topic routing | ❌ Half a solution |
| **Azure Notification Hubs** | Fits Azure-heavy stacks; cross-platform; SLA-backed | Paid (per-push pricing); heavier setup; adds Azure dependency beyond Key Vault; Capacitor plugin maturity is poor | ⚠️ Consider for enterprise tier |
| **OneSignal** | Easy setup; good dashboard; free tier | Third-party SaaS dependency; data leaves your infrastructure; doesn't align with Prism's self-hosted ethos | ❌ Wrong fit for a NuGet package |

### Why FCM wins for a Marketplace package

1. **Zero cost to consumers.** FCM is free. A NuGet package that requires a paid push service adds friction.
2. **Capacitor-native.** `@capacitor/push-notifications` uses FCM on Android and APNs-via-FCM on iOS. One token type, one API.
3. **HTTP v1 API is modern.** OAuth2 service account auth (fits Prism's existing Azure credential patterns). No legacy server keys.
4. **Topic messaging.** FCM supports server-side topic subscriptions — but we'll manage subscriptions ourselves for tenant isolation and flexibility.

### Configuration Shape

```json
{
  "Prism": {
    "Notifications": {
      "Enabled": true,
      "Provider": "fcm",
      "Fcm": {
        "ProjectId": "my-firebase-project",
        "ServiceAccountKeyPath": "/path/to/service-account.json"
      },
      "ContentNotifications": {
        "Enabled": true,
        "NotifyOnPublish": true,
        "NotifyOnUnpublish": false
      }
    }
  }
}
```

> **Alternative:** Store the FCM service account key in Azure Key Vault (Prism already integrates with Key Vault). The `ServiceAccountKeyPath` could accept a Key Vault secret name prefixed with `keyvault:` — e.g., `"ServiceAccountKeyPath": "keyvault:fcm-service-account"`.

---

## 3. Device Token Registration

### Flow

```
App Launch
    │
    ▼
[@capacitor/push-notifications] requestPermissions()
    │
    ▼
OS grants permission → plugin fires 'registration' event with PushToken
    │
    ▼
[Capacitor Bridge JS] POST /umbraco/prism/mobile/notifications/register
    Headers: PrismMemberCookie (authenticated)
    Body: {
      "pushToken": "fcm-token-string",
      "deviceId": "existing-device-uuid",
      "platform": "ios"
    }
    │
    ▼
[NotificationController] Upserts PushToken on prismDeviceCredentials
    (extends existing row — same DeviceId + TenantId + UserId)
```

### Storage: Extend `prismDeviceCredentials`

Rather than creating a separate push token table, add a `PushToken` column to the existing `prismDeviceCredentials` table. The device credential already has `DeviceId`, `TenantId`, `UserId`, and `Platform` — exactly the fields we need.

**New migration: `AddPushTokenColumn`**

```csharp
public class AddPushTokenColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismDeviceCredentials", "PushToken"))
        {
            Create.Column("PushToken")
                .OnTable("prismDeviceCredentials")
                .AsString(512)
                .Nullable()
                .Do();
        }
        return Task.CompletedTask;
    }
}
```

**Why extend, not separate?**

- A device that can receive push notifications is the same device that has biometric credentials. One row per device per tenant.
- Avoids join overhead when resolving "which devices should receive this notification."
- If a user has biometric auth disabled but notifications enabled, the device credential row still exists (just with `TokenHash` empty or `RevokedAt` set) — the `PushToken` field is independent.
- Edge case: a device may have a push token but no biometric registration. In that case, we create a minimal `prismDeviceCredentials` row with only `DeviceId`, `TenantId`, `UserId`, `Platform`, and `PushToken` populated. The biometric fields remain null/empty.

### Token Refresh Handling

Push tokens rotate. The app should re-register on every launch. The backend upserts by `(TenantId, DeviceId, UserId)` — if the token changed, it's updated. If a push fails with `NotRegistered` or `InvalidRegistration`, the delivery service nulls out the `PushToken` on that row (stale token cleanup).

### Endpoint Design

```
POST /umbraco/prism/mobile/notifications/register
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]

Request:
{
  "pushToken": "string (required, max 512)",
  "deviceId": "string (required, max 64)",
  "platform": "string (optional, 'ios' | 'android')"
}

Response: 204 No Content
```

```
DELETE /umbraco/prism/mobile/notifications/register
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]

Request:
{
  "deviceId": "string (required)"
}

Response: 204 No Content
(Nulls out PushToken — device stops receiving notifications but credential remains)
```

---

## 4. Notification Routing & Subscriptions

### Subscription Model

A subscription links a user+device to a **notification topic**. Topics are string identifiers with a type prefix:

| Topic Pattern | Meaning | Example |
|---------------|---------|---------|
| `content:{nodeId}` | A specific Umbraco content node | `content:1234` |
| `contentType:{alias}` | All content of a given type | `contentType:blogPost` |
| `global` | System-wide broadcasts | `global` |
| `custom:{key}` | Developer-defined topics | `custom:order-updates` |

### New Table: `prismNotificationSubscriptions`

```
prismNotificationSubscriptions
├── Id            (int, PK, auto-increment)
├── TenantId      (nvarchar(450), NOT NULL)
├── UserId        (nvarchar(450), NOT NULL)    -- Entra OID
├── Topic         (nvarchar(255), NOT NULL)
├── CreatedAt     (datetime, NOT NULL)
└── UNIQUE(TenantId, UserId, Topic)
```

**Index:** `IX_prismNotificationSubscriptions_TenantId_Topic` — for "find all subscribers to topic X in tenant Y."

### Why per-user, not per-device?

Subscriptions are per-user, not per-device. If a user has two devices, both receive the notification. The routing flow is:

```
Topic "content:1234" in Tenant "acme"
    │
    ▼
SELECT UserId FROM prismNotificationSubscriptions
WHERE TenantId = 'acme' AND Topic = 'content:1234'
    │
    ▼
SELECT PushToken, Platform FROM prismDeviceCredentials
WHERE TenantId = 'acme' AND UserId IN (...) AND PushToken IS NOT NULL AND RevokedAt IS NULL
    │
    ▼
Batch PushTokens → FCM send
```

### Subscription Endpoints

```
POST /umbraco/prism/mobile/notifications/subscribe
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]

Request: { "topic": "content:1234" }
Response: 204 No Content

---

DELETE /umbraco/prism/mobile/notifications/subscribe
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]

Request: { "topic": "content:1234" }
Response: 204 No Content

---

GET /umbraco/prism/mobile/notifications/subscriptions
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]

Response: {
  "subscriptions": [
    { "topic": "content:1234", "createdAt": "2026-07-14T..." },
    { "topic": "global", "createdAt": "2026-07-14T..." }
  ]
}
```

### Global Notifications

`global` is an implicit subscription. All devices with a valid `PushToken` in a tenant receive global notifications **without** needing an explicit subscription record. The router short-circuits:

```csharp
if (topic == "global")
{
    // Skip subscription lookup — target all devices in tenant
    tokens = await GetAllActiveTokensForTenantAsync(tenantId);
}
else
{
    tokens = await GetTokensByTopicAsync(tenantId, topic);
}
```

### Auto-Subscribe on Content View (Optional)

For content-driven notifications, the mobile app could auto-subscribe a user when they view a content page (with an opt-out toggle). This is a UX decision, not an architectural one — the subscription API supports it either way.

---

## 5. Content Event Hooks

### Umbraco Notification System Integration

Umbraco v17 uses a notification pattern (not to be confused with push notifications) for lifecycle events. Prism already uses this for migrations and content seeding.

**New handler: `PrismContentNotificationHandler`**

```csharp
public class PrismContentNotificationHandler
    : INotificationAsyncHandler<ContentPublishedNotification>
{
    private readonly IPrismNotificationService _notificationService;
    private readonly IOptions<PrismNotificationOptions> _options;

    public async Task HandleAsync(
        ContentPublishedNotification notification,
        CancellationToken cancellationToken)
    {
        if (!_options.Value.ContentNotifications.Enabled)
            return;

        foreach (var content in notification.PublishedEntities)
        {
            // Resolve tenant from content (via domain binding or config)
            var tenantId = ResolveTenantForContent(content);
            if (tenantId == null) continue;

            // Build notification payload
            var payload = new PrismPushPayload
            {
                Title = content.Name ?? "Content Updated",
                Body = $"{content.ContentType.Alias} was published",
                Data = new Dictionary<string, string>
                {
                    ["contentId"] = content.Id.ToString(),
                    ["contentType"] = content.ContentType.Alias,
                    ["url"] = content.GetUrl() // requires published snapshot
                }
            };

            // Send to subscribers of this content node
            await _notificationService.SendToTopicAsync(
                tenantId, $"content:{content.Id}", payload, cancellationToken);

            // Also notify subscribers of this content type
            await _notificationService.SendToTopicAsync(
                tenantId, $"contentType:{content.ContentType.Alias}", payload, cancellationToken);
        }
    }
}
```

**Registration in `PrismComposer`:**

```csharp
if (notificationOptions.Enabled)
{
    builder.AddNotificationAsyncHandler<ContentPublishedNotification,
        PrismContentNotificationHandler>();
}
```

### Tenant Resolution for Content

In a multi-tenant setup, we need to know which tenant a content node belongs to. Options:

1. **Domain binding** (recommended) — Umbraco assigns domains to content nodes. Look up which `prismTenants.hostname` matches the content's assigned domain.
2. **Explicit mapping** — A config section maps content root IDs to tenant IDs. Simpler but manual.
3. **Single-tenant mode** — If only one tenant exists, all content belongs to it. This is the common case for most Marketplace consumers.

**Recommendation:** Default to single-tenant mode (use the first/only tenant). Add domain-based resolution as an opt-in for multi-tenant deployments.

---

## 6. Backend-Triggered API

### Developer-Facing Service: `IPrismNotificationService`

This is the API that package consumers use from their own code. It must be simple, injectable, and Umbraco-idiomatic.

```csharp
public interface IPrismNotificationService
{
    /// Send a notification to all subscribers of a topic within a tenant.
    Task SendToTopicAsync(
        string tenantId,
        string topic,
        PrismPushPayload payload,
        CancellationToken cancellationToken = default);

    /// Send a notification to a specific user (all their devices) within a tenant.
    Task SendToUserAsync(
        string tenantId,
        string userId,
        PrismPushPayload payload,
        CancellationToken cancellationToken = default);

    /// Send a global notification to all devices in a tenant.
    Task SendToAllAsync(
        string tenantId,
        PrismPushPayload payload,
        CancellationToken cancellationToken = default);

    /// Send a notification to specific device IDs.
    Task SendToDevicesAsync(
        string tenantId,
        IEnumerable<string> deviceIds,
        PrismPushPayload payload,
        CancellationToken cancellationToken = default);
}
```

### Payload Model

```csharp
public class PrismPushPayload
{
    /// Notification title (shown in OS notification shade).
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    /// Notification body text.
    [Required]
    [StringLength(500)]
    public string Body { get; set; } = string.Empty;

    /// Optional image URL (shown as large image on Android, attachment on iOS).
    [StringLength(2048)]
    public string? ImageUrl { get; set; }

    /// Optional deep-link path within the app (e.g., "/dashboard/content/1234").
    [StringLength(2048)]
    public string? DeepLink { get; set; }

    /// Arbitrary key-value data delivered to the app (not displayed in notification).
    public Dictionary<string, string>? Data { get; set; }

    /// Optional notification channel/category (Android channels, iOS categories).
    [StringLength(50)]
    public string? Category { get; set; }
}
```

### Usage Example (Developer Code)

```csharp
// In a custom Umbraco controller or background task
public class OrderController : Controller
{
    private readonly IPrismNotificationService _notifications;
    private readonly IPrismContext _prismContext;

    [HttpPost]
    public async Task<IActionResult> CompleteOrder(int orderId)
    {
        // ... business logic ...

        // Notify the member
        await _notifications.SendToUserAsync(
            _prismContext.CurrentTenant!.Id.ToString(),
            User.FindFirst("oid")!.Value,
            new PrismPushPayload
            {
                Title = "Order Complete",
                Body = $"Your order #{orderId} has been dispatched.",
                DeepLink = $"/orders/{orderId}"
            });

        return Ok();
    }
}
```

### Admin API (Backoffice)

For the Umbraco backoffice, expose a management API:

```
POST /umbraco/api/v1/prism/notifications/send
[Authorize(Policy = "PrismAdmins")]

Request: {
  "tenantId": "1",
  "target": "global" | "topic:content:1234" | "user:{oid}",
  "title": "System Maintenance",
  "body": "The site will be down for maintenance at 2am.",
  "deepLink": "/announcements",
  "imageUrl": null
}

Response: {
  "devicesTargeted": 142,
  "deliveryId": "notif_abc123"
}
```

---

## 7. Demo Suggestion for Use Case 2

### Recommended Demo: "Content Expiry Watchdog"

**Scenario:** An Umbraco background task checks for content that is about to expire (using Umbraco's content expiry dates). When content expires within 24 hours, it sends a push notification to the content author and any subscribed members.

**Why this resonates with Umbraco developers:**

1. **Real problem.** Content expiry is a built-in Umbraco feature that many sites use but nobody monitors proactively. Content silently disappears when it expires — authors often don't realize until a user reports a broken page.
2. **Shows backend-triggered + scheduled.** Demonstrates `IRecurringBackgroundTask` (Umbraco's built-in scheduler) firing `IPrismNotificationService` — the exact pattern a developer would use.
3. **Tangible value.** It's not a "hello world" — it's a feature a real editorial team would want on day one.
4. **Small scope.** The demo is ~50 lines of code, fitting in a single file.

**Demo Implementation Sketch:**

```csharp
public class ContentExpiryWatchdog : IRecurringBackgroundTask
{
    private readonly IContentService _contentService;
    private readonly IPrismNotificationService _notifications;

    public TimeSpan Period => TimeSpan.FromHours(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddHours(24);
        var expiringContent = _contentService
            .GetPagedDescendants(-1, 0, 100, out _)
            .Where(c => c.ExpireDate.HasValue && c.ExpireDate.Value <= threshold
                        && c.Published);

        foreach (var content in expiringContent)
        {
            await _notifications.SendToTopicAsync(
                tenantId: ResolveTenant(content),
                topic: $"content:{content.Id}",
                payload: new PrismPushPayload
                {
                    Title = "⏰ Content Expiring Soon",
                    Body = $"\"{content.Name}\" expires in less than 24 hours.",
                    DeepLink = $"/umbraco/content/{content.Id}",
                    Category = "content-expiry"
                },
                cancellationToken);
        }
    }
}
```

**Test site integration:** The test site ships with a blog post that has an expiry date set to `DateTime.UtcNow.AddHours(12)` on seed. Within an hour of starting the test site, the watchdog fires and the member's phone buzzes. Instant "wow" moment.

### Alternative Demo Considered: "Welcome Notification on First Login"

Send a personalized push notification 5 minutes after a member's first biometric registration ("Welcome to {TenantName}! Your biometric login is now active."). Simpler but less compelling — it doesn't demonstrate a recurring backend pattern.

---

## 8. Key Design Decisions

### Decision 1: Extend `prismDeviceCredentials` vs. new push token table

**Recommendation: Extend.**

- The device credential row already exists for every registered mobile device. Adding `PushToken` as a nullable column avoids a join and keeps the "device" concept unified.
- Trade-off: tighter coupling between biometric auth and notifications. If a consumer wants notifications without biometric auth, they still need a device credential row (just with empty biometric fields).
- If the team disagrees, a separate `prismPushTokens` table with a FK to `prismDeviceCredentials.Id` is the clean alternative.

**Status: Needs team agreement.**

### Decision 2: FCM as sole shipped provider vs. provider abstraction from day one

**Recommendation: Ship FCM only, behind `IPrismPushGateway` interface.**

- The interface exists from v1, but only `FcmPushGateway` ships. Consumers who need Azure Notification Hubs or APNs direct can implement `IPrismPushGateway` and register it in DI.
- Don't build multiple provider implementations until there's demand. YAGNI.

**Status: Recommended — low risk.**

### Decision 3: Subscription storage — Prism-managed vs. FCM topics

**Recommendation: Prism-managed subscriptions (database).**

- FCM has built-in topic subscriptions, but they're device-scoped (not user-scoped) and not tenant-aware. We'd have to namespace topics as `{tenantId}_{topic}` and lose the ability to query "what has this user subscribed to?"
- Prism-managed subscriptions give us full control: per-user (not per-device), tenant-isolated, queryable, and visible in the admin API.
- Trade-off: more DB queries on notification send. Mitigated by batching and the subscription index.

**Status: Strongly recommended.**

### Decision 4: Notification delivery — synchronous vs. queued

**Recommendation: Synchronous with async batching for v1; queue-based for v2.**

- v1: `IPrismNotificationService.SendToTopicAsync()` resolves tokens, batches them (500 per FCM request), and sends in-process. Simple, no infrastructure dependency.
- v2: Add an optional `IBackgroundTaskQueue` (Umbraco's built-in) to decouple send from caller. Important for high-volume sites but overkill for v1.
- The `IPrismNotificationService` interface stays the same either way — the queueing is an internal implementation detail.

**Status: Recommended for v1 simplicity.**

### Decision 5: Mobile app changes — who generates the push scaffolding?

**Recommendation: `MobileBundleService` generates push notification bootstrap code, gated behind a config flag.**

- Same pattern as biometric auth: `MobileBundleService` already conditionally includes `biometric-bridge.ts`. Add a `notifications-bridge.ts` when `NotificationsEnabled` is true in the tenant's `MobileAppConfig`.
- The bridge handles: permission request → token registration → notification received event → deep-link navigation.
- Consumer doesn't need to write any Capacitor push code — it's generated.

**Status: Recommended — follows established pattern.**

---

## 9. Package Boundaries

### What lives in Umbraco.Prism (the NuGet package)

| Component | Description |
|-----------|-------------|
| `IPrismNotificationService` | Public API — the main developer touchpoint |
| `IPrismPushGateway` + `FcmPushGateway` | Push delivery abstraction + FCM default |
| `NotificationController` | Token registration, subscribe/unsubscribe endpoints |
| `PrismContentNotificationHandler` | Umbraco event → push notification bridge |
| `prismNotificationSubscriptions` table | Migration + NPoco schema |
| `PushToken` column on `prismDeviceCredentials` | Migration |
| `PrismNotificationOptions` | Configuration model |
| `notifications-bridge.ts` template | Capacitor push scaffolding (in MobileBundleService) |
| Admin API endpoint | Send notification from backoffice |

### What the consumer implements

| Component | Description |
|-----------|-------------|
| Firebase project setup | Create FCM project, download service account key |
| `appsettings.json` config | FCM project ID, service account key path |
| Custom notification triggers | Their own code calling `IPrismNotificationService` |
| Custom `IPrismPushGateway` (optional) | Only if they don't want FCM |
| Content expiry watchdog (optional) | Shipped as a demo/example, not auto-registered |
| Notification UI in mobile app (optional) | In-app notification center, read/unread state — consumer territory for v1 |

### What is explicitly out of scope for v1

| Item | Reason |
|------|--------|
| Web push (browser) | Different token model, different UX, adds complexity |
| In-app notification inbox | Requires local storage / sync — consumer responsibility |
| Notification analytics | Opens/clicks/delivery rates — FCM console covers basics |
| Rich notification actions | iOS notification actions (buttons) — v2 |
| Scheduled notifications | "Send at 9am tomorrow" — use Umbraco's background tasks instead |
| Multi-language notification body | Consumer can localize before calling `SendToUserAsync` |

---

## Appendix A: Database Schema Summary

### New table

```sql
CREATE TABLE prismNotificationSubscriptions (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    TenantId    NVARCHAR(450) NOT NULL,
    UserId      NVARCHAR(450) NOT NULL,
    Topic       NVARCHAR(255) NOT NULL,
    CreatedAt   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(TenantId, UserId, Topic)
);

CREATE INDEX IX_prismNotificationSubscriptions_TenantId_Topic
    ON prismNotificationSubscriptions (TenantId, Topic);
```

### Modified table

```sql
ALTER TABLE prismDeviceCredentials
    ADD COLUMN PushToken NVARCHAR(512) NULL;
```

### Migration plan addition

```csharp
// In PrismMigrationPlan.DefinePlan()
.To<AddPushTokenColumn>("add-push-token")
.To<CreateNotificationSubscriptionsTable>("add-notification-subscriptions")
```

---

## Appendix B: FCM HTTP v1 API Integration

### Authentication

FCM HTTP v1 uses OAuth2 with a Google service account. The `FcmPushGateway` loads the service account JSON, obtains a short-lived access token, and sends to:

```
POST https://fcm.googleapis.com/v1/projects/{project_id}/messages:send
Authorization: Bearer {oauth_token}
Content-Type: application/json

{
  "message": {
    "token": "{device_push_token}",
    "notification": {
      "title": "Content Updated",
      "body": "Blog post 'My Article' was published"
    },
    "data": {
      "contentId": "1234",
      "deepLink": "/blog/my-article"
    },
    "android": {
      "notification": { "channel_id": "prism_content" }
    },
    "apns": {
      "payload": {
        "aps": { "category": "PRISM_CONTENT", "sound": "default" }
      }
    }
  }
}
```

### Batch Sending

For multi-device notifications, use FCM's batch endpoint or loop with concurrency control. The `FcmPushGateway` should:

1. Chunk tokens into groups of 500 (FCM limit for multicast)
2. Send chunks concurrently (max 3 parallel requests to avoid rate limits)
3. Collect failed tokens from response
4. Return failed tokens to `NotificationDeliveryService` for stale token cleanup

### Stale Token Cleanup

When FCM returns `NOT_REGISTERED` or `INVALID_ARGUMENT` for a token:

```csharp
// In NotificationDeliveryService
foreach (var staleToken in result.FailedTokens)
{
    await NullifyPushTokenAsync(staleToken);
}
```

---

## Appendix C: Capacitor Push Notification Bridge

### Generated `notifications-bridge.ts`

```typescript
import { PushNotifications } from '@capacitor/push-notifications';

export async function initPrismPushNotifications(): Promise<void> {
  const permission = await PushNotifications.requestPermissions();
  if (permission.receive !== 'granted') return;

  await PushNotifications.register();

  PushNotifications.addListener('registration', async (token) => {
    // Send token to Prism backend
    await fetch('/umbraco/prism/mobile/notifications/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({
        pushToken: token.value,
        deviceId: localStorage.getItem('prism.deviceId'),
        platform: getPlatform()
      })
    });
  });

  PushNotifications.addListener('pushNotificationReceived', (notification) => {
    // Notification received while app is in foreground
    console.log('[Prism] Push received:', notification);
  });

  PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
    // User tapped notification — navigate to deep link
    const deepLink = action.notification.data?.deepLink;
    if (deepLink) {
      window.location.href = deepLink;
    }
  });
}

function getPlatform(): string {
  return /iPad|iPhone|iPod/.test(navigator.userAgent) ? 'ios' : 'android';
}
```

---

## Appendix D: Dependency Summary

### NuGet packages (new)

| Package | Purpose | Version Strategy |
|---------|---------|-----------------|
| `Google.Apis.FirebaseCloudMessaging.v1` | FCM HTTP v1 client | Latest stable |

### npm packages (in generated Capacitor bundle)

| Package | Purpose |
|---------|---------|
| `@capacitor/push-notifications` | Native push notification plugin |

### No new Azure dependencies

Push notifications go through FCM, not Azure. The existing Azure Key Vault integration can optionally store the FCM service account key, but it's not required.
