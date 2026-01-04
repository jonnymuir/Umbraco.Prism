using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoPrism.Core;

/// <summary>
/// Migration handler for creating custom database tables for Prism.
/// </summary>
public class PrismMigrationHandler : INotificationHandler<UmbracoApplicationStartingNotification>
{
    /// <summary>
    /// Handles the application starting notification to set up database migrations.
    /// </summary>
    /// <param name="notification"></param>
    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        // We will fill this in shortly to create your SQLite tables
    }
}