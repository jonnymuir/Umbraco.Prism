using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the Member Dashboard document type.
/// Inherits <see cref="RenderController"/> so Umbraco auto-discovers it via
/// the naming convention: controller name matches document type alias
/// "memberDashboard" → <c>MemberDashboardController</c>.
/// </summary>
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class MemberDashboardController(
    ILogger<MemberDashboardController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IPrismContext prismContext)
    : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
{
    /// <summary>
    /// Renders the member dashboard. Unauthenticated requests are redirected to
    /// the login page (the <c>[Authorize]</c> challenge handles this via the
    /// PrismMemberCookie scheme's configured <c>LoginPath</c>, but we also
    /// guard explicitly for any edge cases).
    /// Proactively warms up (and refreshes if expired) the Prism access token
    /// so the downstream API demo works immediately without a page reload.
    /// </summary>
    // 'override' matches the base RenderController.Index() signature exactly, ensuring
    // only one endpoint is registered for this route. ASP.NET Core has no
    // SynchronizationContext, so GetAwaiter().GetResult() is safe here.
    public override IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect("/auth/login?returnUrl=/dashboard");

        // Warm up the token — triggers a silent refresh if near expiry.
        // Wrapped in try/catch so infrastructure failures (vault, HTTP factory)
        // degrade gracefully: page still renders, user can navigate, and downstream
        // API calls will surface their own errors rather than crashing the dashboard.
        try
        {
            prismContext.GetAuthorizationHeaderAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token warmup failed during dashboard page load; continuing with potentially stale token");
        }

        ViewBag.DisplayName = User.FindFirst("name")?.Value
                              ?? User.FindFirst("preferred_username")?.Value
                              ?? "Member";
        ViewBag.Email = User.FindFirst("email")?.Value
                        ?? User.FindFirst("preferred_username")?.Value
                        ?? "";
        ViewBag.UserOid = User.FindFirst("oid")?.Value
                          ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                          ?? "";
        ViewBag.Tenant = prismContext.CurrentTenant;

        return CurrentTemplate(CurrentPage!);
    }
}
