using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for handling account-related actions such as login and logout.
/// </summary>
// The Entra ID auth entry/exit surface — Login/Register issue an OIDC Challenge (no session
// yet), Logout is a [ValidateAntiForgeryToken]-guarded POST. None has anything to authorize.
[AllowAnonymous]
[Route("auth")]
public class AccountController(ILogger<AccountController> logger) : Controller
{
    /// <summary>
    /// Initiates the login process.
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "/")
    {
        var safeReturnUrl = PrismReturnUrl.Normalize(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(safeReturnUrl);
        }

        var properties = new AuthenticationProperties { RedirectUri = safeReturnUrl };

        // Triggers the OIDC flow which PrismOidcConfiguration will intercept
        return Challenge(properties, "PrismEntraID");
    }

    /// <summary>
    /// Initiates the Entra ID CIAM sign-up flow.
    /// Uses the same tenant-specific OIDC configuration as login but adds
    /// the <c>prompt=create</c> parameter to trigger registration.
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet("register")]
    public IActionResult Register(string returnUrl = "/")
    {
        var safeReturnUrl = PrismReturnUrl.Normalize(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(safeReturnUrl);
        }

        var properties = new AuthenticationProperties { RedirectUri = safeReturnUrl };
        properties.Items["PrismPrompt"] = "create";

        return Challenge(properties, "PrismEntraID");
    }

    /// <summary>
    /// Logs the user out of both the local session and Entra ID.
    /// Requires POST + antiforgery token to prevent logout-CSRF (SEC-PT2-003).
    /// Front-end must submit a form with the antiforgery token rather than a link/GET.
    /// </summary>
    /// <returns></returns>
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        // TEMPORARY diagnostic: reported live on mobile — sign-out no longer bounces to system
        // Safari (a native WKWebView popup-handling fix), but the app still isn't actually
        // signed out afterward, and Entra's own host has never once appeared in client-side
        // navigation logs across several live tests. This logs server-side ground truth instead:
        // is this action even reached more than once per tap (a client-side fix that reloads a
        // popup's request could plausibly replay the exact same POST), and what does the request
        // look like when it is.
        logger.LogInformation(
            "Prism logout requested: user={User}, authenticated={IsAuthenticated}, host={Host}, userAgent={UserAgent}",
            User.Identity?.Name ?? "(none)",
            User.Identity?.IsAuthenticated == true,
            Request.Host.Value,
            Request.Headers.UserAgent.ToString());

        // A session established via BiometricController's own backchannel refresh_token grant
        // (see its own remarks on the "prism_auth_method" claim) never went through "PrismEntraID"'s
        // interactive OIDC challenge, so Entra has no browser-side SSO session in this WebView to
        // end and no id_token_hint to give its end-session endpoint — signing out of that scheme
        // anyway just shows Entra's own interactive account-chooser for nothing. Sign out of the
        // local cookie only in that case; every other session still signs out of both.
        if (User.HasClaim("prism_auth_method", "biometric"))
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                "PrismMemberCookie"
            );
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            "PrismMemberCookie",
            "PrismEntraID"
        );
    }
}
