using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Ensures the seeded member journey content contract exists for the local
/// auth/workflow flows: Home, Dashboard, Get in Touch, and My Workflows.
/// Development-only and idempotent.
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
            EnsureHomeAndDashboard();
            CleanupOldRetirementQuotePage();
            EnsureCommunityEnquiryPage();
            EnsurePlanningWorkflowPage();
            EnsureWorkflowHubPage();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WORKFLOW PAGE SEEDER: Unexpected error; skipping");
        }

        return Task.CompletedTask;
    }

    private void CleanupOldRetirementQuotePage()
    {
        var oldPage = EnumerateContentTree()
            .FirstOrDefault(c => c.ContentType.Alias == TestSiteSeedContract.WorkflowPageAlias
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

    private void EnsureHomeAndDashboard()
    {
        var homeType = contentTypeService.Get(TestSiteSeedContract.HomePageAlias);
        if (homeType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: homePage doc type not found; skipping home/dashboard seed");
            return;
        }

        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogInformation("WORKFLOW PAGE SEEDER: Creating seeded home page");
            homePage = contentService.Create(TestSiteSeedContract.HomePageName, Constants.System.Root, TestSiteSeedContract.HomePageAlias);
        }

        SaveAndPublishIfNeeded(homePage, TestSiteSeedContract.HomePageName, null, "seeded home page");

        var dashboardType = contentTypeService.Get(TestSiteSeedContract.DashboardAlias);
        if (dashboardType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: memberDashboard doc type not found; skipping dashboard seed");
            return;
        }

#pragma warning disable CS0618
        var dashboardPage = contentService.GetPagedChildren(homePage.Id, 0, 100, out _)
#pragma warning restore CS0618
            .FirstOrDefault(c => c.ContentType.Alias == TestSiteSeedContract.DashboardAlias)
            ?? TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.DashboardAlias);

        if (dashboardPage == null)
        {
            logger.LogInformation("WORKFLOW PAGE SEEDER: Creating seeded dashboard page");
            dashboardPage = contentService.Create(TestSiteSeedContract.DashboardName, homePage.Id, TestSiteSeedContract.DashboardAlias);
        }

        SaveAndPublishIfNeeded(dashboardPage, TestSiteSeedContract.DashboardName, null, "seeded dashboard page");
    }

    private void EnsureCommunityEnquiryPage()
    {
        var contentType = contentTypeService.Get(TestSiteSeedContract.WorkflowPageAlias);
        if (contentType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: workflowPage doc type not found; skipping (run again after seeder)");
            return;
        }

        var existing = TestSiteSeedContract.FindWorkflowContent(contentService, TestSiteSeedContract.WorkflowKey);

        if (existing != null)
        {
            SaveAndPublishIfNeeded(
                existing,
                TestSiteSeedContract.WorkflowPageName,
                page => page.SetValue("workflowKey", TestSiteSeedContract.WorkflowKey),
                "seeded workflow page");
            return;
        }

        logger.LogInformation("WORKFLOW PAGE SEEDER: Creating seeded workflow page");

        var page = contentService.Create(TestSiteSeedContract.WorkflowPageName, Constants.System.Root, TestSiteSeedContract.WorkflowPageAlias);
        page.SetValue("workflowKey", TestSiteSeedContract.WorkflowKey);
        SaveAndPublishIfNeeded(page, TestSiteSeedContract.WorkflowPageName, null, "seeded workflow page");
    }

    private void EnsurePlanningWorkflowPage()
    {
        var contentType = contentTypeService.Get(TestSiteSeedContract.WorkflowPageAlias);
        if (contentType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: workflowPage doc type not found; skipping planning workflow page");
            return;
        }

        var existing = TestSiteSeedContract.FindWorkflowContent(contentService, TestSiteSeedContract.PlanningWorkflowKey);

        if (existing != null)
        {
            SaveAndPublishIfNeeded(
                existing,
                TestSiteSeedContract.PlanningWorkflowPageName,
                page => page.SetValue("workflowKey", TestSiteSeedContract.PlanningWorkflowKey),
                "seeded planning workflow page");
            return;
        }

        logger.LogInformation("WORKFLOW PAGE SEEDER: Creating seeded planning workflow page");

        var page = contentService.Create(TestSiteSeedContract.PlanningWorkflowPageName, Constants.System.Root, TestSiteSeedContract.WorkflowPageAlias);
        page.SetValue("workflowKey", TestSiteSeedContract.PlanningWorkflowKey);
        SaveAndPublishIfNeeded(page, TestSiteSeedContract.PlanningWorkflowPageName, null, "seeded planning workflow page");
    }

    private void EnsureWorkflowHubPage()
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: homePage not found; skipping workflow hub seed");
            return;
        }

        var contentType = contentTypeService.Get(TestSiteSeedContract.WorkflowHubAlias);
        if (contentType == null)
        {
            logger.LogDebug("WORKFLOW PAGE SEEDER: workflowHub doc type not found; skipping (run again after seeder)");
            return;
        }

        var existing =
