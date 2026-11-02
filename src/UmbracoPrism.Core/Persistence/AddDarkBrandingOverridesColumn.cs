using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

public class AddDarkBrandingOverridesColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismTenants", "DarkBrandingOverrides"))
        {
            Create.Column("DarkBrandingOverrides")
            .OnTable("prismTenants")
                .AsString(int.MaxValue)
                .Nullable()
                .Do();
        }

        return Task.CompletedTask;
    }
}
