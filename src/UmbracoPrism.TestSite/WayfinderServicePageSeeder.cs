using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoPrism.Core.Models;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds TestSite's two worked Wayfinder.Umbraco examples — "Apply for a juggling licence" (a
/// public, anonymous-first citizen self-service journey) and "Submit contributions file" / the
/// caseworker queue (the NJF Contributions Team's bulk-contributions worklist demo, including a
/// real downstream support-system call to Mock Business App at interaction time — this seeder
/// itself makes no network calls, it's pure content/blueprint-definition creation). Mirrors
/// Wayfinder.Umbraco.ReferenceApp's own ReferenceContentSeeder: C# seeders own initial demo data;
/// uSync only captures subsequent portable edits made through the backoffice.
///
/// Runs whenever <c>Prism:SeedStarterContent</c> is enabled (see
/// <see cref="MobileNavSchemaSetup"/> for why this moved off a Development-only gate).
/// </summary>
/// <remarks>
/// Seeds definitions through <see cref="IServiceBlueprintSourceStore"/> — the same authoring-side
/// save path a backoffice edit uses — rather than inserting into the database directly, since
/// <c>UmbracoProcessManagerEngine</c> is a singleton that loads its definitions once at
/// construction; a raw DB insert made after that point (as every seeder's notification handler
/// runs) would never become visible to the running engine.
///
/// KNOWN LATENT RACE, pre-existing (not introduced by the config-gate change above): this class
/// depends on <c>PrismStarterContentSeeder</c> (Core) having already created the <c>homePage</c>
/// node and <see cref="WayfinderServicePageContentType"/> having already created the
/// <c>wayfinderServicePage</c> content type — both react to the same
/// <see cref="UmbracoApplicationStartedNotification"/>, and despite correct composer/DI
/// registration ordering (<c>[ComposeAfter(typeof(PrismComposer))]</c> on
/// <c>TestSiteComposer</c>), that does NOT guarantee this handler runs after theirs have fully
/// completed — confirmed empirically: on a genuinely first-ever boot of an empty database this
/// seeder silently no-ops (its own existence checks correctly find nothing to build on yet,
/// log at Debug, and return — see <c>EnsureStagePage</c>), then succeeds cleanly on the very
/// next restart against that same, now-populated database, no code change. Self-heals on any
/// later boot since every check here is a live existence check, not a one-shot flag — so a
/// redeploy/restart is always a safe, sufficient workaround. Not fixed here: doing so properly
/// means either making this seeder resilient to running before its dependencies (retry/wait) or
/// restructuring so it doesn't depend on notification-handler ordering at all — a bigger, more
/// careful change than this pass warrants.
/// </remarks>
public class WayfinderServicePageSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IServiceBlueprintSourceStore workflowSourceStore,
    IWebHostEnvironment env,
    IOptions<PrismConfiguration> prismConfig,
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
        if (!prismConfig.Value.SeedStarterContent) return;

        try
        {
            await EnsureDefinitionSeededAsync(TestSiteSeedContract.JugglingLicenceBlueprintKey, "apply-for-a-juggling-licence.json", cancellationToken);
            await EnsureDefinitionSeededAsync(TestSiteSeedContract.ContributionsBlueprintKey, "bulk-contributions.json", cancellationToken);
            await EnsureDefinitionSeededAsync(TestSiteSeedContract.MoneyModellerBlueprintKey, "money-modeller.json", cancellationToken);

            EnsureStagePage(TestSiteSeedContract.JugglingLicencePageName, TestSiteSeedContract.JugglingLicenceBlueprintKey);
            EnsureStagePage(TestSiteSeedContract.ContributionsPageName, TestSiteSeedContract.ContributionsBlueprintKey);
            EnsureWorklistPage(TestSiteSeedContract.CaseworkerQueuePageName);
            // Money Modeller's own web-user queue is the citizen-facing part (model savings pot
            // scenarios, hand a chosen one off as a quote request) — exactly what a stage page
            // hosts. Its business-user queue (reviewing/issuing the formal quote) is a genuine
            // second half of the blueprint, same shape as bulk-contributions' caseworker side,
            // but nothing asked for that admin view yet — only the citizen-facing modeller was
            // the point (showing off graph/calculation-heavy UI working well on mobile), so no
            // worklist page for it here.
            EnsureStagePage(TestSiteSeedContract.MoneyModellerPageName, TestSiteSeedContract.MoneyModellerBlueprintKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WAYFINDER SERVICE PAGE SEEDER: Unexpected error; skipping");
        }
    }

    // Stamped into the saved blueprint's own Tags so a later boot can tell "the checked-in seed
    // file changed since this was last synced" apart from "this was hand-edited live (via the
    // backoffice or an MCP-driven walkthrough) since it was last synced" — only the former should
    // ever silently overwrite what's in the database. Deliberately a hash of the FILE's raw
    // content, not the deserialized-and-reserialized ServiceBlueprint (which would need every
    // nested type to round-trip identically through JSON to compare reliably) — simpler and just
    // as sufficient for "did the source of truth change at all".
    private const string SeedSourceHashTagKey = "_prismSeedSourceHash";

    private async Task EnsureDefinitionSeededAsync(string definitionKey, string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(env.ContentRootPath, "service-blueprints", fileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Seed file not found at {Path}; skipping", path);
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var fileHash = ComputeHash(json);

        var existing = await workflowSourceStore.LoadAsync(definitionKey, cancellationToken);
        if (existing is not null)
        {
            var storedHash = existing.Tags?.GetValueOrDefault(SeedSourceHashTagKey);
            if (storedHash == fileHash)
            {
                logger.LogDebug("WAYFINDER SERVICE PAGE SEEDER: {Key} already up to date with its seed file; leaving as-is", definitionKey);
                return;
            }

            // storedHash is null (a definition seeded before this hash-tracking existed) or
            // differs from the file's current hash — either way the checked-in file is this
            // reference app's source of truth for these three demo blueprints (see this class's
            // own remarks), so re-sync it. A row that was instead hand-edited live keeps its
            // stored hash matching whatever the FILE was last synced to, so it's left untouched
            // here unless the file itself also changed.
            var updatedBlueprint = DeserializeOrWarn(json, fileName);
            if (updatedBlueprint is null) return;
            updatedBlueprint = updatedBlueprint with { Tags = MergeTag(updatedBlueprint.Tags, SeedSourceHashTagKey, fileHash) };

            var updateResult = await workflowSourceStore.SaveAsync(updatedBlueprint, existing.Version, cancellationToken);
            if (!updateResult.Saved)
            {
                logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Re-sync reported a conflict for {Key} (expected version {Version}); leaving the existing row untouched", definitionKey, existing.Version);
                return;
            }

            logger.LogInformation("WAYFINDER SERVICE PAGE SEEDER: {Key} re-synced from its updated seed file and pushed to the live engine", definitionKey);
            return;
        }

        var blueprint = DeserializeOrWarn(json, fileName);
        if (blueprint is null) return;
        blueprint = blueprint with { Tags = MergeTag(blueprint.Tags, SeedSourceHashTagKey, fileHash) };

        var result = await workflowSourceStore.SaveAsync(blueprint, expectedVersion: 0, cancellationToken);
        if (!result.Saved)
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: Save reported a conflict for {Key} (unexpected for a fresh seed); skipping", definitionKey);
            return;
        }

        logger.LogInformation("WAYFINDER SERVICE PAGE SEEDER: {Key} seeded and pushed to the live engine", definitionKey);
    }

    private ServiceBlueprint? DeserializeOrWarn(string json, string fileName)
    {
        var blueprint = JsonSerializer.Deserialize<ServiceBlueprint>(json, ReadOptions);
        if (blueprint is null)
        {
            logger.LogWarning("WAYFINDER SERVICE PAGE SEEDER: {File} failed to deserialize; skipping", fileName);
        }

        return blueprint;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16];
    }

    private static IReadOnlyDictionary<string, string> MergeTag(IReadOnlyDictionary<string, string>? existingTags, string key, string value)
    {
        var merged = existingTags is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(existingTags);
        merged[key] = value;
        return merged;
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
