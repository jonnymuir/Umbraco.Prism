using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates the prismNotificationSubscriptions table for per-user,
/// per-tenant genre subscriptions used by the push notification fan-out.
/// </summary>
public class CreatePrismNotificationSubscriptionsTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismNotificationSubscriptions"))
        {
            Create.Table<PrismNotificationSubscriptionSchema>().Do();

            // Unique: one subscription row per (user, tenant, genre)
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismNotificationSubscriptions_User_Tenant_Genre
                ON prismNotificationSubscriptions (UserId, TenantId, Genre);");

            // Fan-out lookup: all subscribers for a genre within a tenant
            Database.Execute(@"
                CREATE INDEX IX_prismNotificationSubscriptions_TenantId_Genre
                ON prismNotificationSubscriptions (TenantId, Genre);");
        }

        return Task.CompletedTask;
    }
}
