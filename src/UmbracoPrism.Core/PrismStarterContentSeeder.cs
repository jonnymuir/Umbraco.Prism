using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    IRuntimeState runtimeState,
    ILogger<PrismStarterContentSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!prismConfig.Value.SeedStarterContent) return;

        await Task.Run(() =>
        {
            var rootContent = contentService.GetRootContent().ToList();

            // Only seed Home + Dashboard on a completely empty tree
            if (!rootContent.Any())
            {
                SeedHomeAndDashboard();
            }

            // Always ensure Settings node exists and has default nav links
            // (idempotent — only sets values if currently empty)
            EnsureSettingsDefaults();

        }, cancellationToken);
    }

    private void SeedHomeAndDashboard()
    {
        // Seed home page
        var homePageType = contentTypeService.Get("homePage");
        if (homePageType != null)
        {
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
        }
    }

    private void EnsureSettingsDefaults()
    {
        logger.LogInformation("PRISM StarterSeeder: EnsureSettingsDefaults starting");

        var settingsType = contentTypeService.Get("settings");
        if (settingsType == null) return;

        // Find or create the Settings node
        var settings = contentService.GetRootContent()
            .FirstOrDefault(c => c.ContentType.Alias == "settings");

        bool isNew = settings == null;
        if (isNew)
        {
            settings = contentService.Create("Settings", Constants.System.Root, settingsType.Alias);
        }

        // Only set nav links if currently empty (don't overwrite user edits)
        var existing = settings!.GetValue<string>("mobileNavLinks");
        if (!string.IsNullOrWhiteSpace(existing)) return;

        // Guard: verify mobileNavLinks property exists and uses the correct data type
        var mobileNavProperty = settingsType.PropertyTypes.FirstOrDefault(p => p.Alias == "mobileNavLinks");
        if (mobileNavProperty == null)
        {
            logger.LogWarning("PRISM: mobileNavLinks property not found on settings doc type. Saving without nav links.");
            contentService.Save(settings!);
            return;
        }

        var expectedDataTypeKey = new Guid("3b4c5d6e-7f80-9a1b-c2d3-e4f567890abc");
        logger.LogInformation("PRISM: About to publish Settings node. mobileNavLinks property data type key from content type: {Key}",
            mobileNavProperty.DataTypeKey);

        if (mobileNavProperty.DataTypeKey != expectedDataTypeKey)
        {
            logger.LogWarning("PRISM: mobileNavLinks property uses unexpected data type {Key}. Saving without nav links to avoid validation error.", mobileNavProperty.DataTypeKey);
            contentService.Save(settings!);
            contentService.Publish(settings!, new[] { "*" });
            return;
        }

        var navLinksJson = JsonSerializer.Serialize(new[]
        {
            new { name = "Home", target = "", type = "External", url = "/" },
            new { name = "Dashboard", target = "", type = "External", url = "/dashboard" }
        });
        settings.SetValue("mobileNavLinks", navLinksJson);

        var saveResult = contentService.Save(settings);
        if (saveResult.Success)
        {
            contentService.Publish(settings, new[] { "*" });
        }

        logger.LogInformation("PRISM StarterSeeder: EnsureSettingsDefaults complete");
    }
}
