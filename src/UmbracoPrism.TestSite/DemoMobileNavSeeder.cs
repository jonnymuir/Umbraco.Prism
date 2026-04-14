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
/// Seeds the TestSite Settings node with the stable auth-flow mobile nav items
/// (Home, Dashboard, My Workflows),
/// each backed by an SVG icon written to /media/prism-nav-icons/ and registered in the
/// Umbraco media library under a "Prism Navigation Icons" folder.
///
/// Runs idempotently in Development only and repairs stale/missing nav contracts.
/// </summary>
public class DemoMobileNavSeeder(
    IWebHostEnvironment env,
    IContentService contentService,
    IContentTypeService contentTypeService,
    IMediaService mediaService,
    IRuntimeState runtimeState,
    ILogger<DemoMobileNavSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // Deterministic GUIDs for idempotency. Only the folder uses a fixed key (reliable across restarts).
    // Media items are looked up by name to avoid key-persistence issues with the deprecated Save API.
    private static readonly Guid MediaFolderKey = new("b5c6d7e8-f9a0-1234-efab-345678901234");
    private static readonly Guid HomeElementKey = new("f3a4b5c6-d7e8-4012-cdef-123456789012");
    private static readonly Guid DashElementKey = new("a4b5c6d7-e8f9-4123-defa-234567890123");
    private static readonly Guid WorkflowsElementKey = new("b5c6d7e8-f9a0-4234-efab-456789012345");

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

    private const string WorkflowsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
          <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14H7v-2h5v2zm5-4H7v-2h10v2zm0-4H7V7h10v2z"/>
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
        var settings = EnsureSettingsNode();

        if (settings == null)
        {
            logger.LogDebug("DEMO SEEDER: Settings node not found — skipping.");
            return Task.CompletedTask;
        }

        // Always ensure SVG files and media library items exist, regardless of content state.
        WriteSvgFiles();
        var folderId = EnsureIconsFolder();
        var homeKey = EnsureIconMedia("Nav Icon - Home",      folderId, "home.svg");
        var dashKey = EnsureIconMedia("Nav Icon - Dashboard", folderId, "dashboard.svg");
        var workflowsKey = EnsureIconMedia("Nav Icon - Workflows", folderId, "workflows.svg");

        var existing = settings.GetValue<string>("mobileNavLinks");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            bool isV14BlockList = existing.Contains("\"Umbraco.BlockList\"", StringComparison.Ordinal)
                               && !existing.Contains("\"contentUdi\":", StringComparison.Ordinal)
                               && existing.Contains("\"expose\":", StringComparison.Ordinal);
            bool hasHomeItem = existing.Contains(TestSiteSeedContract.HomePageUrl, StringComparison.Ordinal);
            bool hasDashboardItem = existing.Contains(TestSiteSeedContract.DashboardUrl, StringComparison.Ordinal);
            bool hasWorkflowsItem = existing.Contains(TestSiteSeedContract.WorkflowHubUrl, StringComparison.Ordinal);

            if (isV14BlockList && hasHomeItem && hasDashboardItem && hasWorkflowsItem)
            {
                logger.LogDebug("DEMO SEEDER: mobileNavLinks already populated — skipping content seed.");
                return Task.CompletedTask;
            }

            logger.LogInformation("DEMO SEEDER: Replacing mobileNavLinks to restore the seeded auth-flow contract.");
        }

        var blockListJson = BuildBlockListJson(homeKey, dashKey, workflowsKey);

        settings.SetValue("mobileNavLinks", blockListJson);
        contentService.Save(settings, null, null!);
#pragma warning disable CS0618
        contentService.Publish(settings, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618

        logger.LogInformation("DEMO SEEDER: Seeded mobile nav with Home, Dashboard, and My Workflows items.");
        return Task.CompletedTask;
    }

    private IContent? EnsureSettingsNode()
    {
        var settings = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.SettingsAlias);
        if (settings != null)
        {
            return settings;
        }

        var settingsType = contentTypeService.Get(TestSiteSeedContract.SettingsAlias);
        if (settingsType == null)
        {
            return null;
        }

        logger.LogInformation("DEMO SEEDER: Creating seeded Settings node for mobile navigation.");
        settings = contentService.Create(TestSiteSeedContract.SettingsName, Constants.System.Root, TestSiteSeedContract.SettingsAlias);
        var saveResult = contentService.Save(settings);
        if (!saveResult.Success)
        {
            logger.LogWarning("DEMO SEEDER: Could not create Settings node — {Reason}", saveResult.Result);
            return null;
        }

#pragma warning disable CS0618
        var publishResult = contentService.Publish(settings, Array.Empty<string>(), Constants.Security.SuperUserId);
