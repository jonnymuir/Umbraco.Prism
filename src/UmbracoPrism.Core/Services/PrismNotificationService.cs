using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Implements <see cref="IPrismNotificationService"/> using Firebase Cloud Messaging (FCM), through
/// <see cref="PrismFirebase"/>.
/// </summary>
public class PrismNotificationService : IPrismNotificationService
{
    private const int FcmBatchSize = 500;

    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ILogger<PrismNotificationService> _logger;
    private readonly FirebaseMessaging? _messaging;

    public PrismNotificationService(
        IUmbracoDatabaseFactory databaseFactory,
        PrismFirebase firebase,
        ILogger<PrismNotificationService> logger)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _messaging = firebase.Messaging;
    }

    // ── Token registration ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task RegisterDeviceTokenAsync(string userId, string tenantId, string pushToken, CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismDeviceCredentialSchema>(
            "WHERE UserId = @0 AND TenantId = @1", userId, tenantId);

        if (existing != null)
        {
            db.Execute(
                "UPDATE prismDeviceCredentials SET PushToken = @0 WHERE UserId = @1 AND TenantId = @2",
                pushToken, userId, tenantId);
        }
        else
        {
            // No device credential row yet — create a minimal stub so the push token can be stored.
            // The biometric flow will fill in the remaining columns when the user registers biometrics.
            var row = new PrismDeviceCredentialSchema
            {
                UserId = userId,
                TenantId = tenantId,
                PushToken = pushToken,
                DeviceId = $"push-only-{userId}",
                TokenHash = string.Empty,
                ExpiresAt = DateTime.UtcNow.AddYears(10),
                RegisteredAt = DateTime.UtcNow
            };
            db.Insert(row);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnregisterDeviceTokenAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();
        // Journey statuses reach the device through its push token, so they go with it.
        db.Execute(
            "UPDATE prismDeviceCredentials SET PushToken = NULL, JourneyStatusPlatform = NULL, LiveActivityStartToken = NULL WHERE UserId = @0 AND TenantId = @1",
            userId, tenantId);
        db.Execute("DELETE FROM prismJourneyStatuses WHERE UserId = @0 AND TenantId = @1", userId, tenantId);
        return Task.CompletedTask;
    }

    // ── Genre subscriptions ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task SubscribeToGenreAsync(string userId, string tenantId, string genre, CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismNotificationSubscriptionSchema>(
            "WHERE UserId = @0 AND TenantId = @1 AND Genre = @2", userId, tenantId, genre);

        if (existing == null)
        {
            db.Insert(new PrismNotificationSubscriptionSchema
            {
                UserId = userId,
                TenantId = tenantId,
                Genre = genre,
                CreatedAt = DateTime.UtcNow
            });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsubscribeFromGenreAsync(string userId, string tenantId, string genre, CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();
        db.Execute(
            "DELETE FROM prismNotificationSubscriptions WHERE UserId = @0 AND TenantId = @1 AND Genre = @2",
            userId, tenantId, genre);
        return Task.CompletedTask;
    }

    // ── Notification delivery ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendNotificationToGenreSubscribersAsync(
        string tenantId,
        string genre,
        string title,
        string body,
        CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();

        // Resolve all subscriber userIds for this genre + tenant
        var subscriptions = db.Fetch<PrismNotificationSubscriptionSchema>(
            "WHERE TenantId = @0 AND Genre = @1", tenantId, genre);

        if (subscriptions.Count == 0)
            return;

        var userIds = subscriptions.Select(s => s.UserId).Distinct().ToList();

        // Collect push tokens for those users within the tenant
        var tokens = GetPushTokensForUsers(db, tenantId, userIds);

        await FanOutAsync(db, tenantId, tokens, title, body, deepLinkPath: null, ct);
    }

    /// <inheritdoc/>
    public async Task SendNotificationToAllMembersAsync(
        string tenantId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();

        var tokens = db.Fetch<string>(
            "SELECT PushToken FROM prismDeviceCredentials WHERE TenantId = @0 AND PushToken IS NOT NULL",
            tenantId);

        await FanOutAsync(db, tenantId, tokens, title, body, deepLinkPath: null, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> SendNotificationToUserAsync(
        string userId,
        string tenantId,
        string title,
        string body,
        string? deepLinkPath = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(deepLinkPath) && !PrismDeepLinkPath.IsSitePath(deepLinkPath))
        {
            _logger.LogWarning(
                "Deep link {DeepLinkPath} is not a path on this site, so it was dropped; the notification is sent without it (tenant: {TenantId}, title: {Title}).",
                LogScrub.Line(deepLinkPath), tenantId, LogScrub.Line(title));
            deepLinkPath = null;
        }

        using var db = _databaseFactory.CreateDatabase();

        var tokens = GetPushTokensForUsers(db, tenantId, [userId]);

        var sentCount = await FanOutAsync(db, tenantId, tokens, title, body, deepLinkPath, ct);
        return sentCount > 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string> GetPushTokensForUsers(
        Umbraco.Cms.Infrastructure.Persistence.IUmbracoDatabase db,
        string tenantId,
        IEnumerable<string> userIds)
    {
        var tokens = new List<string>();
        foreach (var userId in userIds)
        {
            var userTokens = db.Fetch<string>(
                "SELECT PushToken FROM prismDeviceCredentials WHERE TenantId = @0 AND UserId = @1 AND PushToken IS NOT NULL",
                tenantId, userId);
            tokens.AddRange(userTokens);
        }
        return tokens;
    }

    /// <returns>The total number of devices actually sent to, across every batch — 0 for every "nothing to do" or failure case (no registered device, Firebase not configured, every send rejected), so a caller can tell a real send from a silent no-op instead of assuming success just because nothing threw.</returns>
    private async Task<int> FanOutAsync(
        Umbraco.Cms.Infrastructure.Persistence.IUmbracoDatabase db,
        string tenantId,
        IReadOnlyList<string> tokens,
        string title,
        string body,
        string? deepLinkPath,
        CancellationToken ct)
    {
        if (tokens.Count == 0)
        {
            _logger.LogInformation(
                "No registered device for this notification (tenant: {TenantId}, title: {Title}).",
                tenantId, LogScrub.Line(title));
            return 0;
        }

        if (_messaging == null)
        {
            _logger.LogWarning(
                "FCM is not initialised (Prism:Firebase:CredentialJson not configured). " +
                "Notification not sent (title: {Title}).", LogScrub.Line(title));
            return 0;
        }

        var staleTokens = new List<string>();
        var totalSent = 0;

        for (var offset = 0; offset < tokens.Count; offset += FcmBatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = tokens.Skip(offset).Take(FcmBatchSize).ToList();
            var message = new MulticastMessage
            {
                Tokens = batch,
                Notification = new Notification { Title = title, Body = body },
                Data = string.IsNullOrWhiteSpace(deepLinkPath)
                    ? null
                    : new Dictionary<string, string> { ["url"] = deepLinkPath },
            };

            try
            {
                var response = await _messaging.SendEachForMulticastAsync(message, ct);

                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var r = response.Responses[i];
                    if (!r.IsSuccess &&
                        r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    {
                        staleTokens.Add(batch[i]);
                    }
                }

                _logger.LogInformation(
                    "FCM multicast: sent={Sent} failed={Failed} (title: {Title})",
                    response.SuccessCount, response.FailureCount, LogScrub.Line(title));
                totalSent += response.SuccessCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM multicast batch failed (title: {Title}, batchOffset: {Offset})", LogScrub.Line(title), offset);
            }
        }

        // Nullify stale tokens so they don't accumulate (scoped to current tenant)
        foreach (var stale in staleTokens)
        {
            try
            {
                db.Execute(
                    "UPDATE prismDeviceCredentials SET PushToken = NULL WHERE PushToken = @0 AND TenantId = @1",
                    stale, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to nullify stale push token.");
            }
        }

        return totalSent;
    }
}
