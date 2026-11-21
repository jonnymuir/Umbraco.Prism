using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>Creates prismSessions, the default home of member sign-in sessions.</summary>
public class CreatePrismSessionsTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismSessions"))
        {
            Create.Table<PrismSessionSchema>().Do();
            Database.Execute("CREATE INDEX IX_prismSessions_Subject ON prismSessions (Subject);");
            Database.Execute("CREATE INDEX IX_prismSessions_ExpiresAt ON prismSessions (ExpiresAt);");
        }

        return Task.CompletedTask;
    }
}
