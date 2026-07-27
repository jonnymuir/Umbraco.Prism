using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Umbraco route-hijacking controller for the Member Dashboard document type.
/// Inherits <see cref="RenderController"/> so Umbraco auto-discovers it via
/// the naming convention: controller name matches document type alias
/// "memberDashboard" → <c>MemberDashboardController</c>.
/// </summary>
public class MemberDashboardController(
    ILogger<MemberDashboardController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IPrismContext prismContext,
    IConfiguration configuration)
    : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
{
    /// <summary>
    /// Renders the member dashboard. Unauthenticated requests are redirected to
    /// the login page (the <c>[Authorize]</c> challenge handles this via the
    /// PrismMemberCookie scheme's configured <c>LoginPath</c>, but we also
    /// guard explicitly for any edge cases).
    /// The dashboard no longer performs token warmup during the initial page
    /// render because auth-cookie renewal on first navigation can trigger a
    /// self-redirect loop before the page settles. Downstream API actions
    /// still refresh on demand when the user invokes them.
    /// </summary>
    // 'override' matches the base RenderController.Index() signature exactly, ensuring
    // only one endpoint is registered for this route. ASP.NET Core has no
    // SynchronizationContext, so GetAwaiter().GetResult() is safe here.
    public override IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect(BuildLoginRedirectUrl());

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

        // Derive the MockBusinessApp service-desk admin URL from the configured public base URL.
        // In Codespaces this resolves to the forwarded port URL; locally it is https://localhost:7245.
        var businessAppApiBase = configuration["PrismBusinessApp:ApiBaseUrl"]?.TrimEnd('/');
        ViewBag.ServiceDeskUrl = string.IsNullOrWhiteSpace(businessAppApiBase)
            ? null
            : $"{businessAppApiBase}/admin/service-desk";
        ViewBag.ServiceBlueprintEditorUrl = string.IsNullOrWhiteSpace(businessAppApiBase)
            ? null
            : $"{businessAppApiBase}/service-blueprint-editor";

        // Render the authored dashboard view directly. On the first authenticated
        // navigation after /signin-oidc, CurrentTemplate(CurrentPage!) can settle
        // into a self-redirect loop on /dashboard under the local Aspire stack.
        return View("~/Views/memberDashboard.cshtml", CurrentPage!);
    }

    private string BuildLoginRedirectUrl()
    {
        var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
        return $"/auth/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