#pragma warning restore CS0618
        if (!publishResult.Success)
        {
            logger.LogWarning("DEMO SEEDER: Could not publish Settings node — {Reason}", publishResult.Result);
            return null;
        }

        return settings;
    }

    private void WriteSvgFiles()
    {
        var iconDir = Path.Combine(env.WebRootPath, "media", "prism-nav-icons");
        Directory.CreateDirectory(iconDir);

        var homePath = Path.Combine(iconDir, "home.svg");
        var dashPath = Path.Combine(iconDir, "dashboard.svg");

        if (!System.IO.File.Exists(homePath))  System.IO.File.WriteAllText(homePath,  HomeSvg,       Encoding.UTF8);
        if (!System.IO.File.Exists(dashPath))  System.IO.File.WriteAllText(dashPath,  DashboardSvg,  Encoding.UTF8);

        var workflowsPath = Path.Combine(iconDir, "workflows.svg");
        if (!System.IO.File.Exists(workflowsPath)) System.IO.File.WriteAllText(workflowsPath, WorkflowsSvg, Encoding.UTF8);

        logger.LogDebug("DEMO SEEDER: SVG icon files written to {Path}.", iconDir);
    }

    private Guid? EnsureIconMedia(string name, int folderId, string fileName)
    {
        var fileValue = $"{MediaBasePath}/{fileName}";

        // Find by name within the folder. Key-based lookup is unreliable because Umbraco's
        // deprecated Save() API may not preserve a manually-assigned Key.
        var existing = mediaService.GetPagedChildren(folderId, 0, 100, out _)
            .FirstOrDefault(m => m.Name == name);

        if (existing != null)
        {
            // Repair missing file reference if the item was created but never got its value set.
            var currentFile = existing.GetValue<string>("umbracoFile");
            if (string.IsNullOrEmpty(currentFile))
            {
                existing.SetValue("umbracoFile", fileValue);
#pragma warning disable CS0618
                mediaService.Save(existing, Constants.Security.SuperUserId);
#pragma warning restore CS0618
                logger.LogInformation("DEMO SEEDER: Repaired empty file reference on media item '{Name}'.", name);
            }
            return existing.Key;
        }

        try
        {
            // Try the SVG media type first; fall back to Image if it is not installed.
            IMedia? media = null;
            foreach (var alias in new[] { "umbracoMediaVectorGraphics", "Image", "File" })
            {
                try
                {
                    media = mediaService.CreateMedia(name, folderId, alias);
                    break;
                }
                catch (Exception innerEx)
                {
                    logger.LogDebug(innerEx, "DEMO SEEDER: Media type '{Alias}' unavailable for '{Name}', trying next.", alias, name);
                }
            }

            if (media == null)
            {
                logger.LogWarning("DEMO SEEDER: No suitable media type found for '{Name}' — icon skipped.", name);
                return null;
            }

            media.SetValue("umbracoFile", fileValue);
#pragma warning disable CS0618
            mediaService.Save(media, Constants.Security.SuperUserId);
#pragma warning restore CS0618

            // Re-fetch by integer ID to get the actual persisted key from the database.
            var saved = mediaService.GetById(media.Id);
            var actualKey = saved?.Key ?? media.Key;
            logger.LogInformation("DEMO SEEDER: Created media item '{Name}' (key: {Key}).", name, actualKey);
            return actualKey;
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
            // Search existing media at the root for our folder by key, then by name as fallback.
            var roots = mediaService.GetRootMedia();
            var folder = roots.FirstOrDefault(m => m.Key == MediaFolderKey)
                      ?? roots.FirstOrDefault(m => m.Name == "Prism Navigation Icons");
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
    /// Builds the Block List JSON using the Umbraco v14+ format:
    /// layout entries use <c>contentKey</c> (Guid), and contentData items use a <c>values</c> array
    /// instead of flat properties. This allows the backoffice label template (e.g. <c>{{ navLabel }}</c>)
    /// to resolve correctly, since the TypeScript interpolation reads from the <c>values</c> array.
    /// </summary>
    private static string BuildBlockListJson(Guid? homeMediaKey, Guid? dashMediaKey, Guid? workflowsMediaKey)
    {
        var homeKey      = HomeElementKey.ToString();
        var dashKey      = DashElementKey.ToString();
        var workflowsKey = WorkflowsElementKey.ToString();

        var root = new JsonObject
        {
            ["layout"] = new JsonObject
            {
                ["Umbraco.BlockList"] = new JsonArray(
                    new JsonObject { ["contentKey"] = homeKey },
                    new JsonObject { ["contentKey"] = dashKey },
                    new JsonObject { ["contentKey"] = workflowsKey }
                )
            },
            ["contentData"] = new JsonArray(
                BuildBlockItem(homeKey,      "Home",         TestSiteSeedContract.HomePageUrl,   homeMediaKey),
                BuildBlockItem(dashKey,      "Dashboard",    TestSiteSeedContract.DashboardUrl,  dashMediaKey),
                BuildBlockItem(workflowsKey, "My Workflows", TestSiteSeedContract.WorkflowHubUrl, workflowsMediaKey)
            ),
            ["settingsData"] = new JsonArray(),
            ["expose"] = new JsonArray(
                new JsonObject { ["contentKey"] = homeKey,      ["culture"] = null, ["segment"] = null },
                new JsonObject { ["contentKey"] = dashKey,      ["culture"] = null, ["segment"] = null },
                new JsonObject { ["contentKey"] = workflowsKey, ["culture"] = null, ["segment"] = null }
            )
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject BuildBlockItem(string key, string label, string url, Guid? mediaKey)
    {
        var values = new JsonArray
        {
            new JsonObject { ["alias"] = "navLabel",     ["culture"] = null, ["segment"] = null, ["value"] = JsonValue.Create(label) },
            new JsonObject { ["alias"] = "navUrl",       ["culture"] = null, ["segment"] = null, ["value"] = JsonValue.Create(url) },
            new JsonObject { ["alias"] = "openInNewTab", ["culture"] = null, ["segment"] = null, ["value"] = JsonValue.Create(0) }
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

            values.Add(new JsonObject { ["alias"] = "navIcon", ["culture"] = null, ["segment"] = null, ["value"] = pickerItems });
        }
        else
        {
            values.Add(new JsonObject { ["alias"] = "navIcon", ["culture"] = null, ["segment"] = null, ["value"] = JsonValue.Create((string?)null) });
        }

        return new JsonObject
        {
            ["contentTypeKey"] = MobileNavItemTypeKey.ToString(),
            ["key"]            = key,
            ["values"]         = values
        };
    }
}