#pragma warning disable CS0618
            contentService.GetPagedChildren(homePage.Id, 0, 100, out _)
#pragma warning restore CS0618
                .FirstOrDefault(c => c.ContentType.Alias == TestSiteSeedContract.WorkflowHubAlias)
            ?? TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.WorkflowHubAlias);

        if (existing != null && existing.ParentId != homePage.Id)
        {
            logger.LogInformation(
                "WORKFLOW PAGE SEEDER: Replacing workflow hub at parent {ParentId} so the seeded route stays under Home",
                existing.ParentId);
            var deleteResult = contentService.Delete(existing);
            if (!deleteResult.Success)
            {
                logger.LogWarning("WORKFLOW PAGE SEEDER: Delete failed — {Reason}", deleteResult.Result);
                return;
            }

            existing = null;
        }

        if (existing != null)
        {
            SaveAndPublishIfNeeded(existing, TestSiteSeedContract.WorkflowHubName, null, "seeded workflow hub");
            return;
        }

        logger.LogInformation("WORKFLOW PAGE SEEDER: Creating seeded workflow hub");

        var page = contentService.Create(TestSiteSeedContract.WorkflowHubName, homePage.Id, TestSiteSeedContract.WorkflowHubAlias);
        SaveAndPublishIfNeeded(page, TestSiteSeedContract.WorkflowHubName, null, "seeded workflow hub");
    }

    private IEnumerable<Umbraco.Cms.Core.Models.IContent> EnumerateContentTree()
    {
        foreach (var root in contentService.GetRootContent())
        {
            yield return root;

#pragma warning disable CS0618
            foreach (var descendant in contentService.GetPagedDescendants(root.Id, 0, 2048, out _))
#pragma warning restore CS0618
            {
                yield return descendant;
            }
        }
    }

    private void SaveAndPublishIfNeeded(
        Umbraco.Cms.Core.Models.IContent content,
        string expectedName,
        Action<Umbraco.Cms.Core.Models.IContent>? mutate,
        string label)
    {
        var changed = false;

        if (!string.Equals(content.Name, expectedName, StringComparison.Ordinal))
        {
            content.Name = expectedName;
            changed = true;
        }

        if (mutate != null)
        {
            mutate(content);
            changed = true;
        }

        var saveResult = contentService.Save(content);
        if (!saveResult.Success)
        {
            logger.LogWarning("WORKFLOW PAGE SEEDER: Save failed — {Reason}", saveResult.Result);
            return;
        }

        var publishResult = contentService.Publish(content, Array.Empty<string>());
        if (publishResult.Success)
        {
            logger.LogInformation("WORKFLOW PAGE SEEDER: {Label} published (id={Id}, changed={Changed})", label, content.Id, changed);
        }
        else
            logger.LogWarning("WORKFLOW PAGE SEEDER: Publish failed — {Reason}", publishResult.Result);
    }
}
