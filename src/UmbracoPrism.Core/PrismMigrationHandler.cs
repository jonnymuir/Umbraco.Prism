using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core;

/// <summary>
/// Migration handler for creating custom database tables for Prism.
/// </summary>
public class PrismMigrationHandler(
        IMigrationPlanExecutor migrationPlanExecutor,
        ICoreScopeProvider coreScopeProvider,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    /// <summary>
    /// Handles the application starting notification to set up database migrations.
    /// </summary>
    /// <param name="notification"></param>
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        // Only run migrations if Umbraco is fully installed and ready
        if (runtimeState.Level < Umbraco.Cms.Core.RuntimeLevel.Run) return;

        var upgrader = new Upgrader(new PrismMigrationPlan());
        await upgrader.ExecuteAsync(migrationPlanExecutor, coreScopeProvider, keyValueService);
    }
}