using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Implements <see cref="IPrismNotificationService"/> using Firebase Cloud Messaging (FCM).
/// FCM credentials are loaded from <c>Prism:Firebase:CredentialJson</c> — a JSON string
/// (value starts with <c>{</c>) or a file path to a service-account JSON file.
/// </summary>
public class PrismNotificationService : IPrismNotificationService
{
    private const string CredentialConfigKey = "Prism:Firebase:CredentialJson";
    private const int FcmBatchSize = 500;

    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly ILogger<PrismNotificationService> _logger;
    private readonly FirebaseMessaging? _messaging;

    public PrismNotificationService(
        IUmbracoDatabaseFactory databaseFactory,
        IConfiguration configuration,
        ILogger<PrismNotificationService> logger)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _messaging = TryInitFirebase(configuration, logger);
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
        db.Execute(
            "UPDATE prismDeviceCredentials SET PushToken = NULL WHERE UserId = @0 AND TenantId = @1",
            userId, tenantId);
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

        await FanOutAsync(db, tenantId, tokens, title, body, ct);
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

        await FanOutAsync(db, tenantId, tokens, title, body, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> SendNotificationToUserAsync(
        string userId,
        string tenantId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        using var db = _databaseFactory.CreateDatabase();

        var tokens = GetPushTokensForUsers(db, tenantId, [userId]);

        var sentCount = await FanOutAsync(db, tenantId, tokens, title, body, ct);
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
                Notification = new Notification { Title = title, Body = body }
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

    private static FirebaseMessaging? TryInitFirebase(IConfiguration configuration, ILogger logger)
    {
        var credentialValue = configuration[CredentialConfigKey];

        if (string.IsNullOrWhiteSpace(credentialValue))
        {
            logger.LogInformation(
                "Prism:Firebase:CredentialJson is not configured — push notifications disabled.");
            return null;
        }

        // Diagnostic only — structure/shape, never the credential's own values. Found live: a
        // generic "not configured" message downstream (FanOutAsync, hardcoded regardless of
        // which branch here actually failed) made every real failure indistinguishable from a
        // genuinely-missing credential, with the one specific log line that would say why (the
        // exception this method's own catch block deliberately swallowed) never surfacing at all.
        var isJson = credentialValue.TrimStart().StartsWith('{');
        logger.LogInformation(
            "Firebase credential resolved: length={Length}, mode={Mode}, hasType={HasType}, " +
            "hasProjectId={HasProjectId}, hasPrivateKey={HasPrivateKey}, hasClientEmail={HasClientEmail}.",
            credentialValue.Length,
            isJson ? "json" : "file-path",
            isJson && credentialValue.Contains("\"type\""),
            isJson && credentialValue.Contains("\"project_id\""),
            isJson && credentialValue.Contains("\"private_key\""),
            isJson && credentialValue.Contains("\"client_email\""));

        try
        {
            // Guard: only initialise once across the app lifetime
            var appName = "prism-notifications";
            FirebaseApp? app;

            try
            {
                app = FirebaseApp.GetInstance(appName);
                logger.LogInformation(
                    "Reusing an already-created FirebaseApp instance named '{AppName}' — its " +
                    "credential was NOT re-read from current config.", appName);
            }
            catch (Exception)
            {
                app = null;
            }

            if (app == null)
            {
                GoogleCredential credential;

                if (isJson)
                {
                    // JSON string (from Key Vault or appsettings dev override)
                    credential = GoogleCredential.FromJson(credentialValue);
                }
                else
                {
                    // File path (legacy local dev scenario)
                    credential = GoogleCredential.FromFile(credentialValue);
                }

                app = FirebaseApp.Create(
                    new AppOptions { Credential = credential },
                    appName);
            }

            var messaging = FirebaseMessaging.GetMessaging(app);
            logger.LogInformation("Firebase initialised successfully.");
            return messaging;
        }
        catch (Exception ex)
        {
            // The exception type/message describe a structural problem (e.g. "missing
            // 'private_key' field", "unrecognized credential type") — they don't echo back the
            // credential's own field values, so logging them isn't a leak, unlike the raw JSON
            // itself would be. This is the one piece of information that would have actually
            // answered "why is _messaging null" instead of everyone downstream guessing.
            logger.LogError(ex,
                "Failed to initialise Firebase — push notifications disabled. Failure: {ExceptionType}: {ExceptionMessage}",
                ex.GetType().Name, ex.Message);
            return null;
        }
    }
}
