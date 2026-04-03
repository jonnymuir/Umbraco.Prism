# Prism Notification Service — Backend Design

**Author:** Blathers  
**Date:** 2026-03-22  
**Status:** Design Proposal  

---

## 1. Service Interface

### `IPrismNotificationService`

The public API that Umbraco site developers will call from their own code when they want to send notifications.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for sending push notifications to mobile app users via Firebase Cloud Messaging.
/// </summary>
public interface IPrismNotificationService
{
    /// <summary>
    /// Sends a notification to a specific user identified by Entra Object ID.
    /// Dispatches to all registered device tokens for that user within the current tenant context.
    /// </summary>
    /// <param name="userOid">Entra Object ID of the target user.</param>
    /// <param name="notification">Notification payload containing title, body, and optional data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, delivered count, and any failures.</returns>
    Task<NotificationResult> SendToUserAsync(
        string userOid, 
        PrismNotificationPayload notification, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to a list of users identified by Entra Object IDs.
    /// Dispatches to all registered device tokens for those users within the current tenant context.
    /// </summary>
    /// <param name="userOids">Collection of Entra Object IDs of target users.</param>
    /// <param name="notification">Notification payload containing title, body, and optional data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, delivered count, and any failures.</returns>
    Task<NotificationResult> SendToUsersAsync(
        IEnumerable<string> userOids, 
        PrismNotificationPayload notification, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all users subscribed to a specific content node.
    /// Resolves subscriptions within the current tenant context and dispatches to registered device tokens.
    /// </summary>
    /// <param name="contentKey">Umbraco content node key (GUID).</param>
    /// <param name="notification">Notification payload containing title, body, and optional data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, delivered count, and any failures.</returns>
    Task<NotificationResult> SendToSubscribersAsync(
        Guid contentKey, 
        PrismNotificationPayload notification, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all registered users in the current tenant.
    /// Use sparingly for global announcements.
    /// </summary>
    /// <param name="notification">Notification payload containing title, body, and optional data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, delivered count, and any failures.</returns>
    Task<NotificationResult> BroadcastAsync(
        PrismNotificationPayload notification, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification payload model.
/// </summary>
public class PrismNotificationPayload
{
    /// <summary>Notification title (required).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Notification body text (required).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional image URL to display in notification.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Optional custom data payload (key-value pairs) delivered to the app.
    /// Example: {"contentId": "123", "action": "viewArticle"}
    /// </summary>
    public Dictionary<string, string>? Data { get; set; }
}

/// <summary>
/// Result of a notification send operation.
/// </summary>
public class NotificationResult
{
    /// <summary>True if at least one notification was successfully sent.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Number of notifications successfully delivered to FCM.</summary>
    public int DeliveredCount { get; set; }

    /// <summary>Number of notifications that failed to send.</summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Collection of device tokens that failed permanently and should be removed.
    /// FCM returns NotFound (404) for unregistered/stale tokens.
    /// </summary>
    public List<string> StaleTokens { get; set; } = new();

    /// <summary>
    /// Optional error message if the entire operation failed (e.g., FCM unavailable).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

**Rationale:**
- **Tenant-scoped:** All operations implicitly use `IPrismContext.CurrentTenant` — no cross-tenant notification leakage.
- **User-centric:** Developers think in terms of users (Entra OIDs), not device tokens.
- **Subscription-aware:** First-class support for content-node subscriptions.
- **Result transparency:** Returns stale tokens for cleanup, delivered/failed counts for monitoring.

---

## 2. FCM Integration

### .NET Firebase SDK

**Choice:** `FirebaseAdmin` NuGet package (Google official, v3.x or later)

**Why:**
- Official Google SDK for .NET — best-supported, maintained.
- Supports server-side FCM v1 API (legacy HTTP v1 is deprecated).
- Integrates cleanly with ASP.NET Core DI.

**Installation:**
```bash
dotnet add package FirebaseAdmin --version 3.0.0
```

### FCM Credential Configuration

**Pattern:** Integrate with existing **Azure Key Vault** pattern used for biometric signing keys.

**Key Vault Secret:**
- Secret Name: `Prism--Notifications--FcmServiceAccountJson`
- Secret Value: Firebase service account JSON (download from Firebase Console → Project Settings → Service Accounts)

**Why Key Vault:**
- Consistent with existing `PrismKeyVaultConfigureOptions` pattern for biometric keys.
- No hardcoded credentials in appsettings or source control.
- Managed Identity support in production; `az login` for local dev.

**Configuration Options Class:**

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Configurable options for Firebase Cloud Messaging push notifications.
/// Bind from appsettings.json under "Prism:Notifications".
/// </summary>
public class PrismNotificationOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "Prism:Notifications";

    /// <summary>
    /// Firebase service account JSON credentials (for local dev only).
    /// In production, this should be null — use Key Vault via PrismNotificationKeyVaultConfigureOptions.
    /// </summary>
    public string? FcmServiceAccountJson { get; set; }

    /// <summary>
    /// Firebase project ID. Required for FCM initialization.
    /// Example: "my-umbraco-app-prod"
    /// </summary>
    public string? FcmProjectId { get; set; }

    /// <summary>
    /// Maximum number of device tokens to send in a single FCM batch request (default: 500, FCM max).
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// Whether to enable dry-run mode (notifications are validated but not delivered).
    /// Useful for local testing. Default: false.
    /// </summary>
    public bool DryRun { get; set; } = false;
}
```

**Key Vault Integration (new class):**

```csharp
namespace UmbracoPrism.Core.Configuration;

/// <summary>
/// Configures <see cref="PrismNotificationOptions"/> by fetching FCM credentials
/// from Azure Key Vault at options-resolution time.
/// If Prism:VaultUri is not configured, this becomes a no-op (local dev scenario).
/// </summary>
public class PrismNotificationKeyVaultConfigureOptions : IConfigureOptions<PrismNotificationOptions>
{
    private const string FcmServiceAccountSecretName = "Prism--Notifications--FcmServiceAccountJson";

    private readonly IConfiguration _configuration;

    public PrismNotificationKeyVaultConfigureOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(PrismNotificationOptions options)
    {
        var vaultUri = _configuration["Prism:VaultUri"];

        if (string.IsNullOrWhiteSpace(vaultUri))
            return; // Local dev: use appsettings value if provided

        if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"Prism: VaultUri '{vaultUri}' must be a valid HTTPS URI.");

        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(0.8),
                MaxDelay = TimeSpan.FromSeconds(8),
                Mode = RetryMode.Exponential
            }
        };

        var client = new SecretClient(uri, new DefaultAzureCredential(), clientOptions);

        try
        {
            var fcmJson = client.GetSecret(FcmServiceAccountSecretName).Value.Value;
            options.FcmServiceAccountJson = fcmJson;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Not a hard failure: allows zero-config deployments where notifications are disabled
            options.FcmServiceAccountJson = null;
        }
        catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            throw new InvalidOperationException(
                $"Prism: Key Vault access denied for FCM credentials. " +
                "Ensure the application identity has 'Get' permission on secrets.",
                ex);
        }
    }
}
```

**appsettings.json (local dev example):**

```json
{
  "Prism": {
    "VaultUri": null,
    "Notifications": {
      "FcmProjectId": "umbraco-prism-dev",
      "FcmServiceAccountJson": "{...paste Firebase service account JSON...}",
      "DryRun": true
    }
  }
}
```

**appsettings.Production.json:**

```json
{
  "Prism": {
    "VaultUri": "https://myprismvault.vault.azure.net/",
    "Notifications": {
      "FcmProjectId": "umbraco-prism-prod"
    }
  }
}
```

**Rationale:**
- Zero-config path for sites that don't use notifications (missing FCM secret = service logs warning but doesn't crash).
- Local dev: paste service account JSON directly in appsettings (never commit to source control).
- Production: Key Vault fetch at startup, consistent with existing biometric pattern.

---

## 3. Device Token Storage

### Recommended Approach: **Custom Database Table**

**Why not Umbraco Member properties?**
- ❌ Umbraco Members are optional in many Prism deployments (stateless OIDC = Entra-only users).
- ❌ Multi-device support awkward (one property = one value; arrays in JSON = brittle).
- ❌ No relational querying (subscription joins would require JSON deserialization).

**Why extend `prismDeviceCredentials` instead?**
- ✅ One unified row per device per user per tenant (whether it has biometric, push, or both).
- ✅ Reuses the existing table's tenant isolation, user binding, and lifecycle management.
- ✅ Reduces schema complexity — developers work with a single device credential model.
- ✅ No separate migration or schema management needed — just one new column.

### Storage: Extend `prismDeviceCredentials`

Rather than creating a separate `prismDeviceTokens` table, add a `PushToken` column to the existing `prismDeviceCredentials` table. The device credential already has `DeviceId`, `TenantId`, `UserId`, and `Platform` — exactly the fields we need.

**New migration: `AddPushTokenColumn`**

```csharp
public class AddPushTokenColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    public override async Task MigrateAsync(IAlterSchemaBuilder schema)
    {
        if (!ColumnExists("prismDeviceCredentials", "PushToken"))
        {
            Create.Column("PushToken")
                .OnTable("prismDeviceCredentials")
                .AsString(512)
                .Nullable()
                .Do();
        }

