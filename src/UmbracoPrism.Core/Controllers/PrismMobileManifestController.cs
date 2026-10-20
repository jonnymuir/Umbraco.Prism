using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// The mobile manifest a generated app reads at launch and whenever the member signs in or out:
/// the native tabs (from <see cref="IPrismMobileTabsProvider"/>) and the cookie that marks a
/// signed-in session, so the app knows when to refresh its other tabs.
/// </summary>
[AllowAnonymous]
[Route("umbraco/prism/mobile")]
public class PrismMobileManifestController(IPrismMobileTabsProvider tabsProvider) : Controller
{
    internal const int ManifestVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [HttpGet("manifest")]
    public async Task<IActionResult> Manifest(CancellationToken cancellationToken)
    {
        // /umbraco routes default to the backoffice's own scheme, so the member is read explicitly.
        var member = await HttpContext.AuthenticateAsync(PrismAuthenticationSchemes.MemberCookie);
        if (member.Succeeded)
        {
            HttpContext.User = member.Principal;
        }

        var tabs = await tabsProvider.GetTabsAsync(HttpContext, cancellationToken);
        var json = JsonSerializer.Serialize(new
        {
            version = ManifestVersion,
            tabs,
            sessionCookie = PrismAuthenticationSchemes.MemberCookie
        }, JsonOptions);

        var etag = "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16] + "\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.Vary = "Cookie";

        if (Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Content(json, "application/json");
    }
}
