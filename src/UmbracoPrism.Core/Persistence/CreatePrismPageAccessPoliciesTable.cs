using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates the prismPageAccessPolicies table.
/// </summary>
public class CreatePrismPageAccessPoliciesTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists("prismPageAccessPolicies"))
        {
            Create.Table<PrismPageAccessPolicySchema>().Do();
        }

        return Task.CompletedTask;
    }
}
