using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;


public class CreatePrismTables(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        // Check if the table exists before creating
        if (!TableExists("prismTenants"))
        {
            Create.Table<PrismTenantSchema>().Do();
        }

        return Task.CompletedTask;
    }
}