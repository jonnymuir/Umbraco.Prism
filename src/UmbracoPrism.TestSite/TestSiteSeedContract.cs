using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace UmbracoPrism.TestSite;

public static class TestSiteSeedContract
{
    public const string HomePageAlias = "homePage";
    public const string HomePageName = "Home";
    public const string HomePageUrl = "/";

    public const string DashboardAlias = "memberDashboard";
    public const string DashboardName = "Dashboard";
    public const string DashboardUrl = "/dashboard";

    public const string SettingsAlias = "settings";
    public const string SettingsName = "Settings";

    public const string WorkflowPageAlias = "workflowPage";
    public const string WorkflowPageName = "Get in Touch";
    public const string WorkflowPageUrl = "/get-in-touch";
    public const string WorkflowKey = "community-enquiry";

    public const string PlanningWorkflowPageName = "Apply for Planning Permission";
    public const string PlanningWorkflowPageUrl = "/apply-for-planning-permission";
    public const string PlanningWorkflowKey = "planning";

    public const string PaymentDemoPageName = "Payment Demo";
    public const string PaymentDemoPageUrl = "/payment-demo";
    public const string PaymentDemoWorkflowKey = "payment-demo";

    public const string InformationRequestPageName = "Request Information";
    public const string InformationRequestPageUrl = "/request-information";
    public const string InformationRequestWorkflowKey = "information-request";

    public const string MoneyModellerPageName = "Money Modeller";

    public const string MoneyModellerWorkflowKey = "money-modeller";

    public const string WorkflowHubAlias = "workflowHub";
    public const string WorkflowHubName = "My Workflows";
    public const string WorkflowHubUrl = "/my-workflows";

    public static IContent? FindContentByAlias(IContentService contentService, string alias)
        => EnumerateContentTree(contentService)
            .FirstOrDefault(content => content.ContentType.Alias == alias);

    public static IContent? FindWorkflowContent(IContentService contentService, string workflowKey)
        => EnumerateContentTree(contentService)
               .FirstOrDefault(content =>
                   content.ContentType.Alias == WorkflowPageAlias
                   && string.Equals(content.GetValue<string>("workflowKey"), workflowKey, StringComparison.OrdinalIgnoreCase));

    public static IPublishedContent? FindPublishedByAlias(IEnumerable<IPublishedContent> roots, string alias)
        => roots
            .SelectMany(root => root.DescendantsOrSelf())
            .FirstOrDefault(content => content.ContentType.Alias == alias);

    public static IPublishedContent? FindPublishedWorkflowPage(IEnumerable<IPublishedContent> roots, string workflowKey)
        => roots
               .SelectMany(root => root.DescendantsOrSelf())
               .FirstOrDefault(content =>
                    content.ContentType.Alias == WorkflowPageAlias
                    && string.Equals(content.Value<string>("workflowKey"), workflowKey, StringComparison.OrdinalIgnoreCase));

    public static string ResolveUrl(IPublishedContent? content, string fallback)
        => ResolveUrl(content?.Url(), fallback);

    public static string ResolveUrl(string? resolvedUrl, string fallback)
    {
        var normalizedFallback = NormalizeUrl(fallback);

        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            return normalizedFallback;
        }

        var normalizedResolvedUrl = NormalizeUrl(resolvedUrl);

        // During cold-start publishing/routing convergence Umbraco can briefly surface "/"
        // for seeded child pages before their final route is available. Keep the expected
        // fallback route instead of collapsing navigation back to Home.
        if (normalizedResolvedUrl == "/" && normalizedFallback != "/")
        {
            return normalizedFallback;
        }

        return normalizedResolvedUrl;
    }

    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url == "/")
        {
            return "/";
        }

        return url.TrimEnd('/');
    }

    private static IEnumerable<IContent> EnumerateContentTree(IContentService contentService)
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
}
