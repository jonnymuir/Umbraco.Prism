using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds the "Apply for a juggling licence" CMS Workflow demo — the reference example proving
/// Prism CMS Workflow's backoffice-authored, uSync-portable, in-Umbraco-only implementation.
/// Mirrors <see cref="TouchpointPageSeeder"/>'s convention exactly: C# seeders own initial demo
/// data; uSync only captures subsequent portable edits made through the backoffice.
/// </summary>
/// <remarks>
/// Seeds the definition through <see cref="IServiceBlueprintSourceStore"/> — the same authoring-side
/// save path a backoffice edit uses — rather than inserting into the database directly.
/// <c>CmsProcessManager</c> is a singleton that loads its definitions once at construction, so a
/// raw DB insert made after that (as every seeder's notification handler runs) would never
/// become visible to the running engine; <c>IServiceBlueprintSourceStore.SaveAsync</c> is what pushes a
/// new/changed definition into the live engine immediately (see
/// <c>UmbracoCmsServiceBlueprintStore</c>'s own remarks) — a raw DB write bypasses that
/// entirely regardless of source (seeder, migration, manual query).
/// </remarks>
public class JugglingLicenceCmsServiceBlueprintSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IServiceBlueprintSourceStore workflowSourceStore,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<JugglingLicenceCmsServiceBlueprintSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;
        if (!env.IsDevelopment()) return;

        try
        {
            await EnsureDefinitionSeededAsync(cancellationToken);
            EnsureContentPageUnderHome();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JUGGLING LICENCE SEEDER: Unexpected error; skipping");
        }
    }

    private async Task EnsureDefinitionSeededAsync(CancellationToken cancellationToken)
    {
        var existing = await workflowSourceStore.LoadAsync(TestSiteSeedContract.JugglingLicenceBlueprintKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogDebug("JUGGLING LICENCE SEEDER: Definition already present; leaving the existing (possibly edited) row untouched");
            return;
        }

        var path = Path.Combine(env.ContentRootPath, "cms-workflow-seeds", "apply-for-a-juggling-licence.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("JUGGLING LICENCE SEEDER: Seed file not found at {Path}; skipping", path);
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var workflow = JsonSerializer.Deserialize<ServiceBlueprint>(json, ReadOptions);
        if (workflow is null)
        {
            logger.LogWarning("JUGGLING LICENCE SEEDER: Seed file failed to deserialize; skipping");
            return;
        }

        var result = await workflowSourceStore.SaveAsync(workflow, expectedVersion: 0, cancellationToken);
        if (!result.Saved)
        {
            logger.LogWarning("JUGGLING LICENCE SEEDER: Save reported a conflict (unexpected for a fresh seed); skipping");
            return;
        }

        logger.LogInformation("JUGGLING LICENCE SEEDER: Definition seeded and pushed to the live engine");
    }

    private void EnsureContentPageUnderHome()
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("JUGGLING LICENCE SEEDER: homePage not found; skipping content seed");
            return;
        }

        var contentType = contentTypeService.Get(TestSiteSeedContract.CmsServiceRequestPageAlias);
        if (contentType == null)
        {
            logger.LogDebug("JUGGLING LICENCE SEEDER: cmsServiceRequestPage doc type not found; skipping (run again after the content-type seeder)");
            return;
        }

        // Filtered by blueprintKey, not just alias — a second cmsServiceRequestPage instance now exists
        // (transfer-a-juggling-licence's seeded reference), and a bare alias match would find
        // whichever one happens to be first in the tree and wrongly skip creating this one.
        var existing = TestSiteSeedContract.FindCmsServiceRequestPageByKey(
            contentService, TestSiteSeedContract.JugglingLicenceBlueprintKey);
        if (existing != null)
        {
            return;
        }

        logger.LogInformation("JUGGLING LICENCE SEEDER: Creating seeded CMS workflow page");

        var page = contentService.Create(
            TestSiteSeedContract.JugglingLicencePageName, homePage.Id, TestSiteSeedContract.CmsServiceRequestPageAlias);
        page.SetValue("blueprintKey", TestSiteSeedContract.JugglingLicenceBlueprintKey);

        var saveResult = contentService.Save(page);
        if (!saveResult.Success)
        {
            logger.LogWarning("JUGGLING LICENCE SEEDER: Save failed — {Reason}", saveResult.Result);
            return;
        }

        var publishResult = contentService.Publish(page, Array.Empty<string>());
        if (!publishResult.Success)
        {
            logger.LogWarning("JUGGLING LICENCE SEEDER: Publish failed — {Reason}", publishResult.Result);
        }
    }
}
