using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates the prismCmsWorkflowDefinition table backing the backoffice-hosted
/// CMS Workflow editor's definition store.
/// </summary>
public class CreatePrismCmsWorkflowDefinitionTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismCmsWorkflowDefinition"))
        {
            Create.Table<PrismCmsWorkflowDefinitionSchema>().Do();
        }

        return Task.CompletedTask;
    }
}
