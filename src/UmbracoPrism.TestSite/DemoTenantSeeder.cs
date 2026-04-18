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

        ReconcileTenant(db, LocalhostHostname, TenantName, oidcAuthority);

        // In GitHub Codespaces the browser reaches the app via a forwarded hostname like
        // {name}-44345.app.github.dev. Seed a matching tenant so OIDC works over that URL.
        var codespaceHostname = BuildCodespaceTestSiteHostname();
        if (codespaceHostname is not null)
        {
            var codespaceName = Environment.GetEnvironmentVariable("CODESPACE_NAME")!;
            var domain = Environment.GetEnvironmentVariable("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN") ?? "app.github.dev";
            var codespaceKeycloakAuthority = $"https://{codespaceName}-8443.{domain}/realms/prism-dev";
            ReconcileTenant(db, codespaceHostname, $"{TenantName} (Codespaces)", codespaceKeycloakAuthority);
        }
    }

    private static string? BuildCodespaceTestSiteHostname()
    {
        var codespaceName = Environment.GetEnvironmentVariable("CODESPACE_NAME");
        if (string.IsNullOrWhiteSpace(codespaceName)) return null;
        var domain = Environment.GetEnvironmentVariable("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN") ?? "app.github.dev";
        return $"{codespaceName}-44345.{domain}";
    }

    private void ReconcileTenant(IUmbracoDatabase db, string hostname, string tenantName, string oidcAuthority)
    {
        var existing = db.FirstOrDefault<PrismTenantSchema>(
            "SELECT * FROM prismTenants WHERE Hostname = @0",
            [hostname]);

        if (existing == null)
        {
            var schema = CreateSeedTenant(hostname, tenantName, oidcAuthority);
            db.Insert(schema);

            logger.LogInformation(
                "DEMO SEEDER: Created tenant '{Name}' pointing at Keycloak ({Authority}).",
                tenantName,
                oidcAuthority);
            return;
        }

        if (!ApplySeedValues(existing, tenantName, hostname, oidcAuthority))
        {
            logger.LogDebug(
                "DEMO SEEDER: Tenant '{Name}' already matches seeded config (id={Id}).",
                tenantName,
                existing.Id);
            return;
        }

        db.Update(existing);
        logger.LogInformation(
            "DEMO SEEDER: Reconciled tenant '{Name}' (id={Id}) to seeded config ({Authority}).",
            tenantName,
            existing.Id,
            oidcAuthority);
    }

    private static PrismTenantSchema CreateSeedTenant(string hostname, string tenantName, string oidcAuthority)
    {
        var schema = new PrismTenantSchema
        {
            Hostname = hostname
        };

        ApplySeedValues(schema, tenantName, hostname, oidcAuthority);
        return schema;
    }

    private static bool ApplySeedValues(PrismTenantSchema tenant, string tenantName, string hostname, string oidcAuthority)
    {
        var changed = false;

        changed |= SetRequiredString(tenant.Name, tenantName, value => tenant.Name = value);
        changed |= SetRequiredString(tenant.Hostname, hostname, value => tenant.Hostname = value);
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
