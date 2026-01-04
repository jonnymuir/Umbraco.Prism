using Umbraco.Cms.Core.Packaging;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration plan for the Umbraco Prism package.
/// </summary>
public class PrismMigrationPlan : PackageMigrationPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrismMigrationPlan"/> class.
    /// </summary>
    public PrismMigrationPlan() : base("UmbracoPrism")
    {
    }

    /// <summary>
    /// Defines the migration plan.
    /// </summary>
    protected override void DefinePlan()
    {
        // Define the initial state of your DB
        To<CreatePrismTables>("initial-state");
    }
}
