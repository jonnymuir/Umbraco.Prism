using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for handling account-related actions such as login and logout.
/// </summary>
// The Entra ID auth entry/exit surface — Login/Register issue an OIDC Challenge (no session
// yet), Logout is a [ValidateAntiForgeryToken]-guarded POST. None has anything to authorize.
[AllowAnonymous]
[Route("auth")]
public class AccountController(ILogger<AccountController> logger, IPrismContext prismContext) : Controller
{
    /// <summary>
    /// Initiates the login process. Not found on a hostname whose tenant has no sign-in
    /// configured: there is no identity provider to send anyone to.
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet("login")]
    public async Task<IActionResult> Login(string returnUrl = "/")
    {
        if (prismContext.CurrentTenant?.HasSignInConfigured() != true)
        {
            return NotFound();
        }

        var safeReturnUrl = PrismReturnUrl.Normalize(returnUrl);

        if (await GetMemberAsync() is not null)
        {
            return LocalRedirect(safeReturnUrl);
        }

        var properties = new AuthenticationProperties { RedirectUri = safeReturnUrl };

        // Triggers the OIDC flow which PrismOidcConfiguration will intercept
        return Challenge(properties, PrismAuthenticationSchemes.Oidc);
    }

    /// <summary>
    /// Initiates the Entra ID CIAM sign-up flow.
    /// Uses the same tenant-specific OIDC configuration as login but adds
    /// the <c>prompt=create</c> parameter to trigger registration. Not found where login is.
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet("register")]
    public async Task<IActionResult> Register(string returnUrl = "/")
    {
        if (prismContext.CurrentTenant?.HasSignInConfigured() != true)
        {
            return NotFound();
        }

        var safeReturnUrl = PrismReturnUrl.Normalize(returnUrl);

        if (await GetMemberAsync() is not null)
        {
            return LocalRedirect(safeReturnUrl);
        }

        var properties = new AuthenticationProperties { RedirectUri = safeReturnUrl };
        properties.Items["PrismPrompt"] = "create";

        return Challenge(properties, PrismAuthenticationSchemes.Oidc);
    }

    /// <summary>
    /// Logs the user out of both the local session and Entra ID.
    /// Requires POST + antiforgery token to prevent logout-CSRF (SEC-PT2-003).
    /// Front-end must submit a form with the antiforgery token rather than a link/GET.
    /// </summary>
    /// <returns></returns>
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var member = await GetMemberAsync();

        // TEMPORARY diagnostic: reported live on mobile — sign-out no longer bounces to system
        // Safari (a native WKWebView popup-handling fix), but the app still isn't actually
        // signed out afterward, and Entra's own host has never once appeared in client-side
        // navigation logs across several live tests. This logs server-side ground truth instead:
        // is this action even reached more than once per tap (a client-side fix that reloads a
        // popup's request could plausibly replay the exact same POST), and what does the request
        // look like when it is.
        logger.LogInformation(
            "Prism logout requested: user={User}, authenticated={IsAuthenticated}, host={Host}, userAgent={UserAgent}",
            member?.Identity?.Name ?? "(none)",
            member is not null,
            Request.Host.Value,
            Request.Headers.UserAgent.ToString());

        // A session established via BiometricController's own backchannel refresh_token grant
        // (see its own remarks on the "prism_auth_method" claim) never went through "PrismEntraID"'s
        // interactive OIDC challenge, so Entra has no browser-side SSO session in this WebView to
        // end and no id_token_hint to give its end-session endpoint — signing out of that scheme
        // anyway just shows Entra's own interactive account-chooser for nothing. Sign out of the
        // local cookie only in that case, and wherever this hostname has no sign-in configured
        // (no provider session to end); every other session still signs out of both.
        if (member?.HasClaim("prism_auth_method", "biometric") == true ||
            prismContext.CurrentTenant?.HasSignInConfigured() != true)
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                PrismAuthenticationSchemes.MemberCookie
            );
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            PrismAuthenticationSchemes.MemberCookie,
            PrismAuthenticationSchemes.Oidc
        );
    }

    // The Prism member session, asked of Prism's own cookie rather than User: User comes from
    // the application's default scheme, and a host session is not a Prism sign-in.
    private async Task<System.Security.Claims.ClaimsPrincipal?> GetMemberAsync()
    {
        var result = await HttpContext.AuthenticateAsync(PrismAuthenticationSchemes.MemberCookie);
        return result.Succeeded ? result.Principal : null;
    }
}
