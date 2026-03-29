using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for handling account-related actions such as login and logout.
/// </summary>
[Route("auth")]
public class AccountController : Controller
{
    /// <summary>
    /// Initiates the login process.
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "/")
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl);
        }

        var properties = new AuthenticationProperties { RedirectUri = returnUrl };

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
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl);
        }

        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        properties.Items["PrismPrompt"] = "create";

        return Challenge(properties, "PrismEntraID");
    }

    /// <summary>
    /// Logs the user out of both the local session and Entra ID.
    /// </summary>
    /// <returns></returns>
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        // Sign out of the local cookie AND the Entra ID session
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            "PrismMemberCookie",
            "PrismEntraID"
        );
    }
}