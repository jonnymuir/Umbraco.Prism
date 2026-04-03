using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that adds the PushToken column to prismDeviceCredentials
/// for storing Firebase Cloud Messaging device tokens.
/// </summary>
public class AddPushTokenColumn(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismDeviceCredentials", "PushToken"))
        {
            Create.Column("PushToken")
                .OnTable("prismDeviceCredentials")
                .AsString(500)
                .Nullable()
                .Do();
        }

        return Task.CompletedTask;
    }
}
