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
/// Seeds "Transfer a Professional Juggling Licence" — the same definition
/// <c>tests/demo/licence-transfer-demo.spec.ts</c> has an AI agent design and save live over MCP,
/// captured afterwards as a "here's one we made earlier" reference so it survives a runtime wipe
/// without needing a real agent run. Mirrors <see cref="JugglingLicenceCmsWorkflowSeeder"/>
/// exactly, including the same reasoning for why <see cref="IWorkflowSourceStore"/> (not a raw
/// DB insert) is the correct save path.
/// </summary>
/// <remarks>
/// A future re-recording of the MCP walkthrough will hit a version conflict saving under this
/// same key, since the seeder will have already created version 1 — delete the seeded definition
/// (and its content page/nav entry) via the backoffice first, let the agent create it fresh, then
/// this seeder will happily recreate the "here's one we made earlier" reference once you're done.
/// </remarks>
public class LicenceTransferCmsWorkflowSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IWorkflowSourceStore workflowSourceStore,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<LicenceTransferCmsWorkflowSeeder> logger)
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
            logger.LogWarning(ex, "LICENCE TRANSFER SEEDER: Unexpected error; skipping");
        }
    }

    private async Task EnsureDefinitionSeededAsync(CancellationToken cancellationToken)
    {
        var existing = await workflowSourceStore.LoadAsync(TestSiteSeedContract.JugglingLicenceTransferWorkflowKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogDebug("LICENCE TRANSFER SEEDER: Definition already present; leaving the existing (possibly edited) row untouched");
            return;
        }

        var path = Path.Combine(env.ContentRootPath, "cms-workflow-seeds", "transfer-a-juggling-licence.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("LICENCE TRANSFER SEEDER: Seed file not found at {Path}; skipping", path);
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, ReadOptions);
        if (workflow is null)
        {
            logger.LogWarning("LICENCE TRANSFER SEEDER: Seed file failed to deserialize; skipping");
            return;
        }

        var result = await workflowSourceStore.SaveAsync(workflow, expectedVersion: 0, cancellationToken);
        if (!result.Saved)
        {
            logger.LogWarning("LICENCE TRANSFER SEEDER: Save reported a conflict (unexpected for a fresh seed); skipping");
            return;
        }

        logger.LogInformation("LICENCE TRANSFER SEEDER: Definition seeded and pushed to the live engine");
    }

    private void EnsureContentPageUnderHome()
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("LICENCE TRANSFER SEEDER: homePage not found; skipping content seed");
            return;
        }

        var contentType = contentTypeService.Get(TestSiteSeedContract.CmsWorkflowPageAlias);
        if (contentType == null)
        {
            logger.LogDebug("LICENCE TRANSFER SEEDER: cmsWorkflowPage doc type not found; skipping (run again after the content-type seeder)");
            return;
        }

        var existing = TestSiteSeedContract.FindCmsWorkflowPageByKey(
            contentService, TestSiteSeedContract.JugglingLicenceTransferWorkflowKey);
        if (existing != null)
        {
            return;
        }

        logger.LogInformation("LICENCE TRANSFER SEEDER: Creating seeded CMS workflow page");

        var page = contentService.Create(
            TestSiteSeedContract.LicenceTransferPageName, homePage.Id, TestSiteSeedContract.CmsWorkflowPageAlias);
        page.SetValue("workflowKey", TestSiteSeedContract.JugglingLicenceTransferWorkflowKey);

        var saveResult = contentService.Save(page);
        if (!saveResult.Success)
        {
            logger.LogWarning("LICENCE TRANSFER SEEDER: Save failed — {Reason}", saveResult.Result);
            return;
        }

        var publishResult = contentService.Publish(page, Array.Empty<string>());
        if (!publishResult.Success)
        {
            logger.LogWarning("LICENCE TRANSFER SEEDER: Publish failed — {Reason}", publishResult.Result);
        }
    }
}
