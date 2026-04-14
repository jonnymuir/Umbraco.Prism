using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Dev-only seeder that ensures a localhost tenant pointing at the Aspire Keycloak
/// instance exists on startup. Only runs in Development and reconciles the seeded
/// localhost row back to the expected auth shape on every start so isolated test
/// databases can be recreated or restarted without configuration drift.
/// </summary>
public class DemoTenantSeeder(
    IUmbracoDatabaseFactory databaseFactory,
    IHostEnvironment hostEnvironment,
    IRuntimeState runtimeState,
    ILogger<DemoTenantSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // Keycloak values from keycloak/realm-export.json (Aspire dev environment).
    private const string LocalhostHostname = "localhost";
    private const string TenantName        = "Local Dev (Keycloak)";
    private const string DefaultKeycloakBaseUrl = "https://localhost:8443";
    private const string OidcClientId      = "prism-client";
    private const string OidcClientSecret  = "prism-dev-secret";

    public Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;
        if (!hostEnvironment.IsDevelopment())       return Task.CompletedTask;

        return Task.Run(() => ReconcileLocalhostTenant(), cancellationToken);
    }

    private void ReconcileLocalhostTenant()
    {
        var keycloakBaseUrl = Environment.GetEnvironmentVariable("KEYCLOAK_URL");
        if (string.IsNullOrWhiteSpace(keycloakBaseUrl))
        {
            keycloakBaseUrl = DefaultKeycloakBaseUrl;
        }

        var oidcAuthority = $"{keycloakBaseUrl.TrimEnd('/')}/realms/prism-dev";

        using var db = databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismTenantSchema>(
            "SELECT * FROM prismTenants WHERE Hostname = @0",
            [LocalhostHostname]);

        if (existing == null)
        {
            var schema = CreateSeedTenant(oidcAuthority);
            db.Insert(schema);

            logger.LogInformation(
                "DEMO SEEDER: Created localhost tenant '{Name}' pointing at Keycloak ({Authority}).",
                TenantName,
                oidcAuthority);
            return;
        }

        if (!ApplySeedValues(existing, oidcAuthority))
        {
            logger.LogDebug(
                "DEMO SEEDER: localhost tenant already matches seeded config (id={Id}).",
                existing.Id);
            return;
        }

        db.Update(existing);
        logger.LogInformation(
            "DEMO SEEDER: Reconciled localhost tenant '{Name}' (id={Id}) to seeded config ({Authority}).",
            TenantName,
            existing.Id,
            oidcAuthority);
    }

    private static PrismTenantSchema CreateSeedTenant(string oidcAuthority)
    {
        var schema = new PrismTenantSchema
        {
            Hostname = LocalhostHostname
        };

        ApplySeedValues(schema, oidcAuthority);
        return schema;
    }

    private static bool ApplySeedValues(PrismTenantSchema tenant, string oidcAuthority)
    {
        var changed = false;

        changed |= SetRequiredString(tenant.Name, TenantName, value => tenant.Name = value);
        changed |= SetRequiredString(tenant.Hostname, LocalhostHostname, value => tenant.Hostname = value);
        changed |= SetString(tenant.OidcAuthority, oidcAuthority, value => tenant.OidcAuthority = value);
        changed |= SetString(tenant.OidcClientId, OidcClientId, value => tenant.OidcClientId = value);
        changed |= SetString(tenant.OidcClientSecretProvider, PrismSecretProviderNames.Inline, value => tenant.OidcClientSecretProvider = value);
        changed |= SetString(tenant.OidcClientSecretReference, OidcClientSecret, value => tenant.OidcClientSecretReference = value);
        changed |= SetString(tenant.OidcClientSecret, null, value => tenant.OidcClientSecret = value);
        changed |= SetString(tenant.EntraTenantId, null, value => tenant.EntraTenantId = value);
        changed |= SetString(tenant.EntraClientId, null, value => tenant.EntraClientId = value);
        changed |= SetString(tenant.SecretKeyName, null, value => tenant.SecretKeyName = value);

        if (!tenant.AllowBiometricLogin)
        {
            tenant.AllowBiometricLogin = true;
            changed = true;
        }

        return changed;
    }

    private static bool SetString(string? currentValue, string? expectedValue, Action<string?> assign)
    {
        if (string.Equals(currentValue, expectedValue, StringComparison.Ordinal))
        {
            return false;
        }

        assign(expectedValue);
        return true;
    }

    private static bool SetRequiredString(string currentValue, string expectedValue, Action<string> assign)
    {
        if (string.Equals(currentValue, expectedValue, StringComparison.Ordinal))
        {
            return false;
        }

        assign(expectedValue);
        return true;
    }
}
