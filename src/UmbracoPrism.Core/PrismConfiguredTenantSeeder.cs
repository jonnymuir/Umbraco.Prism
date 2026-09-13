using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core;

/// <summary>
/// Opt-in seeder for a single tenant defined entirely in configuration (<c>Prism:SeedTenant</c>)
/// — no backoffice interaction, no uSync file needed. Reconciles the tenant back to the
/// configured values on every boot (same idempotent pattern <c>DemoTenantSeeder</c> already uses
/// for the local Keycloak dev tenant), so config changes (a rotated Entra app registration, a
/// changed secret name) take effect on the next deploy/restart with no manual step.
///
/// Found necessary live: a uSync-file-based approach (a committed <c>PrismTenant</c> config,
/// <see cref="UmbracoPrism.uSync.SyncHandlers.PrismTenantHandler"/>) was tried first for
/// deploying the production reference app's own tenant, but never actually created the row —
/// this codebase's own established pattern for "content that needs to exist on a real deploy" is
/// a code-based reconciler on <see cref="UmbracoApplicationStartedNotification"/> (see also
/// <see cref="PrismStarterContentSeeder"/>), not uSync. This does that, generically, for one
/// configured tenant, rather than hardcoding another one-off seeder per environment.
/// </summary>
public class PrismConfiguredTenantSeeder(
    IOptions<PrismConfiguration> prismConfig,
    IUmbracoDatabaseFactory databaseFactory,
    IRuntimeState runtimeState,
    ILogger<PrismConfiguredTenantSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;

        var options = prismConfig.Value.SeedTenant;
        if (string.IsNullOrWhiteSpace(options.Hostname)) return Task.CompletedTask;

        return Task.Run(() => ReconcileTenant(options), cancellationToken);
    }

    private void ReconcileTenant(PrismSeedTenantOptions options)
    {
        var hostname = options.Hostname!.Trim().ToLowerInvariant();
        var name = string.IsNullOrWhiteSpace(options.Name) ? hostname : options.Name!.Trim();

        using var db = databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismTenantSchema>(
            "SELECT * FROM prismTenants WHERE Hostname = @0",
            [hostname]);

        if (existing == null)
        {
            var schema = new PrismTenantSchema { Hostname = hostname };
            ApplySeedValues(schema, name, hostname, options);
            db.Insert(schema);

            logger.LogInformation(
                "PRISM ConfiguredTenantSeeder: Created tenant '{Name}' for host '{Hostname}'.",
                name, hostname);
            return;
        }

        if (!ApplySeedValues(existing, name, hostname, options))
        {
            logger.LogDebug(
                "PRISM ConfiguredTenantSeeder: Tenant '{Name}' (id={Id}) already matches seeded config.",
                name, existing.Id);
            return;
        }

        db.Update(existing);
        logger.LogInformation(
            "PRISM ConfiguredTenantSeeder: Reconciled tenant '{Name}' (id={Id}) to seeded config.",
            name, existing.Id);
    }

    private static bool ApplySeedValues(PrismTenantSchema tenant, string name, string hostname, PrismSeedTenantOptions options)
    {
        var changed = false;

        changed |= SetRequiredString(tenant.Name, name, value => tenant.Name = value);
        changed |= SetRequiredString(tenant.Hostname, hostname, value => tenant.Hostname = value);
        changed |= SetString(tenant.EntraTenantId, options.EntraTenantId, value => tenant.EntraTenantId = value);
        changed |= SetString(tenant.EntraClientId, options.EntraClientId, value => tenant.EntraClientId = value);
        changed |= SetString(tenant.SecretKeyName, options.SecretKeyName, value => tenant.SecretKeyName = value);
        changed |= SetString(tenant.OidcAuthority, options.OidcAuthority, value => tenant.OidcAuthority = value);
        changed |= SetString(tenant.OidcClientId, options.OidcClientId, value => tenant.OidcClientId = value);
        changed |= SetString(tenant.OidcClientSecretProvider, options.OidcClientSecretProvider, value => tenant.OidcClientSecretProvider = value);
        changed |= SetString(tenant.OidcClientSecretReference, options.OidcClientSecretReference, value => tenant.OidcClientSecretReference = value);

        if (tenant.AllowBiometricLogin != options.AllowBiometricLogin)
        {
            tenant.AllowBiometricLogin = options.AllowBiometricLogin;
            changed = true;
        }

        return changed;
    }

    private static bool SetString(string? currentValue, string? expectedValue, Action<string?> assign)
    {
        if (string.Equals(currentValue, expectedValue, StringComparison.Ordinal)) return false;
        assign(expectedValue);
        return true;
    }

    private static bool SetRequiredString(string currentValue, string expectedValue, Action<string> assign)
    {
        if (string.Equals(currentValue, expectedValue, StringComparison.Ordinal)) return false;
        assign(expectedValue);
        return true;
    }
}
