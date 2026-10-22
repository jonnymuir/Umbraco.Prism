using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Adds the journey status columns to prismDeviceCredentials: which app platform shows statuses,
/// and the iOS token that starts a Live Activity.
/// </summary>
public class AddJourneyStatusColumns(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!ColumnExists("prismDeviceCredentials", "JourneyStatusPlatform"))
        {
            Create.Column("JourneyStatusPlatform").OnTable("prismDeviceCredentials").AsString(16).Nullable().Do();
        }

        if (!ColumnExists("prismDeviceCredentials", "LiveActivityStartToken"))
        {
            Create.Column("LiveActivityStartToken").OnTable("prismDeviceCredentials").AsString(500).Nullable().Do();
        }

        return Task.CompletedTask;
    }
}
