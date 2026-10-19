using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.TestSite.Services;

/// <summary>
/// The native app's tabs are the same Settings → <c>mobileNavLinks</c> items editors already
/// maintain for the web tab bar. Items that open in a new tab are links out of the app, not tabs.
/// </summary>
public class TestSiteMobileTabsProvider(
    IPublishedContentQuery publishedContentQuery,
    IUmbracoContextFactory umbracoContextFactory,
    IDocumentUrlService documentUrlService,
    IPrismPageAccessResolver pageAccessResolver,
    IPrismContext prismContext) : IPrismMobileTabsProvider
{
    public Task<IReadOnlyList<PrismMobileTab>> GetTabsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        // Media URLs need an UmbracoContext, which a plain API route does not have ambiently.
        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();

        var settings = TestSiteSeedContract.FindPublishedByAlias(
            publishedContentQuery.ContentAtRoot(), TestSiteSeedContract.SettingsAlias);

        var items = MobileNavItems.Available(
            settings?.Value<BlockListModel>("mobileNavLinks"),
            documentUrlService,
            pageAccessResolver,
            prismContext.CurrentTenant,
            context.User.Identity?.IsAuthenticated == true);

        IReadOnlyList<PrismMobileTab> tabs = items
            .Where(item => !item.OpenInNewTab)
            .Select(item => new PrismMobileTab(item.Label, item.Href, item.IconUrl))
            .ToList();

        return Task.FromResult(tabs);
    }
}
