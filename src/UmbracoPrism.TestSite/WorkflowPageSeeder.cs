using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds a demo "Retirement Quote" content node of type <c>workflowPage</c> at
/// <c>/retirement-quote</c> so the route-hijacking controller has a real content
/// node to intercept.  Development-only, idempotent.
/// </summary>
public class WorkflowPageSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<WorkflowPageSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return Task.CompletedTask;
        if (!env.IsDevelopment()) return Task.CompletedTask;

        try
        {
            EnsureRetirementQuotePage();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WORKFLOW PAGE SEEDER: Unexpected error; skipping");
        }

        return Task.CompletedTask;
    }

    private void EnsureRetirementQuotePage()
    {
        var contentType = contentTypeService.Get("workflowPage");
        if (contentType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: workflowPage doc type not found; skipping (run again after seeder)");
            return;
        }

        // Check if a workflowPage node with this name already exists
        var existing = contentService
            .GetRootContent()
            .FirstOrDefault(c => c.ContentType.Alias == "workflowPage"
                              && string.Equals(c.Name, "Retirement Quote", StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: 'Retirement Quote' content node already exists");
            return;
        }

        logger.LogInformation("WORKFLOW PAGE SEEDER: Creating 'Retirement Quote' content node");

        var page = contentService.Create("Retirement Quote", Constants.System.Root, "workflowPage");
        page.SetValue("workflowKey", "retirement-quote");

        var saveResult = contentService.Save(page);
        if (!saveResult.Success)
        {
            logger.LogWarning("WORKFLOW PAGE SEEDER: Save failed — {Reason}", saveResult.Result);
            return;
        }

        var publishResult = contentService.Publish(page, Array.Empty<string>());
        if (publishResult.Success)
            logger.LogInformation("WORKFLOW PAGE SEEDER: 'Retirement Quote' published (id={Id})", page.Id);
        else
            logger.LogWarning("WORKFLOW PAGE SEEDER: Publish failed — {Reason}", publishResult.Result);
    }
}
