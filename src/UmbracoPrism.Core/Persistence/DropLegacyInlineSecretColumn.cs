using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>Drops the plaintext OidcClientSecret column. Its values were moved to the provider and reference columns earlier in the plan.</summary>
public class DropLegacyInlineSecretColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    /// <inheritdoc />
    protected override Task MigrateAsync()
    {
        if (ColumnExists("prismTenants", "OidcClientSecret"))
        {
            Delete.Column("OidcClientSecret").FromTable("prismTenants").Do();
        }

        return Task.CompletedTask;
    }
}
