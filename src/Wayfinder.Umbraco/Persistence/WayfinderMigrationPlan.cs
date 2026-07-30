using Umbraco.Cms.Core.Packaging;

namespace Wayfinder.Umbraco.Persistence;

/// <summary>
/// Migration plan for the Wayfinder.Umbraco package's own database tables — independent of
/// any host's own migration plan (e.g. Prism's), since this package is a standalone Umbraco
/// dependency, not something only Prism can install.
/// </summary>
public class WayfinderMigrationPlan : PackageMigrationPlan
{
    public WayfinderMigrationPlan() : base("Wayfinder.Umbraco")
    {
    }

    protected override void DefinePlan()
    {
        To<CreatePrismCmsServiceBlueprintTable>("initial-state")
            .To<CreatePrismCmsServiceRequestTable>("add-cms-service-requests");
    }
}
