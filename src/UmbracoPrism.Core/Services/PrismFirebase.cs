using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// The site's Firebase Cloud Messaging client, shared by push notifications and journey statuses.
/// Credentials come from <c>Prism:Firebase:CredentialJson</c>: a JSON string (starting with
/// <c>{</c>) or the path to a service-account JSON file. <see cref="Messaging"/> is null when they
/// are missing or invalid, and the reason is logged once, at start-up.
/// </summary>
public sealed class PrismFirebase(IConfiguration configuration, ILogger<PrismFirebase> logger)
{
    private const string CredentialConfigKey = "Prism:Firebase:CredentialJson";

    public FirebaseMessaging? Messaging { get; } = TryInit(configuration, logger);

    private static FirebaseMessaging? TryInit(IConfiguration configuration, ILogger logger)
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

            // Defensive, not expected: FirebaseMessaging.GetMessaging(FirebaseApp) is documented
            // to always return a real instance for a non-null app (or throw ArgumentNullException,
            // already handled by the catch below) — but trusting a third-party contract forever is
            // exactly the kind of assumption that reintroduces a silent null with no diagnostic
            // trail if a future SDK version ever changes that. Cheap to check, and if it ever does
            // fire, it's immediately actionable instead of another round of "why is this null".
            if (messaging is null)
            {
                logger.LogError(
                    "FirebaseMessaging.GetMessaging returned null despite a successfully created " +
                    "FirebaseApp — contradicts its documented contract; treating as uninitialised.");
                return null;
            }

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
