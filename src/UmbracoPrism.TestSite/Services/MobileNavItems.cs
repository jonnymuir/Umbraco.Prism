using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.TestSite.Services;

public sealed record MobileNavItem(string Label, string Href, string? IconUrl, bool OpenInNewTab);

/// <summary>
/// The Settings node's <c>mobileNavLinks</c> items a visitor can actually use, shared by the web
/// tab bar (<c>_MobileShellNav</c>) and the native app's tabs (<see cref="TestSiteMobileTabsProvider"/>).
/// A page the visitor's tenant marks Unavailable is left out entirely, so its existence is never
/// sent to the client.
/// </summary>
public static class MobileNavItems
{
    public static IReadOnlyList<MobileNavItem> Available(
        IEnumerable<BlockListItem>? blocks,
        IDocumentUrlService documentUrlService,
        IPrismPageAccessResolver pageAccessResolver,
        PrismTenant? tenant,
        bool isAuthenticated)
    {
        if (blocks is null)
        {
            return [];
        }

        return blocks
            .Where(block => IsAvailable(block.Content.Value<string>("navUrl"), documentUrlService, pageAccessResolver, tenant, isAuthenticated))
            .Select(block => new MobileNavItem(
                block.Content.Value<string>("navLabel") ?? string.Empty,
                block.Content.Value<string>("navUrl") ?? "/",
                block.Content.Value<IPublishedContent?>("navIcon")?.Url(),
                block.Content.Value<bool>("openInNewTab")))
            .ToList();
    }

    private static bool IsAvailable(
        string? url,
        IDocumentUrlService documentUrlService,
        IPrismPageAccessResolver pageAccessResolver,
        PrismTenant? tenant,
        bool isAuthenticated)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var contentKey = documentUrlService.GetDocumentKeyByRoute(url, culture: null, documentStartNodeId: null, isDraft: false);
        return contentKey is null
            || pageAccessResolver.Resolve(contentKey.Value, tenant, isAuthenticated) != PrismPageAccessResult.Unavailable;
    }
}
