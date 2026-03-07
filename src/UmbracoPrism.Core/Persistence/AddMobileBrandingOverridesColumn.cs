using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

public class AddMobileBrandingOverridesColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismTenants", "MobileBrandingOverrides"))
        {
            Create.Column("MobileBrandingOverrides")
            .OnTable("prismTenants")
                .AsString(int.MaxValue)
                .Nullable()
                .Do();
        }

        return Task.CompletedTask;
    }
}
