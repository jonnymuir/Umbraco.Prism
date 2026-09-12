using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Serves tenant-specific branding as a normal, externally-referenced CSS resource rather than
/// splicing it inline into every HTML response (SEC-PT2-004 CSP follow-up). CSP's inline-content
/// restrictions (nonces/hashes/<c>unsafe-inline</c>) only ever apply to literal inline
/// <c>&lt;style&gt;</c>/<c>&lt;script&gt;</c> bodies — an externally-referenced resource is
/// governed purely by origin allowlisting (<c>style-src 'self'</c>), regardless of how dynamic
/// its content is. Hosts reference this with a plain
/// <c>&lt;link rel="stylesheet" href="/umbraco/prism/branding.css"&gt;</c> in their layout — the
/// same pattern already used for Prism's other front-end assets (e.g. <c>prism-mobile-nav.js</c>),
/// not something Prism injects automatically.
/// </summary>
[AllowAnonymous]
[Route("umbraco/prism")]
public class PrismBrandingAssetsController(IPrismContext prismContext) : Controller
{
    /// <summary>
    /// Returns the current tenant's branding override CSS. Mobile overrides are layered on top
    /// of the desktop ones when the request is detected as a Prism mobile request (same
    /// detection <see cref="PrismMobileRequestDetection"/> already uses elsewhere) — matching
    /// the precedence the old inline injection used. Always returns a valid CSS document
    /// (empty when the tenant has no overrides configured), so hosts can reference this
    /// unconditionally without checking whether any tenant actually has branding configured.
    /// </summary>
    [HttpGet("branding.css")]
    public ContentResult BrandingCss()
    {
        var tenant = prismContext.CurrentTenant;
        var includeMobileOverrides = PrismMobileRequestDetection.IsPrismMobileRequest(HttpContext);

        var css = PrismBrandingCssBuilder.BuildCssOverrides(
            tenant?.BrandingOverrides,
            includeMobileOverrides ? tenant?.MobileBrandingOverrides : null,
            tenant?.BrandingCssDeclarations,
            includeMobileOverrides ? tenant?.MobileBrandingCssDeclarations : null);

        return Content(css, "text/css");
    }
}
