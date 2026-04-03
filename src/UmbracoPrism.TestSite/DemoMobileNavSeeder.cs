using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds the TestSite Settings node with two default mobile nav items (Home + Dashboard),
/// each backed by an SVG icon written to /media/prism-nav-icons/ and registered in the
/// Umbraco media library under a "Prism Navigation Icons" folder.
///
/// Runs idempotently in Development only — skips if mobileNavLinks is already populated.
/// </summary>
public class DemoMobileNavSeeder(
    IWebHostEnvironment env,
    IContentService contentService,
    IMediaService mediaService,
    IRuntimeState runtimeState,
    ILogger<DemoMobileNavSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // Deterministic GUIDs so idempotency works across restarts.
    private static readonly Guid MediaFolderKey   = new("b5c6d7e8-f9a0-1234-efab-345678901234");
    private static readonly Guid HomeMediaKey     = new("d1e2f3a4-b5c6-7890-abcd-ef1234567890");
    private static readonly Guid DashMediaKey     = new("e2f3a4b5-c6d7-8901-bcde-f12345678901");
    private static readonly Guid HomeElementKey   = new("f3a4b5c6-d7e8-9012-cdef-123456789012");
    private static readonly Guid DashElementKey   = new("a4b5c6d7-e8f9-0123-defa-234567890123");

    // Must match MobileNavSchemaSetup.MobileNavItemTypeKey.
    private static readonly Guid MobileNavItemTypeKey = new("a9f4b2c1-3d5e-6f70-8912-34abc5678def");

    // Static media path — served by ASP.NET Core's static file middleware.
    private const string MediaBasePath = "/media/prism-nav-icons";

    private const string HomeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
          <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/>
        </svg>
        """;

    private const string DashboardSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
          <path d="M3 3h8v8H3V3zm10 0h8v8h-8V3zM3 13h8v8H3v-8zm10 0h8v8h-8v-8z"/>
        </svg>
        """;

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DEMO SEEDER: Unexpected error — safe to ignore.");
        }
    }

    private Task SeedAsync(CancellationToken ct)
    {
        var settings = contentService
            .GetRootContent()
            .FirstOrDefault(c => c.ContentType.Alias == "settings");

        if (settings == null)
        {
            logger.LogDebug("DEMO SEEDER: Settings node not found — skipping.");
            return Task.CompletedTask;
        }

        var existing = settings.GetValue<string>("mobileNavLinks");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            logger.LogDebug("DEMO SEEDER: mobileNavLinks already populated — skipping.");
            return Task.CompletedTask;
        }

        WriteSvgFiles();

        var homeKey  = EnsureIconMedia("Nav Icon - Home",      HomeMediaKey, "home.svg");
        var dashKey  = EnsureIconMedia("Nav Icon - Dashboard",  DashMediaKey, "dashboard.svg");

        var blockListJson = BuildBlockListJson(homeKey, dashKey);

        settings.SetValue("mobileNavLinks", blockListJson);
        contentService.Save(settings, null, null!);
#pragma warning disable CS0618
        contentService.Publish(settings, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618

        logger.LogInformation("DEMO SEEDER: Seeded mobile nav with Home and Dashboard items.");
        return Task.CompletedTask;
    }

    private void WriteSvgFiles()
    {
        var iconDir = Path.Combine(env.WebRootPath, "media", "prism-nav-icons");
        Directory.CreateDirectory(iconDir);

        var homePath = Path.Combine(iconDir, "home.svg");
        var dashPath = Path.Combine(iconDir, "dashboard.svg");

        if (!System.IO.File.Exists(homePath))  System.IO.File.WriteAllText(homePath,  HomeSvg,      Encoding.UTF8);
        if (!System.IO.File.Exists(dashPath))  System.IO.File.WriteAllText(dashPath,  DashboardSvg, Encoding.UTF8);

        logger.LogDebug("DEMO SEEDER: SVG icon files written to {Path}.", iconDir);
    }

    private Guid? EnsureIconMedia(string name, Guid key, string fileName)
    {
        // Check if a media item with this key already exists.
        var existing = mediaService.GetById(key);
        if (existing != null) return existing.Key;

        var folderId = EnsureIconsFolder();

        try
        {
            var media = mediaService.CreateMedia(name, folderId, "umbracoMediaVectorGraphics");
            media.Key = key;
            media.SetValue("umbracoFile", $"{MediaBasePath}/{fileName}");
#pragma warning disable CS0618
            mediaService.Save(media, Constants.Security.SuperUserId);
#pragma warning restore CS0618

            logger.LogInformation("DEMO SEEDER: Created media item '{Name}' (key: {Key}).", name, media.Key);
            return media.Key;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DEMO SEEDER: Could not create media item '{Name}'.", name);
            return null;
        }
    }

    private int EnsureIconsFolder()
    {
        try
        {
            // Search existing media at the root for our folder.
            var roots = mediaService.GetRootMedia();
            var folder = roots.FirstOrDefault(m => m.Key == MediaFolderKey);
            if (folder != null) return folder.Id;

            folder = mediaService.CreateMedia("Prism Navigation Icons", Constants.System.Root, "Folder");
            folder.Key = MediaFolderKey;
#pragma warning disable CS0618
            mediaService.Save(folder, Constants.Security.SuperUserId);
#pragma warning restore CS0618

            logger.LogInformation("DEMO SEEDER: Created 'Prism Navigation Icons' media folder.");
            return folder.Id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DEMO SEEDER: Could not create icons folder — placing icons at root.");
            return Constants.System.Root;
        }
    }

    /// <summary>
    /// Builds the Block List JSON for two nav items.
    /// The navIcon value uses the MediaPicker3 stored format:
    /// <c>[{"key":"{guid}","mediaKey":"{media-guid}","crops":[],"focalPoint":null}]</c>
    /// stored as an embedded JSON array inside contentData.
    /// </summary>
    private static string BuildBlockListJson(Guid? homeMediaKey, Guid? dashMediaKey)
    {
        var homeUdi = $"umb://element/{HomeElementKey:N}";
        var dashUdi = $"umb://element/{DashElementKey:N}";

        var root = new JsonObject
        {
            ["layout"] = new JsonObject
            {
                ["Umbraco.BlockList"] = new JsonArray(
                    new JsonObject { ["contentUdi"] = homeUdi },
                    new JsonObject { ["contentUdi"] = dashUdi }
                )
            },
            ["contentData"] = new JsonArray(
                BuildBlockItem(homeUdi, "Home",      "/",          homeMediaKey),
                BuildBlockItem(dashUdi, "Dashboard", "/dashboard", dashMediaKey)
            ),
            ["settingsData"] = new JsonArray()
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject BuildBlockItem(string udi, string label, string url, Guid? mediaKey)
    {
        var item = new JsonObject
        {
            ["contentTypeKey"] = MobileNavItemTypeKey.ToString(),
            ["udi"]            = udi,
            ["navLabel"]       = label,
            ["navUrl"]         = url,
            ["openInNewTab"]   = "0"
        };

        if (mediaKey.HasValue)
        {
            // MediaPicker3 stores an array of picker items; each has its own unique key + the media item key.
            var pickerItems = new JsonArray(new JsonObject
            {
                ["key"]        = Guid.NewGuid().ToString(),
                ["mediaKey"]   = mediaKey.Value.ToString(),
                ["crops"]      = new JsonArray(),
                ["focalPoint"] = JsonValue.Create((string?)null)
            });

            item["navIcon"] = pickerItems;
        }
        else
        {
            item["navIcon"] = JsonValue.Create((string?)null);
        }

        return item;
    }
}
