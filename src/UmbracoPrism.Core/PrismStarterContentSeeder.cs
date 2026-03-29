using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core;

/// <summary>
/// Opt-in seeder for Prism starter content (Home page + Member Dashboard).
/// Only runs if:
///  1. PrismConfiguration.SeedStarterContent is true, AND
///  2. The content tree is empty (no root content exists).
/// </summary>
public class PrismStarterContentSeeder(
    IOptions<PrismConfiguration> prismConfig,
    IContentService contentService,
    IContentTypeService contentTypeService,
    IRuntimeState runtimeState)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        // Only run if Umbraco is fully running
        if (runtimeState.Level < RuntimeLevel.Run) return;

        // Only seed if explicitly opted in
        if (!prismConfig.Value.SeedStarterContent) return;

        await Task.Run(() =>
        {
            // Check if content tree is empty
            var rootContent = contentService.GetRootContent();
            if (rootContent.Any()) return; // Content already exists, do nothing

            // Seed home page
            var homePageType = contentTypeService.Get("homePage");
            if (homePageType == null) return; // Type not created yet

            var homePage = contentService.Create("Home", Constants.System.Root, homePageType.Alias);
            var saveResult = contentService.Save(homePage);
            if (saveResult.Success)
            {
                var publishResult = contentService.Publish(homePage, new[] { "*" });
                
                if (publishResult.Success)
                {
                    // Seed member dashboard as child of home
                    var dashboardType = contentTypeService.Get("memberDashboard");
                    if (dashboardType != null)
                    {
                        var dashboardPage = contentService.Create("Dashboard", homePage.Id, dashboardType.Alias);
                        var saveDashResult = contentService.Save(dashboardPage);
                        if (saveDashResult.Success)
                        {
                            contentService.Publish(dashboardPage, new[] { "*" });
                        }
                    }
                }
            }
        }, cancellationToken);
    }
}
