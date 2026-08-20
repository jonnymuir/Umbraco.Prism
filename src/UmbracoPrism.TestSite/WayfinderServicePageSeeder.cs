using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds TestSite's two worked Wayfinder.Umbraco examples — "Apply for a juggling licence" (a
/// public, anonymous-first citizen self-service journey) and "Submit contributions file" / the
/// caseworker queue (the NJF Contributions Team's bulk-contributions worklist demo, including a
/// real downstream support-system call to Mock Business App). Mirrors
/// Wayfinder.Umbraco.ReferenceApp's own ReferenceContentSeeder: C# seeders own initial demo data;
/// uSync only captures subsequent portable edits made through the backoffice.
/// </summary>
/// <remarks>
/// Seeds definitions through <see cref="IServiceBlueprintSourceStore"/> — the same authoring-side
/// save path a backoffice edit uses — rather than inserting into the database directly, since
/// <c>UmbracoProcessManagerEngine</c> is a singleton that loads its definitions once at
/// construction; a raw DB insert made after that point (as every seeder's notification handler
/// runs) would never become visible to the running engine.
/// </remarks>
public class WayfinderServicePageSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IServiceBlueprintSourceStore workflowSourceStore,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<WayfinderServicePageSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    // Matches Wayfinder.Umbraco's own CreateServiceRequestStageBlock.cs/
    // CreateServiceRequestWorklistBlock.cs fixed element type keys.
    private static readonly Guid StageElementTypeKey = new("6f2a1c3d-8b4e-4a1f-9c6d-2e7b5a9f1c30");
    private static readonly Guid WorklistElementTypeKey = new("8b4c3e5f-0d6a-4c3b-9e8f-4a9d7c1b3e52");

    private static readonly JsonSerializerOptions BlockValueWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        try
        {
            await EnsureDefinitionSeededAsync(TestSiteSeedContract.JugglingLicenceBlueprintKey, "apply-for-a-juggling-licence.json", cancellationToken);
            await EnsureDefinitionSeededAsync(TestSiteSeedContract.ContributionsBlueprintKey, "bulk-contributions.json", cancellationToken);

            EnsureStagePage(TestSiteSeedContract.JugglingLicencePageName, TestSiteSeedContract.JugglingLicenceBlueprintKey);
            EnsureStagePage(TestSiteSeedContract.ContributionsPageName, TestSiteSeedContract.ContributionsBlueprintKey);
            EnsureWorklistPage(TestSiteSeedContract.CaseworkerQueuePageName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WAYFINDER SERVICE PAGE SEEDER: Unexpected error; skipping");
        }
    }

    private async Task EnsureDefinitionSeededAsync(string definitionKey, string fileName, CancellationToken cancellationToken)
    {
        var existing = await workflowSourceStore.LoadAsync(definitionKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogDebug("WAYFINDER SERVICE PAGE SEEDER: {Key} already present; leaving the existing (possibly edited) row untouched", definitionKey);
            return;
        }

        var path = Path.Combine(env.ContentRootPath, "service-blueprints", fileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Seed file not found at {Path}; skipping", path);
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var blueprint = JsonSerializer.Deserialize<ServiceBlueprint>(json, ReadOptions);
        if (blueprint is null)
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: {File} failed to deserialize; skipping", fileName);
            return;
        }

        var result = await workflowSourceStore.SaveAsync(blueprint, expectedVersion: 0, cancellationToken);
        if (!result.Saved)
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Save reported a conflict for {Key} (unexpected for a fresh seed); skipping", definitionKey);
            return;
        }

        logger.LogInformation("WAYFINDER SERVICE PAGE SEEDER: {Key} seeded and pushed to the live engine", definitionKey);
    }

    private void EnsureStagePage(string name, string blueprintKey)
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("WAYFINDER SERVICE PAGE SEEDER: homePage not found; skipping {Name}", name);
            return;
        }

        if (contentTypeService.Get(WayfinderServicePageContentType.Alias) == null)
        {
            logger.LogDebug("WAYFINDER SERVICE PAGE SEEDER: wayfinderServicePage doc type not found; skipping {Name} (run again once seeded)", name);
            return;
        }

        if (TestSiteSeedContract.FindWayfinderServicePageByName(contentService, name) != null)
        {
            return;
        }

        var page = contentService.Create(name, homePage.Id, WayfinderServicePageContentType.Alias);
        page.SetValue("stageArea", BuildBlockGridValueJson(StageElementTypeKey,
        [
            new BlockPropertyValue("blueprintKey", "Umbraco.TextBox", blueprintKey)
        ]));

        PublishOrLog(page, name);
    }

    private void EnsureWorklistPage(string name)
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("WAYFINDER SERVICE PAGE SEEDER: homePage not found; skipping {Name}", name);
            return;
        }

        if (contentTypeService.Get(WayfinderServicePageContentType.Alias) == null)
        {
            logger.LogDebug("WAYFINDER SERVICE PAGE SEEDER: wayfinderServicePage doc type not found; skipping {Name} (run again once seeded)", name);
            return;
        }

        if (TestSiteSeedContract.FindWayfinderServicePageByName(contentService, name) != null)
        {
            return;
        }

        var page = contentService.Create(name, homePage.Id, WayfinderServicePageContentType.Alias);
        page.SetValue("worklistArea", BuildBlockGridValueJson(WorklistElementTypeKey, []));

        PublishOrLog(page, name);
    }

    private void PublishOrLog(Umbraco.Cms.Core.Models.IContent page, string name)
    {
        var saveResult = contentService.Save(page);
        if (!saveResult.Success)
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Save failed for {Name} — {Reason}", name, saveResult.Result);
            return;
        }

#pragma warning disable CS0618
        var publishResult = contentService.Publish(page, ["*"], Constants.Security.SuperUserId);
#pragma warning restore CS0618
        if (!publishResult.Success)
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Publish failed for {Name} — {Reason}", name, publishResult.Result);
            return;
        }

        logger.LogInformation("WAYFINDER SERVICE PAGE SEEDER: Created and published {Name} content node", name);
    }

    private sealed record BlockPropertyValue(string Alias, string EditorAlias, object Value);

    /// <summary>
    /// The persisted <c>Umbraco.BlockGrid</c> property value shape — one block, no areas, full
    /// column span. Matches the exact JSON Umbraco's own Management API returns for a real
    /// backoffice-placed block (see Wayfinder.Umbraco.ReferenceApp's own ReferenceContentSeeder,
    /// which round-tripped this shape live through the real backoffice first).
    /// </summary>
    private static string BuildBlockGridValueJson(Guid contentTypeKey, IReadOnlyList<BlockPropertyValue> values)
    {
        var blockKey = Guid.NewGuid();
        var blockValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockGrid"] = new[]
                {
                    new
                    {
                        contentKey = blockKey,
                        settingsKey = (Guid?)null,
                        columnSpan = 12,
                        rowSpan = 1,
                        areas = Array.Empty<object>()
                    }
                }
            },
            contentData = new[]
            {
                new
                {
                    contentTypeKey,
                    key = blockKey,
                    values = values.Select(v => new
                    {
                        editorAlias = v.EditorAlias,
                        culture = (string?)null,
                        segment = (string?)null,
                        alias = v.Alias,
                        value = v.Value
                    })
                }
            },
            settingsData = Array.Empty<object>(),
            expose = new[]
            {
                new { contentKey = blockKey, culture = (string?)null, segment = (string?)null }
            }
        };

        return JsonSerializer.Serialize(blockValue, BlockValueWriteOptions);
    }
}
