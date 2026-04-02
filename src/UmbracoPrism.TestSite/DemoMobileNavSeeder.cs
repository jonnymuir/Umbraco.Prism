using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds demo <c>mobileNavLinks</c> into the Umbraco Settings content node on startup
/// so developers can immediately see the mobile nav without manual backoffice setup.
/// Only runs in Development; idempotent — skips if values are already present.
/// </summary>
public class DemoMobileNavSeeder(
    IWebHostEnvironment env,
    IContentService contentService,
    IRuntimeState runtimeState,
    ILogger<DemoMobileNavSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly object[] DemoLinks =
    [
        new { name = "Home",     target = "", type = "External", url = "/" },
        new { name = "Account",  target = "", type = "External", url = "/account" },
        new { name = "Settings", target = "", type = "External", url = "/settings" },
        new { name = "Help",     target = "", type = "External", url = "/help" },
    ];

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        await Task.Run(() => SeedMobileNavLinks(), cancellationToken);
    }

    private void SeedMobileNavLinks()
    {
        try
        {
            var settings = contentService
                .GetRootContent()
                .FirstOrDefault(c => c.ContentType.Alias == "settings");

            if (settings == null)
            {
                logger.LogDebug("DEMO SEEDER: Settings node not found — skipping mobileNavLinks seed.");
                return;
            }

            var existing = settings.GetValue<string>("mobileNavLinks");
            if (!string.IsNullOrWhiteSpace(existing))
            {
                logger.LogDebug("DEMO SEEDER: mobileNavLinks already configured — skipping.");
                return;
            }

            var json = JsonSerializer.Serialize(DemoLinks);
            settings.SetValue("mobileNavLinks", json);

            var saveResult = contentService.Save(settings);
            if (!saveResult.Success)
            {
                logger.LogWarning("DEMO SEEDER: Failed to save Settings node: {Status}", saveResult.Result);
                return;
            }

            contentService.Publish(settings, ["*"]);
            logger.LogInformation("DEMO SEEDER: Seeded 4 demo mobileNavLinks into Settings node.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DEMO SEEDER: Unexpected error seeding mobileNavLinks — safe to ignore.");
        }
    }
}
