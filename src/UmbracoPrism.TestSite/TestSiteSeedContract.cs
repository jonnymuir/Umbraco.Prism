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

    public const string JugglingLicencePageName = "Apply for a juggling licence";
    public const string JugglingLicencePageUrl = "/apply-for-a-juggling-licence";
    public const string JugglingLicenceBlueprintKey = "apply-for-a-juggling-licence";

    public const string ContributionsPageName = "Submit contributions file";
    public const string ContributionsPageUrl = "/submit-contributions-file";
    public const string ContributionsBlueprintKey = "bulk-contributions";

    public const string CaseworkerQueuePageName = "Caseworker queue";
    public const string CaseworkerQueuePageUrl = "/caseworker-queue";

    public static IContent? FindContentByAlias(IContentService contentService, string alias)
        => EnumerateContentTree(contentService)
            .FirstOrDefault(content => content.ContentType.Alias == alias);

    // More than one wayfinderServicePage instance exists (apply-for-a-juggling-licence,
    // bulk-contributions, the caseworker queue) — filter by name, not just alias, so a seeder
    // checking "does my page already exist" doesn't match whichever page happens to be first in
    // the tree and wrongly skip creating another one.
    public static IContent? FindWayfinderServicePageByName(IContentService contentService, string name)
        => EnumerateContentTree(contentService)
               .FirstOrDefault(content =>
                   content.ContentType.Alias == WayfinderServicePageContentType.Alias
                   && string.Equals(content.Name, name, StringComparison.Ordinal));

    public static IPublishedContent? FindPublishedByAlias(IEnumerable<IPublishedContent> roots, string alias)
        => roots
            .SelectMany(root => root.DescendantsOrSelf())
            .FirstOrDefault(content => content.ContentType.Alias == alias);

    public static IPublishedContent? FindPublishedWayfinderServicePageByName(IEnumerable<IPublishedContent> roots, string name)
        => roots
            .SelectMany(root => root.DescendantsOrSelf())
            .FirstOrDefault(content =>
                content.ContentType.Alias == WayfinderServicePageContentType.Alias
                && string.Equals(content.Name, name, StringComparison.Ordinal));

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
