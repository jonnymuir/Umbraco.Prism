using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Authenticated member dashboard. Shows member claims, account overview,
/// and biometric authentication status.
/// </summary>
[Route("dashboard")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class MemberDashboardController(IPrismContext prismContext) : Controller
{
    /// <summary>
    /// Renders the member dashboard with claims and biometric status.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        var displayName = User.FindFirst("name")?.Value
                          ?? User.FindFirst("preferred_username")?.Value
                          ?? "Member";
        var email = User.FindFirst("email")?.Value
                    ?? User.FindFirst("preferred_username")?.Value
                    ?? "";
        var oid = User.FindFirst("oid")?.Value
                  ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                  ?? "";

        ViewBag.DisplayName = displayName;
        ViewBag.Email = email;
        ViewBag.UserOid = oid;
        ViewBag.Tenant = prismContext.CurrentTenant;

        return View();
    }
}
