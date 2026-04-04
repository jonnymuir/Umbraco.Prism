using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

public class DropThemeColorColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (ColumnExists("prismTenants", "themeColor"))
        {
            Delete.Column("themeColor").FromTable("prismTenants").Do();
        }

        return Task.CompletedTask;
    }
}
