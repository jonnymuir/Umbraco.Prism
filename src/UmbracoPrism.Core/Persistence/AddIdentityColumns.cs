using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration to add identity columns to the PrismTenants table.
/// </summary>
/// <param name="context"></param>
public class AddIdentityColumns(IMigrationContext context) : AsyncMigrationBase(context)
{
    /// <summary>
    /// Executes the migration to add identity columns.
    /// </summary>
    /// <returns></returns>
    protected override Task MigrateAsync()
    {
        // Safety check: only add if column doesn't exist
        if (!ColumnExists("PrismTenants", "EntraTenantId"))
            Create.Column("EntraTenantId").OnTable("PrismTenants").AsString(255).Nullable().Do();

        if (!ColumnExists("PrismTenants", "EntraClientId"))
            Create.Column("EntraClientId").OnTable("PrismTenants").AsString(255).Nullable().Do();

        if (!ColumnExists("PrismTenants", "SecretKeyName"))
            Create.Column("SecretKeyName").OnTable("PrismTenants").AsString(255).Nullable().Do();

        return Task.CompletedTask;
    }
}