using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates the prismCmsWorkflowInstance table backing the durable,
/// session-scoped <c>IWorkflowInstanceStore</c> implementation for CMS Workflow.
/// </summary>
public class CreatePrismCmsWorkflowInstanceTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismCmsWorkflowInstance"))
        {
            Create.Table<PrismCmsWorkflowInstanceSchema>().Do();

            // Sweep query: find every row past its expiry.
            Database.Execute(@"
                CREATE INDEX IX_prismCmsWorkflowInstance_ExpiresUtc
                ON prismCmsWorkflowInstance (ExpiresUtc);");

            // FindLatestInstance-style lookup: most recent instance for (tenant, user, workflow).
            Database.Execute(@"
                CREATE INDEX IX_prismCmsWorkflowInstance_Tenant_User_Workflow
                ON prismCmsWorkflowInstance (TenantId, UserId, WorkflowKey);");
        }

        return Task.CompletedTask;
    }
}
