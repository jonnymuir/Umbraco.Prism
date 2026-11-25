using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration to add generic OIDC authority and client columns to the PrismTenants table.
/// Enables support for non-Entra OIDC providers (e.g., Keycloak, Okta) per tenant.
/// </summary>
/// <param name="context"></param>
public class AddOidcAuthorityColumns(IMigrationContext context) : AsyncMigrationBase(context)
{
    /// <summary>
    /// Executes the migration to add OIDC authority and client columns.
    /// </summary>
    /// <returns></returns>
    protected override Task MigrateAsync()
    {
        // OidcAuthority: Full OIDC authority URL (e.g., http://localhost:8080/realms/prism-dev)
        // When set, overrides Entra-specific authority construction
        if (!ColumnExists("PrismTenants", "OidcAuthority"))
            Create.Column("OidcAuthority").OnTable("PrismTenants").AsString(500).Nullable().Do();

        // OidcClientId: OIDC client ID for non-Entra providers
        // Kept separate from EntraClientId for clarity and to avoid confusion
        if (!ColumnExists("PrismTenants", "OidcClientId"))
            Create.Column("OidcClientId").OnTable("PrismTenants").AsString(255).Nullable().Do();

        // OidcClientSecret is created here and moved to the provider and reference columns by the next migration.
        if (!ColumnExists("PrismTenants", "OidcClientSecret"))
            Create.Column("OidcClientSecret").OnTable("PrismTenants").AsString(500).Nullable().Do();

        return Task.CompletedTask;
    }
}
