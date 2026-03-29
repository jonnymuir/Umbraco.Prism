using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

public class AddAllowBiometricLoginColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismTenants", "AllowBiometricLogin"))
        {
            Create.Column("AllowBiometricLogin")
                .OnTable("prismTenants")
                .AsBoolean()
                .WithDefaultValue(true)
                .Do();
        }

        return Task.CompletedTask;
    }
}
