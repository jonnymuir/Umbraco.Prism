using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Dev-only seeder that ensures a localhost tenant pointing at the Aspire Keycloak
/// instance exists on startup. Only runs in Development and only creates the tenant
/// if no tenant with hostname "localhost" already exists — any other tenants are
/// left completely untouched.
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
    private const string OidcAuthority     = "http://localhost:8080/realms/prism-dev";
    private const string OidcClientId      = "prism-client";
    private const string OidcClientSecret  = "prism-dev-secret";

    public Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;
        if (!hostEnvironment.IsDevelopment())       return Task.CompletedTask;

        return Task.Run(() => EnsureLocalhostTenant(), cancellationToken);
    }

    private void EnsureLocalhostTenant()
    {
        using var db = databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismTenantSchema>(
            "SELECT * FROM prismTenants WHERE Hostname = @0",
            [LocalhostHostname]);

        if (existing != null)
        {
            logger.LogDebug(
                "DEMO SEEDER: localhost tenant already exists (id={Id}) — skipping.",
                existing.Id);
            return;
        }

        var schema = new PrismTenantSchema
        {
            Name              = TenantName,
            Hostname          = LocalhostHostname,
            OidcAuthority     = OidcAuthority,
            OidcClientId      = OidcClientId,
            OidcClientSecret  = OidcClientSecret,
            AllowBiometricLogin = true
        };

        db.Insert(schema);

        logger.LogInformation(
            "DEMO SEEDER: Created localhost tenant '{Name}' pointing at Keycloak ({Authority}).",
            TenantName, OidcAuthority);
    }
}