        await Task.CompletedTask;
    }
}
```

**Updated `PrismDeviceCredentialsSchema` model:**

Add this property to the existing model:

```csharp
/// <summary>
/// Firebase Cloud Messaging device registration token.
/// Nullable — a device may have biometric auth without push, or push without biometric.
/// Example: "cXYz123...ABC" (FCM tokens are ~152 chars, use 512 for safety).
/// </summary>
[Column("PushToken")]
[Length(512)]
[NullSetting(NullSetting = NullSettings.Null)]
[Index(IndexTypes.NonClustered, Name = "IX_PrismDeviceCredentials_PushToken")]
public string? PushToken { get; set; }
```

**Edge cases:**
- If a user has biometric auth disabled but notifications enabled, the device credential row still exists (just with `TokenHash` empty or `RevokedAt` set) — the `PushToken` field is independent.
- Edge case: a device may have a push token but no biometric registration. In that case, we create a minimal `prismDeviceCredentials` row with only `DeviceId`, `TenantId`, `UserId`, `Platform`, and `PushToken` populated. The biometric fields remain null/empty.

**Updates `PrismMigrationPlan`:**

```csharp
protected override void DefinePlan()
{
    Definitions.Add<AddPushTokenColumn>();
    // ... existing migrations ...
}
```

**Update `PrismMigrationPlan`:**

```csharp
protected override void DefinePlan()
{
    To<CreatePrismTables>("initial-state")
    // ... existing migrations ...
    .To<AddAllowBiometricLoginColumn>("add-allow-biometric-login")
    .To<CreatePrismDeviceTokensTable>("add-device-tokens");
}
```

**Multi-device handling:**
- Each user can have **multiple rows** (one per device).
- `(TenantId, UserId)` queries return all devices for that user.
- `(TenantId, DeviceToken)` composite index for fast lookup/upsert.

---

## 4. Notification Subscription Model

### Subscribed Notifications (Opt-in to Content Nodes)

**Use Case:**
- User subscribes to "Company News" content node (Umbraco DocType).
- When new content is published under that node, subscribed users receive a push notification.

### Schema: `prismNotificationSubscriptions`

```csharp
namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismNotificationSubscriptions table.
/// Stores user opt-in subscriptions to specific Umbraco content nodes.
/// </summary>
[TableName("prismNotificationSubscriptions")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismNotificationSubscriptionSchema
{
    /// <summary>Unique identifier for the subscription record.</summary>
    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Tenant ID this subscription belongs to (tenant isolation).</summary>
    [Column("TenantId")]
    [Index(IndexTypes.NonClustered, Name = "IX_PrismSubscriptions_TenantId")]
    public int TenantId { get; set; }

    /// <summary>Entra Object ID of the user who subscribed.</summary>
    [Column("UserId")]
    [Length(450)]
    [Index(IndexTypes.NonClustered, Name = "IX_PrismSubscriptions_UserId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Umbraco content node key (GUID) that the user is subscribed to.
    /// </summary>
    [Column("ContentKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_PrismSubscriptions_ContentKey", ForColumns = "ContentKey,TenantId")]
    public Guid ContentKey { get; set; }

    /// <summary>UTC datetime when the subscription was created.</summary>
    [Column("SubscribedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime SubscribedAt { get; set; }

    /// <summary>
    /// Composite unique constraint: one user can subscribe to a content node only once per tenant.
    /// Prevents duplicate subscriptions.
    /// </summary>
    [Index(IndexTypes.UniqueNonClustered, Name = "UX_PrismSubscriptions_UserContent", ForColumns = "TenantId,UserId,ContentKey")]
    public void EnsureUniqueSubscription() { }
}
```

**Migration:**

```csharp
public class CreatePrismNotificationSubscriptionsTable : MigrationBase
{
    public CreatePrismNotificationSubscriptionsTable(IMigrationContext context) : base(context) { }

    protected override void Migrate()
    {
        if (TableExists("prismNotificationSubscriptions"))
            return;

        Create.Table<PrismNotificationSubscriptionSchema>().Do();
    }
}
```

**Query Pattern (fetch subscribers for a content node):**

```csharp
using var scope = _databaseFactory.CreateScope();
var db = scope.Database;

var subscribers = db.Fetch<PrismNotificationSubscriptionSchema>(
    "WHERE TenantId = @tenantId AND ContentKey = @contentKey",
    new { tenantId = currentTenant.Id, contentKey = contentNodeKey });

var userOids = subscribers.Select(s => s.UserId).Distinct().ToList();
```

### Global Notifications

**No subscription table needed.**  
`BroadcastAsync` simply queries all device tokens for the current tenant:

```csharp
var allTokens = db.Fetch<PrismDeviceTokenSchema>(
    "WHERE TenantId = @tenantId",
    new { tenantId = currentTenant.Id });
```

**Rationale:**
- Subscription table enables **opt-in granularity** (user controls which content nodes they follow).
- Unique constraint prevents duplicate subscriptions.
- Efficient lookups via `(TenantId, ContentKey)` composite index.

---

## 5. Content Event Integration

### Umbraco Notification Handler Pattern

Umbraco v13+ uses `INotificationHandler<T>` for content lifecycle events.

**Relevant Events:**
- `ContentPublishedNotification` — Content item published (most common trigger).
- `ContentUnpublishedNotification` — Content unpublished (optional cleanup).
- `ContentDeletedNotification` — Content deleted (cleanup subscriptions).

**Example Handler:**

```csharp
namespace UmbracoPrism.Core.Notifications;

/// <summary>
/// Sends push notifications to subscribed users when content is published.
/// Checks for a custom property on the content type to determine if notifications are enabled.
/// </summary>
public class PrismContentPublishedNotificationHandler : INotificationAsyncHandler<ContentPublishedNotification>
{
    private readonly IPrismNotificationService _notificationService;
    private readonly IContentService _contentService;
    private readonly IPrismContext _prismContext;
    private readonly ILogger<PrismContentPublishedNotificationHandler> _logger;

    public PrismContentPublishedNotificationHandler(
        IPrismNotificationService notificationService,
        IContentService contentService,
        IPrismContext prismContext,
        ILogger<PrismContentPublishedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _contentService = contentService;
        _prismContext = prismContext;
        _logger = logger;
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        // Only process if we have a tenant context
        if (_prismContext.CurrentTenant == null)
            return;

        foreach (var entity in notification.PublishedEntities)
        {
            // Check if this content type has notification enabled
            // (Custom property: "sendPushNotification", boolean checkbox)
            var shouldNotify = entity.GetValue<bool>("sendPushNotification");
            if (!shouldNotify)
                continue;

            // Extract notification content from content properties
            var title = entity.GetValue<string>("notificationTitle") 
                ?? entity.Name; // Fallback to content name

            var body = entity.GetValue<string>("notificationBody") 
                ?? "New content has been published.";

            var imageUrl = entity.GetValue<string>("notificationImage");

            var payload = new PrismNotificationPayload
            {
                Title = title,
                Body = body,
                ImageUrl = imageUrl,
                Data = new Dictionary<string, string>
                {
                    { "contentKey", entity.Key.ToString() },
                    { "contentId", entity.Id.ToString() },
                    { "contentType", entity.ContentType.Alias },
                    { "action", "viewContent" }
                }
            };

            try
            {
                // Send to all users subscribed to this content node
                var result = await _notificationService.SendToSubscribersAsync(
                    entity.Key, 
                    payload, 
                    cancellationToken);

                _logger.LogInformation(
                    "Push notification sent for published content {ContentKey}: {DeliveredCount} delivered, {FailedCount} failed",
                    entity.Key, result.DeliveredCount, result.FailedCount);
            }
            catch (Exception ex)
            {
                // Log but don't block content publishing if notifications fail
                _logger.LogError(ex, 
                    "Failed to send push notification for published content {ContentKey}", 
                    entity.Key);
            }
        }
    }
}
```

**Registration in `PrismComposer`:**

```csharp
builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedNotificationHandler>();
```

**Optional: Content Type Seeder**

Add custom properties (`sendPushNotification`, `notificationTitle`, `notificationBody`, `notificationImage`) to content types that should support push notifications. This could be done via a `PrismNotificationContentTypeSeeder` similar to `PrismContentTypeSeeder`.

**Rationale:**
- **Non-blocking:** Notification failures don't prevent content publishing (try/catch wrapper).
- **Tenant-scoped:** Uses `IPrismContext.CurrentTenant` for isolation.
- **Opt-in per content item:** Editors control whether a specific publish triggers notifications via checkbox.
- **Extensible:** Developers can add custom handlers for other events (unpublish, delete, etc.).

---

## 6. Scheduled Notification Trigger

### Umbraco Hosted Services Pattern

Use `IRecurringBackgroundTask` for scheduled notifications (e.g., daily digest).

**Example: Daily Digest Task**

```csharp
namespace UmbracoPrism.Core.HostedServices;

/// <summary>
/// Sends a daily digest notification to all subscribed users.
/// Runs every 24 hours at 9:00 AM UTC.
/// </summary>
public class PrismDailyDigestTask : IRecurringBackgroundTask
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PrismDailyDigestTask> _logger;

    public PrismDailyDigestTask(
        IServiceProvider serviceProvider,
        ILogger<PrismDailyDigestTask> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task RunJobAsync()
    {
        _logger.LogInformation("PrismDailyDigestTask started");

        // Resolve scoped services inside the task (background tasks are singleton)
        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<IPrismNotificationService>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        // Fetch all tenants (multi-tenant iteration)
        var tenants = await tenantService.GetAllTenantsAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                // Set tenant context manually (no HTTP request context in background tasks)
                // Option 1: Iterate per tenant and query subscriptions/tokens scoped to that tenant
                // Option 2: Use a tenant-aware overload on IPrismNotificationService

                var payload = new PrismNotificationPayload
                {
                    Title = "Daily Digest",
                    Body = "Check out today's top stories from your organization.",
                    Data = new Dictionary<string, string>
                    {
                        { "action", "viewDigest" },
                        { "date", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                    }
                };

                // Broadcast to all users in this tenant
                // (Alternative: query subscriptions to a "digest" topic)
                var result = await notificationService.BroadcastAsync(payload);

                _logger.LogInformation(
                    "Daily digest sent to tenant {TenantId}: {DeliveredCount} delivered",
                    tenant.Id, result.DeliveredCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to send daily digest for tenant {TenantId}", 
                    tenant.Id);
            }
        }
    }

    // Run once per day (24 hours)
    public TimeSpan Period => TimeSpan.FromHours(24);

    // Delay 30 seconds after startup before first run
    public TimeSpan Delay => TimeSpan.FromSeconds(30);
}
```

**Registration in `PrismComposer`:**

```csharp
// In Compose method:
builder.Services.AddHostedService<RecurringBackgroundTaskHostedService<PrismDailyDigestTask>>();
builder.Services.AddSingleton<IRecurringBackgroundTask, PrismDailyDigestTask>();
```

**Tenant Context Challenge:**

Background tasks don't have `IPrismContext` (no HTTP request). Two solutions:

1. **Iterate tenants explicitly** (shown above): Fetch all tenants, query device tokens scoped by `TenantId`.
2. **Add tenant-aware service methods:**

```csharp
// Overload on IPrismNotificationService
Task<NotificationResult> BroadcastAsync(
    int tenantId, 
    PrismNotificationPayload notification, 
    CancellationToken cancellationToken = default);
```

**Rationale:**
- Standard Umbraco pattern for scheduled tasks.
- Decoupled from HTTP request lifecycle.
- Scoped service resolution ensures proper DI lifetime management.

---

## 7. API Surface for Mobile Apps

### Endpoints

```csharp
namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// API endpoints for mobile apps to register device tokens and manage notification subscriptions.
/// All endpoints require authentication via biometric JWT (PrismMemberCookie).
/// </summary>
[Route("umbraco/prism/notifications")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class NotificationController : Controller
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IPrismContext _prismContext;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        IUmbracoDatabaseFactory databaseFactory,
        IPrismContext prismContext,
        ILogger<NotificationController> logger)
    {
        _databaseFactory = databaseFactory;
        _prismContext = prismContext;
        _logger = logger;
    }

    /// <summary>
    /// Registers a device token for push notifications.
    /// Upserts if token already exists for this user/tenant.
    /// </summary>
    /// <param name="request">Device token registration request.</param>
    [HttpPost("register")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceTokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenant = _prismContext.CurrentTenant;
        if (tenant == null)
            return BadRequest(new { error = "No tenant context available." });

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
            return Unauthorized(new { error = "User identity could not be determined." });

        using var scope = _databaseFactory.CreateScope();
        var db = scope.Database;

        // Upsert: check if token already exists for this tenant
        var existing = db.FirstOrDefault<PrismDeviceTokenSchema>(
            "WHERE TenantId = @tenantId AND DeviceToken = @deviceToken",
            new { tenantId = tenant.Id, deviceToken = request.DeviceToken });

        if (existing != null)
        {
            // Update existing record
            existing.UserId = userOid; // User may have changed device ownership
            existing.Platform = request.Platform;
            existing.DeviceName = request.DeviceName;
            db.Update(existing);
        }
        else
        {
            // Insert new record
            var newToken = new PrismDeviceTokenSchema
            {
                TenantId = tenant.Id,
                UserId = userOid,
                DeviceToken = request.DeviceToken,
                Platform = request.Platform,
                DeviceName = request.DeviceName,
                RegisteredAt = DateTime.UtcNow
            };
            db.Insert(newToken);
        }

        scope.Complete();

        _logger.LogInformation(
            "Device token registered for user {UserId} on tenant {TenantId}",
            userOid, tenant.Id);

        return Ok(new { message = "Device token registered successfully." });
    }

    /// <summary>
    /// Subscribes the authenticated user to push notifications for a specific content node.
    /// </summary>
    /// <param name="request">Subscription request containing content key.</param>
    [HttpPost("subscribe")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenant = _prismContext.CurrentTenant;
        if (tenant == null)
            return BadRequest(new { error = "No tenant context available." });

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
            return Unauthorized(new { error = "User identity could not be determined." });

        using var scope = _databaseFactory.CreateScope();
        var db = scope.Database;

        // Check if already subscribed
        var existing = db.FirstOrDefault<PrismNotificationSubscriptionSchema>(
            "WHERE TenantId = @tenantId AND UserId = @userId AND ContentKey = @contentKey",
            new { tenantId = tenant.Id, userId = userOid, contentKey = request.ContentKey });

        if (existing != null)
            return Ok(new { message = "Already subscribed." });

        // Insert new subscription
        var subscription = new PrismNotificationSubscriptionSchema
        {
            TenantId = tenant.Id,
            UserId = userOid,
            ContentKey = request.ContentKey,
            SubscribedAt = DateTime.UtcNow
        };
        db.Insert(subscription);

        scope.Complete();

        _logger.LogInformation(
            "User {UserId} subscribed to content {ContentKey} on tenant {TenantId}",
            userOid, request.ContentKey, tenant.Id);

        return Ok(new { message = "Subscribed successfully." });
    }

    /// <summary>
    /// Unsubscribes the authenticated user from push notifications for a specific content node.
    /// </summary>
    /// <param name="request">Unsubscribe request containing content key.</param>
    [HttpPost("unsubscribe")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenant = _prismContext.CurrentTenant;
        if (tenant == null)
            return BadRequest(new { error = "No tenant context available." });

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
            return Unauthorized(new { error = "User identity could not be determined." });

        using var scope = _databaseFactory.CreateScope();
        var db = scope.Database;

        // Delete subscription
        db.Execute(
            "DELETE FROM prismNotificationSubscriptions WHERE TenantId = @tenantId AND UserId = @userId AND ContentKey = @contentKey",
            new { tenantId = tenant.Id, userId = userOid, contentKey = request.ContentKey });

        scope.Complete();

        _logger.LogInformation(
            "User {UserId} unsubscribed from content {ContentKey} on tenant {TenantId}",
            userOid, request.ContentKey, tenant.Id);

        return Ok(new { message = "Unsubscribed successfully." });
    }

    /// <summary>
    /// Returns all content nodes the authenticated user is subscribed to.
    /// </summary>
    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(SubscriptionsResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSubscriptions()
    {
        var tenant = _prismContext.CurrentTenant;
        if (tenant == null)
            return BadRequest(new { error = "No tenant context available." });

        var userOid = User.FindFirst("oid")?.Value
            ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(userOid))
            return Unauthorized(new { error = "User identity could not be determined." });

        using var scope = _databaseFactory.CreateScope();
        var db = scope.Database;

        var subscriptions = db.Fetch<PrismNotificationSubscriptionSchema>(
            "WHERE TenantId = @tenantId AND UserId = @userId",
            new { tenantId = tenant.Id, userId = userOid });

        var response = new SubscriptionsResponse
        {
            ContentKeys = subscriptions.Select(s => s.ContentKey).ToList()
        };

        return Ok(response);
    }
}

// Request/Response Models

public class RegisterDeviceTokenRequest
{
    public string DeviceToken { get; set; } = string.Empty;
    public string? Platform { get; set; } // "ios" or "android"
    public string? DeviceName { get; set; }
}

public class SubscribeRequest
{
    public Guid ContentKey { get; set; }
}

public class UnsubscribeRequest
{
    public Guid ContentKey { get; set; }
}

public class SubscriptionsResponse
{
    public List<Guid> ContentKeys { get; set; } = new();
}
```

**Authentication:**
- All endpoints require `PrismMemberCookie` (biometric JWT exchange result).
- Consistent with existing `BiometricController` pattern.

**Rationale:**
- **Simple CRUD operations** for device tokens and subscriptions.
- **Idempotent:** `register` upserts; `subscribe` checks for duplicates.
- **Tenant-scoped:** All queries filtered by `TenantId` from `IPrismContext`.

---

## 8. Error Handling & Resilience

### FCM Temporary Unavailability

**Problem:** FCM API is down or network is unreachable.

**Solution:** Polly retry policy (consistent with existing `PrismTokenRefreshService` pattern).

**Implementation:**

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Wraps Firebase Cloud Messaging client with retry/circuit breaker resilience.
/// </summary>
public class PrismNotificationService : IPrismNotificationService
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IPrismContext _prismContext;
    private readonly FirebaseMessaging _fcmClient;
    private readonly ILogger<PrismNotificationService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public PrismNotificationService(
        IUmbracoDatabaseFactory databaseFactory,
        IPrismContext prismContext,
        IOptions<PrismNotificationOptions> options,
        ILogger<PrismNotificationService> logger)
    {
        _databaseFactory = databaseFactory;
        _prismContext = prismContext;
        _logger = logger;

        // Initialize Firebase app if credentials are available
        if (!string.IsNullOrWhiteSpace(options.Value.FcmServiceAccountJson))
        {
            var app = FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(options.Value.FcmServiceAccountJson),
                ProjectId = options.Value.FcmProjectId
            });
            _fcmClient = FirebaseMessaging.GetMessaging(app);
        }

        // Polly resilience pipeline: Retry + Circuit Breaker
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<FirebaseMessagingException>(ex =>
                    ex.MessagingErrorCode == MessagingErrorCode.Unavailable ||
                    ex.MessagingErrorCode == MessagingErrorCode.Internal)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromMinutes(2),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromMinutes(1)
            })
            .Build();
    }

    // ... service methods use _resiliencePipeline.ExecuteAsync(...)
}
```

**Configuration Options (add to `PrismNotificationOptions`):**

```csharp
/// <summary>Retry configuration for FCM requests.</summary>
public int MaxRetryAttempts { get; set; } = 3;

/// <summary>Initial retry delay in seconds (exponential backoff).</summary>
public double RetryDelaySeconds { get; set; } = 1.0;

/// <summary>Circuit breaker failure ratio threshold (0.0-1.0).</summary>
public double CircuitBreakerFailureRatio { get; set; } = 0.5;

/// <summary>Circuit breaker sampling duration in minutes.</summary>
public double CircuitBreakerSamplingMinutes { get; set; } = 2.0;

/// <summary>Circuit breaker break duration in minutes.</summary>
public double CircuitBreakerBreakMinutes { get; set; } = 1.0;
```

### Stale Token Cleanup

**Problem:** FCM returns `404 NotFound` for unregistered/expired tokens.

**Solution:**
1. `NotificationResult.StaleTokens` returns list of failed tokens.
2. Service automatically deletes stale tokens after FCM rejects them.

**Implementation:**

```csharp
private async Task<NotificationResult> SendBatchAsync(
    List<string> deviceTokens, 
    PrismNotificationPayload notification,
    CancellationToken cancellationToken)
{
    var result = new NotificationResult();

    try
    {
        var message = new MulticastMessage
        {
            Tokens = deviceTokens,
            Notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = notification.Title,
                Body = notification.Body,
                ImageUrl = notification.ImageUrl
            },
            Data = notification.Data
        };

        var batchResponse = await _resiliencePipeline.ExecuteAsync(async ct =>
            await _fcmClient.SendMulticastAsync(message, ct), cancellationToken);

        result.DeliveredCount = batchResponse.SuccessCount;
        result.FailedCount = batchResponse.FailureCount;

        // Collect stale tokens (FCM rejected as unregistered)
        for (int i = 0; i < batchResponse.Responses.Count; i++)
        {
            var response = batchResponse.Responses[i];
            if (!response.IsSuccess && 
                response.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                var staleToken = deviceTokens[i];
                result.StaleTokens.Add(staleToken);

                _logger.LogInformation(
                    "FCM token unregistered, marking for deletion: {Token}",
                    staleToken.Substring(0, Math.Min(8, staleToken.Length)) + "...");
            }
        }

        // Auto-cleanup stale tokens
        if (result.StaleTokens.Any())
        {
            await DeleteStaleTokensAsync(result.StaleTokens, cancellationToken);
        }

        result.IsSuccess = result.DeliveredCount > 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "FCM batch send failed");
        result.ErrorMessage = ex.Message;
    }

    return result;
}

private async Task DeleteStaleTokensAsync(List<string> staleTokens, CancellationToken cancellationToken)
{
    using var scope = _databaseFactory.CreateScope();
    var db = scope.Database;

    foreach (var token in staleTokens)
    {
        db.Execute(
            "UPDATE prismDeviceCredentials SET PushToken = NULL WHERE PushToken = @pushToken",
            new { pushToken = token });
    }

    scope.Complete();

    _logger.LogInformation("Nulled {Count} stale push tokens", staleTokens.Count);
}
```

### Fire-and-Forget vs. Queued

**Decision: Fire-and-Forget (with resilience)**

**Rationale:**
- ✅ Simpler implementation (no queue infrastructure).
- ✅ Polly retry handles transient failures (3 attempts + circuit breaker).
- ✅ Non-blocking: Content publishing doesn't wait for notification success.
- ✅ Stale tokens cleaned up automatically on failure.

**When to Consider Queueing (future enhancement):**
- High-volume broadcasts (>10,000 users).
- Guaranteed delivery requirements (notification must eventually succeed).
- Complex delivery scheduling (throttling, time-windowed delivery).

**If queueing is needed later:**
- Use Umbraco's built-in `BackgroundJobManager` or external queue (Azure Service Bus, Hangfire).

---

## 9. Composer Registration

### `PrismComposer.Compose` Updates

```csharp
public void Compose(IUmbracoBuilder builder)
{
    // ... existing services ...

    // 8. Notification Services
    builder.Services.Configure<PrismNotificationOptions>(
        builder.Config.GetSection(PrismNotificationOptions.SectionName));
    builder.Services.ConfigureOptions<PrismNotificationKeyVaultConfigureOptions>();
    builder.Services.AddSingleton<IPrismNotificationService, PrismNotificationService>();

    // 9. Notification Event Handlers
    builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedNotificationHandler>();

    // 10. Optional: Scheduled Notification Tasks
    // builder.Services.AddHostedService<RecurringBackgroundTaskHostedService<PrismDailyDigestTask>>();
    // builder.Services.AddSingleton<IRecurringBackgroundTask, PrismDailyDigestTask>();
}
```

### Zero-Config Path

**Scenario:** Developer installs Umbraco.Prism NuGet but doesn't configure FCM.

**Behavior:**
1. `PrismNotificationKeyVaultConfigureOptions` attempts to fetch FCM secret.
2. If secret doesn't exist (404), `FcmServiceAccountJson` remains `null`.
3. `PrismNotificationService` constructor checks if credentials are null:
   - If null: Logs warning, returns no-op results (`IsSuccess = false, ErrorMessage = "FCM not configured"`).
   - If valid: Initializes `FirebaseApp`.

**Implementation:**

```csharp
public PrismNotificationService(
    IUmbracoDatabaseFactory databaseFactory,
    IPrismContext prismContext,
    IOptions<PrismNotificationOptions> options,
    ILogger<PrismNotificationService> logger)
{
    _databaseFactory = databaseFactory;
    _prismContext = prismContext;
    _logger = logger;

    if (string.IsNullOrWhiteSpace(options.Value.FcmServiceAccountJson))
    {
        _logger.LogWarning(
            "PrismNotificationService: FCM credentials not configured. " +
            "Notification methods will return no-op results. " +
            "To enable: add Prism--Notifications--FcmServiceAccountJson secret to Key Vault.");
        _fcmClient = null; // No-op mode
        return;
    }

    // Normal initialization...
}

public async Task<NotificationResult> SendToUserAsync(
    string userOid, 
    PrismNotificationPayload notification, 
    CancellationToken cancellationToken = default)
{
    if (_fcmClient == null)
    {
        return new NotificationResult
        {
            IsSuccess = false,
            ErrorMessage = "Notification service is not configured (missing FCM credentials)."
        };
    }

    // Normal send logic...
}
```

**Rationale:**
- **Graceful degradation:** Package installs without crashing if FCM is not set up.
- **Clear messaging:** Warning logs guide developers to configuration steps.
- **Opt-in:** Only sites that configure FCM secrets activate notifications.

---

## Summary of Key Decisions

| **Aspect**                  | **Decision**                                                                 | **Rationale**                                                                                     |
|-----------------------------|-------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| **Service Interface**       | `IPrismNotificationService` with user/subscriber/broadcast methods            | Developer-friendly abstraction; hides token/FCM complexity                                        |
| **FCM SDK**                 | `FirebaseAdmin` NuGet (Google official)                                       | Best-supported, future-proof, server-side v1 API                                                  |
| **Credential Storage**      | Azure Key Vault (via `PrismNotificationKeyVaultConfigureOptions`)             | Consistent with existing biometric pattern; no hardcoded secrets                                  |
| **Config Location**         | New `PrismNotificationOptions` class (`Prism:Notifications` section)          | Decoupled from biometric options; room for notification-specific settings                         |
| **Device Token Storage**    | Extend `prismDeviceCredentials` with `PushToken` column                      | Unified device model; reuses existing tenant isolation and lifecycle; simpler schema              |
| **Subscription Model**      | Custom table `prismNotificationSubscriptions` (user + content key mapping)    | Opt-in granularity; unique constraint prevents duplicates; fast lookups                           |
| **Event Integration**       | `INotificationAsyncHandler<ContentPublishedNotification>`                     | Standard Umbraco pattern; non-blocking; opt-in via content property                               |
| **Scheduled Tasks**         | `IRecurringBackgroundTask` for digest/cron notifications                      | Umbraco-native pattern; scoped service resolution                                                 |
| **API Endpoints**           | `/umbraco/prism/notifications/*` (register, subscribe, unsubscribe, list)     | RESTful; biometric JWT auth; tenant-scoped                                                        |
| **Error Handling**          | Polly retry + circuit breaker (FCM transient failures)                        | Consistent with existing token refresh pattern; resilient                                         |
| **Stale Token Cleanup**     | Auto-null push tokens when FCM returns `Unregistered` error                   | Self-healing; prevents wasted sends to dead tokens; device credential remains for biometric/audit |
| **Delivery Model**          | Fire-and-forget with resilience (no queue)                                    | Simpler; sufficient for most use cases; can add queue later if needed                             |
| **Zero-Config Path**        | Service logs warning if FCM not configured; returns no-op results             | Graceful degradation; doesn't block package installation                                          |
| **Composer Registration**   | Services + handlers registered in `PrismComposer.Compose`                     | Centralized DI; follows existing pattern                                                          |

---

## Next Steps (Implementation Phases)

### Phase 1: Foundation
1. Create `PrismNotificationOptions` and `PrismNotificationKeyVaultConfigureOptions`.
2. Add `PushToken` column to `prismDeviceCredentials` and `prismNotificationSubscriptions` table + migrations.
3. Implement `IPrismNotificationService` / `PrismNotificationService` (core send logic).
4. Register services in `PrismComposer`.

### Phase 2: API Endpoints
5. Create `NotificationController` with register/subscribe/unsubscribe endpoints.
6. Add request/response models.

### Phase 3: Content Integration
7. Implement `PrismContentPublishedNotificationHandler`.
8. Add custom content type properties (via seeder or manual docs).

### Phase 4: Scheduled Tasks (Optional)
9. Implement `PrismDailyDigestTask` or other recurring notification tasks.

### Phase 5: Testing & Docs
10. Unit tests for service logic (mock FCM client).
11. Integration tests for API endpoints.
12. Update README with notification setup instructions (Firebase Console, Key Vault secrets).

---

## Open Questions for Product Owner

1. **Content Type Seeding:** Should we auto-add notification properties to existing content types, or document manual setup?
2. **Subscription UI:** Do we need a backoffice UI for viewing/managing user subscriptions, or is API-only sufficient?
3. **Rate Limiting:** Should we rate-limit notification sends per tenant (e.g., max 1000/hour)?
4. **Analytics:** Do we need delivery metrics (dashboard, logs, telemetry)?
5. **Multi-language:** How should notification content be localized (Umbraco variants, custom logic)?

---

**End of Design Document**
