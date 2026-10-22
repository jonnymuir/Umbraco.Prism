using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>Creates prismJourneyStatuses, one row per member journey with a running Live Activity.</summary>
public class CreatePrismJourneyStatusesTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismJourneyStatuses"))
        {
            Create.Table<PrismJourneyStatusSchema>().Do();
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismJourneyStatuses_User_Tenant_Journey
                ON prismJourneyStatuses (UserId, TenantId, JourneyKey);");
        }

        return Task.CompletedTask;
    }
}
