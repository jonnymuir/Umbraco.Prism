using Umbraco.Cms.Infrastructure.Migrations;

namespace Wayfinder.Umbraco.Persistence;

/// <summary>
/// Migration that creates the prismCmsServiceRequest table backing the durable,
/// session-scoped <c>IServiceRequestStore</c> implementation for CMS Service Blueprints.
/// </summary>
public class CreatePrismCmsServiceRequestTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismCmsServiceRequest"))
        {
            Create.Table<PrismCmsServiceRequestSchema>().Do();

            // Sweep query: find every row past its expiry.
            Database.Execute(@"
                CREATE INDEX IX_prismCmsServiceRequest_ExpiresUtc
                ON prismCmsServiceRequest (ExpiresUtc);");

            // FindLatestRequest-style lookup: most recent request for (tenant, user, blueprint).
            Database.Execute(@"
                CREATE INDEX IX_prismCmsServiceRequest_Tenant_User_Blueprint
                ON prismCmsServiceRequest (TenantId, UserId, BlueprintKey);");
        }

        return Task.CompletedTask;
    }
}
