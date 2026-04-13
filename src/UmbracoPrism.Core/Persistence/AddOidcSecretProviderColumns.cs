using Umbraco.Cms.Infrastructure.Migrations;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Adds provider/reference columns for generic OIDC client secret resolution and backfills legacy inline secrets.
/// </summary>
public class AddOidcSecretProviderColumns(IMigrationContext context) : AsyncMigrationBase(context)
{
    /// <inheritdoc />
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("PrismTenants", "OidcClientSecretProvider"))
        {
            Create.Column("OidcClientSecretProvider").OnTable("PrismTenants").AsString(64).Nullable().Do();
        }

        if (!ColumnExists("PrismTenants", "OidcClientSecretReference"))
        {
            Create.Column("OidcClientSecretReference").OnTable("PrismTenants").AsString(500).Nullable().Do();
        }

        if (ColumnExists("PrismTenants", "OidcClientSecret"))
        {
            Database.Execute(
                $"""
                UPDATE prismTenants
                SET OidcClientSecretProvider = '{PrismSecretProviderNames.Inline}',
                    OidcClientSecretReference = OidcClientSecret
                WHERE OidcClientSecret IS NOT NULL
                  AND TRIM(OidcClientSecret) <> ''
                  AND (OidcClientSecretProvider IS NULL OR TRIM(OidcClientSecretProvider) = '')
                  AND (OidcClientSecretReference IS NULL OR TRIM(OidcClientSecretReference) = '');
                """);
        }

        return Task.CompletedTask;
    }
}
