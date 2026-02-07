using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

public class AddBrandingOverridesColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismTenants", "BrandingOverrides"))
        {
            Create.Column("BrandingOverrides")
            .OnTable("prismTenants")
                .AsString(int.MaxValue)
                .Nullable()
                .Do();
        }

        return Task.CompletedTask;
    }
}
