using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
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

        await FanOutAsync(db, tokens, title, body, ct);
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

        await FanOutAsync(db, tokens, title, body, ct);
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

    private async Task FanOutAsync(
        Umbraco.Cms.Infrastructure.Persistence.IUmbracoDatabase db,
        IReadOnlyList<string> tokens,
        string title,
        string body,
        CancellationToken ct)
    {
        if (tokens.Count == 0)
            return;

        if (_messaging == null)
        {
            _logger.LogWarning(
                "FCM is not initialised (Prism:Firebase:CredentialJson not configured). " +
                "Notification not sent (title: {Title}).", title);
            return;
        }

        var staleTokens = new List<string>();

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
                    response.SuccessCount, response.FailureCount, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM multicast batch failed (title: {Title}, batchOffset: {Offset})", title, offset);
            }
        }

        // Nullify stale tokens so they don't accumulate
        foreach (var stale in staleTokens)
        {
            try
            {
                db.Execute(
                    "UPDATE prismDeviceCredentials SET PushToken = NULL WHERE PushToken = @0",
                    stale);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to nullify stale push token.");
            }
        }
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

        try
        {
            // Guard: only initialise once across the app lifetime
            var appName = "prism-notifications";
            FirebaseApp? app;

            try
            {
                app = FirebaseApp.GetInstance(appName);
            }
            catch (Exception)
            {
                app = null;
            }

            if (app == null)
            {
                GoogleCredential credential;

                if (credentialValue.TrimStart().StartsWith('{'))
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

            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialise Firebase — push notifications disabled.");
            return null;
        }
    }
}
