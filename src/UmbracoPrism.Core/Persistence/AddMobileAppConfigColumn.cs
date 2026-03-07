using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

public class AddMobileAppConfigColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismTenants", "MobileAppConfig"))
        {
            Create.Column("MobileAppConfig")
                .OnTable("prismTenants")
                .AsString(int.MaxValue)
                .Nullable()
                .Do();
        }

        return Task.CompletedTask;
    }
}
