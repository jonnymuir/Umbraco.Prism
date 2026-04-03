using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Validates the Settings content node exists on startup so other startup work can rely on it.
/// mobileNavLinks now uses a Block List backed by the MobileNavItem element type — editors
/// add navigation items via the backoffice rather than via a seeder.
///
/// Block List JSON format (for reference if manual seeding is ever needed):
/// <code>
/// {
///   "layout": { "Umbraco.BlockList": [ { "contentUdi": "umb://element/{guid}" } ] },
///   "contentData": [
///     {
///       "contentTypeKey": "{mobileNavItem-key}",
///       "udi": "umb://element/{guid}",
///       "navLabel": "Home",
///       "navUrl": "/",
///       "navIcon": null,
///       "openInNewTab": "0"
///     }
///   ],
///   "settingsData": []
/// }
/// </code>
/// </summary>
public class DemoMobileNavSeeder(
    IWebHostEnvironment env,
    IContentService contentService,
    IRuntimeState runtimeState,
    ILogger<DemoMobileNavSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        await Task.Run(CheckSettingsNode, cancellationToken);
    }

    private void CheckSettingsNode()
    {
        try
        {
            var settings = contentService
                .GetRootContent()
                .FirstOrDefault(c => c.ContentType.Alias == "settings");

            if (settings == null)
            {
                logger.LogDebug("DEMO SEEDER: Settings node not found — skipping.");
                return;
            }

            logger.LogInformation(
                "DEMO SEEDER: mobileNavLinks now uses Block List — add nav items via the backoffice Settings node.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DEMO SEEDER: Unexpected error checking Settings node — safe to ignore.");
        }
    }
}
