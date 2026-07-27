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
public class StagePageSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IWebHostEnvironment env,
    IRuntimeState runtimeState,
    ILogger<StagePageSeeder> logger)
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
            EnsurePlanningStagePage();
            EnsurePaymentDemoPage();
            EnsureInformationRequestPage();
            EnsureMoneyModellerPage();
            EnsureServiceRequestHubPage();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "STAGE PAGE SEEDER: Unexpected error; skipping");
        }

        return Task.CompletedTask;
    }

    private void CleanupOldRetirementQuotePage()
    {
        var oldPage = EnumerateContentTree()
            .FirstOrDefault(c => c.ContentType.Alias == TestSiteSeedContract.StagePageAlias
                              && (string.Equals(c.Name, "Retirement Quote", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(c.GetValue<string>("blueprintKey"), "retirement-quote", StringComparison.OrdinalIgnoreCase)));

        if (oldPage != null)
        {
            logger.LogInformation("STAGE PAGE SEEDER: Deleting old 'Retirement Quote' node (id={Id})", oldPage.Id);
            var deleteResult = contentService.Delete(oldPage);
            if (deleteResult.Success)
                logger.LogInformation("STAGE PAGE SEEDER: Old demo node deleted successfully");
            else
                logger.LogWarning("STAGE PAGE SEEDER: Delete failed — {Reason}", deleteResult.Result);
        }
    }

    private void EnsureHomeAndDashboard()
    {
        var homeType = contentTypeService.Get(TestSiteSeedContract.HomePageAlias);
        if (homeType == null)
        {
            logger.LogDebug("STAGE PAGE SEEDER: homePage doc type not found; skipping home/dashboard seed");
            return;
        }

        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogInformation("STAGE PAGE SEEDER: Creating seeded home page");
            homePage = contentService.Create(TestSiteSeedContract.HomePageName, Constants.System.Root, TestSiteSeedContract.HomePageAlias);
        }

        SaveAndPublishIfNeeded(homePage, TestSiteSeedContract.HomePageName, null, "seeded home page");

        var dashboardType = contentTypeService.Get(TestSiteSeedContract.DashboardAlias);
        if (dashboardType == null)
        {
            logger.LogDebug("STAGE PAGE SEEDER: memberDashboard doc type not found; skipping dashboard seed");
            return;
        }

#pragma warning disable CS0618
        var dashboardPage = contentService.GetPagedChildren(homePage.Id, 0, 100, out _)
#pragma warning restore CS0618
            .FirstOrDefault(c => c.ContentType.Alias == TestSiteSeedContract.DashboardAlias)
            ?? TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.DashboardAlias);

        if (dashboardPage == null)
        {
            logger.LogInformation("STAGE PAGE SEEDER: Creating seeded dashboard page");
            dashboardPage = contentService.Create(TestSiteSeedContract.DashboardName, homePage.Id, TestSiteSeedContract.DashboardAlias);
        }

        SaveAndPublishIfNeeded(dashboardPage, TestSiteSeedContract.DashboardName, null, "seeded dashboard page");
    }

    private void EnsureCommunityEnquiryPage()
    {
        EnsureStagePageUnderHome(
            TestSiteSeedContract.BlueprintKey,
            TestSiteSeedContract.StagePageName,
            "seeded workflow page");
    }

    private void EnsurePlanningStagePage()
    {
        EnsureStagePageUnderHome(
            TestSiteSeedContract.PlanningBlueprintKey,
            TestSiteSeedContract.PlanningStagePageName,
            "seeded planning workflow page");
    }

    private void EnsurePaymentDemoPage()
    {
        EnsureStagePageUnderHome(
            TestSiteSeedContract.PaymentDemoBlueprintKey,
            TestSiteSeedContract.PaymentDemoPageName,
            "seeded payment demo page");
    }

    private void EnsureInformationRequestPage()
    {
        EnsureStagePageUnderHome(
            TestSiteSeedContract.InformationRequestBlueprintKey,
            TestSiteSeedContract.InformationRequestPageName,
            "seeded information request page");
    }

    private void EnsureMoneyModellerPage()
    {
        EnsureStagePageUnderHome(
            TestSiteSeedContract.MoneyModellerBlueprintKey,
            TestSiteSeedContract.MoneyModellerPageName,
            "seeded money modeller page");
    }

    private void EnsureStagePageUnderHome(string blueprintKey, string pageName, string label)
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("STAGE PAGE SEEDER: homePage not found; skipping {Label}", label);
            return;
        }

        var contentType = contentTypeService.Get(TestSiteSeedContract.StagePageAlias);
        if (contentType == null)
        {
            logger.LogDebug("STAGE PAGE SEEDER: stagePage doc type not found; skipping {Label}", label);
            return;
        }

        var existing = EnumerateContentTree()
            .FirstOrDefault(content =>
                content.ContentType.Alias == TestSiteSeedContract.StagePageAlias
                && string.Equals(content.GetValue<string>("blueprintKey"), blueprintKey, StringComparison.OrdinalIgnoreCase));

        if (existing != null && existing.ParentId != homePage.Id)
        {
            logger.LogInformation(
                "STAGE PAGE SEEDER: Replacing workflow page {BlueprintKey} at parent {ParentId} so the seeded route stays under Home",
                blueprintKey,
                existing.ParentId);

            var deleteResult = contentService.Delete(existing);
            if (!deleteResult.Success)
            {
                logger.LogWarning("STAGE PAGE SEEDER: Delete failed — {Reason}", deleteResult.Result);
                return;
            }

            existing = null;
        }

        if (existing != null)
        {
            SaveAndPublishIfNeeded(
                existing,
                pageName,
                page => page.SetValue("blueprintKey", blueprintKey),
                label);
            return;
        }

        logger.LogInformation("STAGE PAGE SEEDER: Creating {Label}", label);

        var page = contentService.Create(pageName, homePage.Id, TestSiteSeedContract.StagePageAlias);
        page.SetValue("blueprintKey", blueprintKey);
        SaveAndPublishIfNeeded(page, pageName, null, label);
    }

    private void EnsureServiceRequestHubPage()
    {
        var homePage = TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.HomePageAlias);
        if (homePage == null)
        {
            logger.LogDebug("STAGE PAGE SEEDER: homePage not found; skipping workflow hub seed");
            return;
        }

        var contentType = contentTypeService.Get(TestSiteSeedContract.ServiceRequestHubAlias);
        if (contentType == null)
        {
            logger.LogDebug("STAGE PAGE SEEDER: serviceRequestHub doc type not found; skipping (run again after seeder)");
            return;
        }

        var existing =
