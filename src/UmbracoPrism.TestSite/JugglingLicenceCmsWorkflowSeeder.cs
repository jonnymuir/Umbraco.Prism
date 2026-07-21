using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds the "Apply for a juggling licence" CMS Workflow demo — the reference example proving
/// Prism CMS Workflow's backoffice-authored, uSync-portable, in-Umbraco-only implementation.
/// Mirrors <see cref="WorkflowPageSeeder"/>'s convention exactly: C# seeders own initial demo
/// data; uSync only captures subsequent portable edits made through the backoffice.
/// </summary>
/// <remarks>
/// Seeds the definition through <see cref="IWorkflowSourceStore"/> — the same authoring-side
/// save path a backoffice edit uses — rather than inserting into the database directly.
/// <c>CmsWorkflowEngine</c> is a singleton that loads its definitions once at construction, so a
/// raw DB insert made after that (as every seeder's notification handler runs) would never
/// become visible to the running engine; <c>IWorkflowSourceStore.SaveAsync</c> is what pushes a
/// new/changed definition into the live engine immediately (see
/// <c>UmbracoCmsWorkflowDefinitionStore</c>'s own remarks) — a raw DB write bypasses that
/// entirely regardless of source (seeder, migration, manual query).
/// </remarks>
public class JugglingLicenceCmsWorkflowSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IWorkflowSourceStore workflowSourceStore,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<JugglingLicenceCmsWorkflowSeeder> logger)
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
        var existing = await workflowSourceStore.LoadAsync(TestSiteSeedContract.JugglingLicenceWorkflowKey, cancellationToken);
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
        var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, ReadOptions);
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

        var contentType = contentTypeService.Get(TestSiteSeedContract.CmsWorkflowPageAlias);
        if (contentType == null)
        {
            logger.LogDebug("JUGGLING LICENCE SEEDER: cmsWorkflowPage doc type not found; skipping (run again after the content-type seeder)");
            return;
        }

        // Filtered by workflowKey, not just alias — a second cmsWorkflowPage instance now exists
        // (transfer-a-juggling-licence's seeded reference), and a bare alias match would find
        // whichever one happens to be first in the tree and wrongly skip creating this one.
        var existing = TestSiteSeedContract.FindCmsWorkflowPageByKey(
            contentService, TestSiteSeedContract.JugglingLicenceWorkflowKey);
        if (existing != null)
        {
            return;
        }

        logger.LogInformation("JUGGLING LICENCE SEEDER: Creating seeded CMS workflow page");

        var page = contentService.Create(
            TestSiteSeedContract.JugglingLicencePageName, homePage.Id, TestSiteSeedContract.CmsWorkflowPageAlias);
        page.SetValue("workflowKey", TestSiteSeedContract.JugglingLicenceWorkflowKey);

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
