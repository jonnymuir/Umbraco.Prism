using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Wayfinder.Umbraco;

/// <summary>
/// Always-on composition for the Wayfinder.Umbraco package itself — just the migration that
/// creates its own tables. Everything else (the engine, stores, generic stage-rendering
/// infrastructure) is opt-in via <see cref="Extensions.WayfinderUmbracoServiceCollectionExtensions.AddWayfinderUmbraco"/>,
/// since a host that references this package for uSync/authoring alone shouldn't get a
/// background sweep service and DI registrations it never asked for.
/// </summary>
public class WayfinderUmbracoComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WayfinderMigrationHandler>();
    }
}
