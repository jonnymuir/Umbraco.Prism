using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates all workflow engine tables.
/// </summary>
public class CreatePrismWorkflowTables(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        // Create workflow definitions table
        if (!TableExists("prismWorkflowDefinitions"))
        {
            Create.Table<PrismWorkflowDefinitionSchema>().Do();

            // Unique index: one definition per (TenantId, WorkflowKey, Version)
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismWorkflowDefinitions_TenantId_WorkflowKey_Version
                ON prismWorkflowDefinitions (TenantId, WorkflowKey, Version);");

            // Published workflow lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowDefinitions_TenantId_Status
                ON prismWorkflowDefinitions (TenantId, Status);");
        }

        // Create field group definitions table
        if (!TableExists("prismFieldGroupDefinitions"))
        {
            Create.Table<PrismFieldGroupDefinitionSchema>().Do();

            // Unique index: one definition per (TenantId, FieldGroupKey, Version)
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismFieldGroupDefinitions_TenantId_FieldGroupKey_Version
                ON prismFieldGroupDefinitions (TenantId, FieldGroupKey, Version);");

            // Published field group lookup
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupDefinitions_TenantId_Status
                ON prismFieldGroupDefinitions (TenantId, Status);");
        }

        // Create workflow instances table
        if (!TableExists("prismWorkflowInstances"))
        {
            Create.Table<PrismWorkflowInstanceSchema>().Do();

            // Tenant isolation and user instances lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowInstances_TenantId_UserId
                ON prismWorkflowInstances (TenantId, UserId);");

            // Active instances lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowInstances_TenantId_Status
                ON prismWorkflowInstances (TenantId, Status);");

            // State version concurrency control
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowInstances_InstanceId_StateVersion
                ON prismWorkflowInstances (InstanceId, StateVersion);");
        }

        // Create workflow tasks table
        if (!TableExists("prismWorkflowTasks"))
        {
            Create.Table<PrismWorkflowTaskSchema>().Do();

            // Instance tasks lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowTasks_InstanceId
                ON prismWorkflowTasks (InstanceId);");

            // Role/user assignment queue lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowTasks_TenantId_AssignedTo_Status
                ON prismWorkflowTasks (TenantId, AssignedTo, Status);");

            // Due date sorting for task queues
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowTasks_DueAt
                ON prismWorkflowTasks (DueAt);");
        }

        // Create workflow events table
        if (!TableExists("prismWorkflowEvents"))
        {
            Create.Table<PrismWorkflowEventSchema>().Do();

            // Instance timeline lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowEvents_InstanceId_TimestampUtc
                ON prismWorkflowEvents (InstanceId, TimestampUtc);");

            // Correlation ID tracing
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowEvents_CorrelationId
                ON prismWorkflowEvents (CorrelationId);");

            // Tenant event audit
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowEvents_TenantId_EventType
                ON prismWorkflowEvents (TenantId, EventType);");
        }

        // Create field group submissions table
        if (!TableExists("prismFieldGroupSubmissions"))
        {
            Create.Table<PrismWorkflowFieldGroupSubmissionSchema>().Do();

            // Instance submissions lookup
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupSubmissions_InstanceId
                ON prismFieldGroupSubmissions (InstanceId);");

            // Current submission by field group
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupSubmissions_InstanceId_FieldGroupKey_IsCurrent
                ON prismFieldGroupSubmissions (InstanceId, FieldGroupKey, IsCurrent);");

            // Tenant data isolation
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupSubmissions_TenantId
                ON prismFieldGroupSubmissions (TenantId);");
        }

        return Task.CompletedTask;
    }
}