#pragma warning disable CS0618
            contentService.GetPagedChildren(homePage.Id, 0, 100, out _)
#pragma warning restore CS0618
                .FirstOrDefault(c => c.ContentType.Alias == TestSiteSeedContract.ServiceRequestHubAlias)
            ?? TestSiteSeedContract.FindContentByAlias(contentService, TestSiteSeedContract.ServiceRequestHubAlias);

        if (existing != null && existing.ParentId != homePage.Id)
        {
            logger.LogInformation(
                "STAGE PAGE SEEDER: Replacing workflow hub at parent {ParentId} so the seeded route stays under Home",
                existing.ParentId);
            var deleteResult = contentService.Delete(existing);
            if (!deleteResult.Success)
            {
                logger.LogWarning("STAGE PAGE SEEDER: Delete failed — {Reason}", deleteResult.Result);
                return;
            }

            existing = null;
        }

        if (existing != null)
        {
            SaveAndPublishIfNeeded(existing, TestSiteSeedContract.ServiceRequestHubName, null, "seeded workflow hub");
            return;
        }

        logger.LogInformation("STAGE PAGE SEEDER: Creating seeded workflow hub");

        var page = contentService.Create(TestSiteSeedContract.ServiceRequestHubName, homePage.Id, TestSiteSeedContract.ServiceRequestHubAlias);
        SaveAndPublishIfNeeded(page, TestSiteSeedContract.ServiceRequestHubName, null, "seeded workflow hub");
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
            logger.LogWarning("STAGE PAGE SEEDER: Save failed — {Reason}", saveResult.Result);
            return;
        }

        var publishResult = contentService.Publish(content, Array.Empty<string>());
        if (publishResult.Success)
        {
            logger.LogInformation("STAGE PAGE SEEDER: {Label} published (id={Id}, changed={Changed})", label, content.Id, changed);
        }
        else
            logger.LogWarning("STAGE PAGE SEEDER: Publish failed — {Reason}", publishResult.Result);
    }
}
