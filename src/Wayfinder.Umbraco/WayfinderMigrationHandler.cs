using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Wayfinder.Umbraco.Persistence;

namespace Wayfinder.Umbraco;

/// <summary>
/// Runs <see cref="WayfinderMigrationPlan"/> at startup — creates Wayfinder.Umbraco's own
/// database tables, independent of any host's own migration plan.
/// </summary>
public class WayfinderMigrationHandler(
        IMigrationPlanExecutor migrationPlanExecutor,
        ICoreScopeProvider coreScopeProvider,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;

        var upgrader = new Upgrader(new WayfinderMigrationPlan());
        await upgrader.ExecuteAsync(migrationPlanExecutor, coreScopeProvider, keyValueService);
    }
}
