using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Serves the home page's editor-picked hero/card image CSS variable overrides as their own
/// stylesheet (SEC-PT2-004 CSP follow-up), the same rationale as
/// UmbracoPrism.Core.Controllers.PrismBrandingAssetsController: this content is genuinely
/// per-content-item dynamic (heroImage/cardImage are Umbraco media pickers on the homePage
/// content type), so — unlike the branding/mobile-shell assets which are per-tenant but
/// otherwise static — it can't just move to a plain static file. Serving it as its own resource,
/// rather than splicing it inline into a &lt;style&gt; tag on every render of homePage.cshtml,
/// needs no CSP unsafe-inline/nonce exception.
/// </summary>
[AllowAnonymous]
[Route("css/home-imagery.css")]
public class HomeImageryCssController(
    IPublishedContentQuery publishedContentQuery,
    IUmbracoContextFactory umbracoContextFactory) : Controller
{
    [HttpGet]
    public ContentResult Get()
    {
        // Media URL resolution (unlike content URL resolution) needs an UmbracoContext that
        // isn't already ambiently available on a plain custom API route the way it is on a
        // real Umbraco-routed content request — EnsureUmbracoContext() is the standard pattern
        // for exactly this (reuses one if it already exists, otherwise creates a scoped one).
        using var contextReference = umbracoContextFactory.EnsureUmbracoContext();

        var roots = publishedContentQuery.ContentAtRoot().ToList();
        var home = TestSiteSeedContract.FindPublishedByAlias(roots, TestSiteSeedContract.HomePageAlias);
        var heroImageUrl = home?.Value<IPublishedContent>("heroImage")?.Url();
        var cardImageUrl = home?.Value<IPublishedContent>("cardImage")?.Url();

        var css = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(heroImageUrl) || !string.IsNullOrWhiteSpace(cardImageUrl))
        {
            css.Append(":root{");
            if (!string.IsNullOrWhiteSpace(heroImageUrl))
            {
                css.Append($"--prism-hero-image:url('{EscapeCssUrl(heroImageUrl)}');");
            }
            if (!string.IsNullOrWhiteSpace(cardImageUrl))
            {
                css.Append($"--prism-card-image:url('{EscapeCssUrl(cardImageUrl)}');");
            }
            css.Append('}');
        }

        return Content(css.ToString(), "text/css");
    }

    // Umbraco generates this URL internally for a picked media item — not free-text editor
    // input the way tenant branding overrides are — but escaped defensively regardless, the
    // same way any value spliced into a CSS string literal should be: a literal apostrophe or
    // backslash in a filename must never be able to break out of the surrounding url('...').
    private static string EscapeCssUrl(string url) =>
        url.Replace("\\", "\\\\").Replace("'", "\\'");
}
