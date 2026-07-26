using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates the prismCmsServiceBlueprint table backing the backoffice-hosted
/// CMS Service Blueprint editor's definition store.
/// </summary>
public class CreatePrismCmsServiceBlueprintTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismCmsServiceBlueprint"))
        {
            Create.Table<PrismCmsServiceBlueprintSchema>().Do();
        }

        return Task.CompletedTask;
    }
}
