using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that removes the legacy prismFieldGroupDefinitions table and renames
/// prismFieldGroupSubmissions to prismWorkflowFieldValues.
/// </summary>
public class RemoveLegacyFieldGroupDefinitions(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        // Drop the legacy field group definitions table if it exists
        if (TableExists("prismFieldGroupDefinitions"))
        {
            Delete.Table("prismFieldGroupDefinitions").Do();
        }

        // Rename prismFieldGroupSubmissions to prismWorkflowFieldValues if needed
        if (TableExists("prismFieldGroupSubmissions") && !TableExists("prismWorkflowFieldValues"))
        {
            Database.Execute("EXEC sp_rename 'prismFieldGroupSubmissions', 'prismWorkflowFieldValues';");
            
            // Rename the indexes
            Database.Execute("EXEC sp_rename 'IX_prismFieldGroupSubmissions_InstanceId', 'IX_prismWorkflowFieldValues_InstanceId', 'INDEX';");
            Database.Execute("EXEC sp_rename 'IX_prismFieldGroupSubmissions_InstanceId_FieldGroupKey_IsCurrent', 'IX_prismWorkflowFieldValues_InstanceId_FieldGroupKey_IsCurrent', 'INDEX';");
            Database.Execute("EXEC sp_rename 'IX_prismFieldGroupSubmissions_TenantId', 'IX_prismWorkflowFieldValues_TenantId', 'INDEX';");
        }

        return Task.CompletedTask;
    }
}
