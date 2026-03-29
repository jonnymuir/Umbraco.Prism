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
    /// </summary>
    public override IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect("/auth/login?returnUrl=/dashboard");

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
