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

    public const string TouchpointPageAlias = "touchpointPage";
    public const string TouchpointPageName = "Get in Touch";
    public const string ServiceRequestPageUrl = "/get-in-touch";
    public const string BlueprintKey = "community-enquiry";

    public const string PlanningTouchpointPageName = "Apply for Planning Permission";
    public const string PlanningTouchpointPageUrl = "/apply-for-planning-permission";
    public const string PlanningBlueprintKey = "planning";

    public const string PaymentDemoPageName = "Payment Demo";
    public const string PaymentDemoPageUrl = "/payment-demo";
    public const string PaymentDemoBlueprintKey = "payment-demo";

    public const string InformationRequestPageName = "Request Information";
    public const string InformationRequestPageUrl = "/request-information";
    public const string InformationRequestBlueprintKey = "information-request";

    public const string MoneyModellerPageName = "Money Modeller";

    public const string MoneyModellerBlueprintKey = "money-modeller";

    public const string ServiceRequestHubAlias = "serviceRequestHub";
    public const string ServiceRequestHubName = "My Workflows";
    public const string ServiceRequestHubUrl = "/my-workflows";

    public const string CmsServiceRequestPageAlias = "cmsServiceRequestPage";
    public const string JugglingLicencePageName = "Apply for a juggling licence";
    public const string JugglingLicencePageUrl = "/apply-for-a-juggling-licence";
    public const string JugglingLicenceBlueprintKey = "apply-for-a-juggling-licence";

    // Originally built live via MCP (see tests/demo/licence-transfer-demo.spec.ts) — that
    // recording is the definition's real origin story and the reason it exists, but the result
    // is good enough to also be C# seeded as a permanent "here's one we made earlier" reference,
    // the same way JugglingLicenceBlueprintKey is (see LicenceTransferCmsServiceBlueprintSeeder). The
    // page name/URL/nav label below match exactly what the recording itself creates live, so a
    // fresh Aspire boot and the recorded video agree on every visible detail.
    public const string JugglingLicenceTransferBlueprintKey = "transfer-a-juggling-licence";
    public const string LicenceTransferPageName = "Transfer your existing juggling licence";
    public const string LicenceTransferPageUrl = "/transfer-your-existing-juggling-licence";
    public const string LicenceTransferNavLabel = "Transfer your licence";

    public static IContent? FindContentByAlias(IContentService contentService, string alias)
        => EnumerateContentTree(contentService)
            .FirstOrDefault(content => content.ContentType.Alias == alias);

    public static IContent? FindServiceRequestContent(IContentService contentService, string blueprintKey)
        => EnumerateContentTree(contentService)
               .FirstOrDefault(content =>
                   content.ContentType.Alias == TouchpointPageAlias
                   && string.Equals(content.GetValue<string>("blueprintKey"), blueprintKey, StringComparison.OrdinalIgnoreCase));

    // cmsServiceRequestPage is a distinct doc type from touchpointPage above (Prism's newer,
    // Umbraco-only-hosted CMS Workflow, not the older business-workflow demos) — and more than
    // one cmsServiceRequestPage instance can now exist (apply-for-a-juggling-licence,
    // transfer-a-juggling-licence), so a seeder checking "does my page already exist" must
    // filter by blueprintKey, not just by alias (which would match whichever page happens to be
    // first in the tree and wrongly skip creating the other one).
    public static IContent? FindCmsServiceRequestPageByKey(IContentService contentService, string blueprintKey)
        => EnumerateContentTree(contentService)
               .FirstOrDefault(content =>
                   content.ContentType.Alias == CmsServiceRequestPageAlias
                   && string.Equals(content.GetValue<string>("blueprintKey"), blueprintKey, StringComparison.OrdinalIgnoreCase));

    public static IPublishedContent? FindPublishedByAlias(IEnumerable<IPublishedContent> roots, string alias)
        => roots
            .SelectMany(root => root.DescendantsOrSelf())
            .FirstOrDefault(content => content.ContentType.Alias == alias);

    public static IPublishedContent? FindPublishedTouchpointPage(IEnumerable<IPublishedContent> roots, string blueprintKey)
        => roots
               .SelectMany(root => root.DescendantsOrSelf())
               .FirstOrDefault(content =>
                    content.ContentType.Alias == TouchpointPageAlias
                    && string.Equals(content.Value<string>("blueprintKey"), blueprintKey, StringComparison.OrdinalIgnoreCase));

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
