using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds a demo "Get in Touch" content node of type <c>workflowPage</c> at
/// <c>/get-in-touch</c> so the route-hijacking controller has a real content
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
            CleanupOldRetirementQuotePage();
            EnsureCommunityEnquiryPage();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WORKFLOW PAGE SEEDER: Unexpected error; skipping");
        }

        return Task.CompletedTask;
    }

    private void CleanupOldRetirementQuotePage()
    {
        // Delete the old "Retirement Quote" demo node if it exists
        var oldPage = contentService
            .GetRootContent()
            .FirstOrDefault(c => c.ContentType.Alias == "workflowPage"
                              && (string.Equals(c.Name, "Retirement Quote", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(c.GetValue<string>("workflowKey"), "retirement-quote", StringComparison.OrdinalIgnoreCase)));

        if (oldPage != null)
        {
            logger.LogInformation("WORKFLOW PAGE SEEDER: Deleting old 'Retirement Quote' node (id={Id})", oldPage.Id);
            var deleteResult = contentService.Delete(oldPage);
            if (deleteResult.Success)
                logger.LogInformation("WORKFLOW PAGE SEEDER: Old demo node deleted successfully");
            else
                logger.LogWarning("WORKFLOW PAGE SEEDER: Delete failed — {Reason}", deleteResult.Result);
        }
    }

    private void EnsureCommunityEnquiryPage()
    {
        var contentType = contentTypeService.Get("workflowPage");
        if (contentType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: workflowPage doc type not found; skipping (run again after seeder)");
            return;
        }

        // Check if the new community-enquiry node already exists (by name OR workflowKey)
        var existing = contentService
            .GetRootContent()
            .FirstOrDefault(c => c.ContentType.Alias == "workflowPage"
                              && (string.Equals(c.Name, "Get in Touch", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(c.GetValue<string>("workflowKey"), "community-enquiry", StringComparison.OrdinalIgnoreCase)));

        if (existing != null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: 'Get in Touch' content node already exists");
            return;
        }

        logger.LogInformation("WORKFLOW PAGE SEEDER: Creating 'Get in Touch' content node");

        var page = contentService.Create("Get in Touch", Constants.System.Root, "workflowPage");
        page.SetValue("workflowKey", "community-enquiry");

        var saveResult = contentService.Save(page);
        if (!saveResult.Success)
        {
            logger.LogWarning("WORKFLOW PAGE SEEDER: Save failed — {Reason}", saveResult.Result);
            return;
        }

        var publishResult = contentService.Publish(page, Array.Empty<string>());
        if (publishResult.Success)
            logger.LogInformation("WORKFLOW PAGE SEEDER: 'Get in Touch' published (id={Id})", page.Id);
        else
            logger.LogWarning("WORKFLOW PAGE SEEDER: Publish failed — {Reason}", publishResult.Result);
    }
}
