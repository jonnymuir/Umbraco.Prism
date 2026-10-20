using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// The files Apple and Google read to confirm the tenant's app may receive links on its site,
/// which is what lets only that app receive the mobile sign-in callback.
/// </summary>
[AllowAnonymous]
[Route(".well-known")]
public class PrismAppLinksController(IPrismContext prismContext) : Controller
{
    [HttpGet("apple-app-site-association")]
    public IActionResult AppleAppSiteAssociation()
    {
        var appLinks = prismContext.CurrentTenant?.MobileAppLinks;
        if (appLinks?.HasIos != true)
        {
            return NotFound();
        }

        return Json(new { webcredentials = new { apps = new[] { appLinks.AppleAppId } } });
    }

    [HttpGet("assetlinks.json")]
    public IActionResult AssetLinks()
    {
        var appLinks = prismContext.CurrentTenant?.MobileAppLinks;
        if (appLinks?.HasAndroid != true)
        {
            return NotFound();
        }

        return Json(new[]
        {
            new
            {
                relation = new[] { "delegate_permission/common.handle_all_urls" },
                target = new
                {
                    @namespace = "android_app",
                    package_name = appLinks.AndroidPackageName,
                    sha256_cert_fingerprints = appLinks.AndroidCertificateFingerprints
                }
            }
        });
    }
}
