using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Sign-in for generated apps, run in the system authentication session rather than the app's
/// webview. The app opens <c>start</c> with an S256 challenge; after the normal OIDC sign-in,
/// <c>complete</c> hands the session over under a one-time code on the <c>callback</c> URL, which
/// only the tenant's verified app receives; the app then posts the code and its verifier to
/// <c>redeem</c> from its webview, which signs the member in there.
/// </summary>
[AllowAnonymous]
[Route(BasePath)]
public partial class PrismMobileSignInController(
    IPrismContext prismContext,
    PrismMobileSignInCodes codes,
    ILogger<PrismMobileSignInController> logger) : Controller
{
    internal const string BasePath = "umbraco/prism/mobile/auth";

    /// <summary>Marks a member session that was handed over from the system authentication session.</summary>
    internal const string AuthMethod = "system";

    [HttpGet("start")]
    public IActionResult Start(string? challenge, string? returnUrl, string? prompt)
    {
        var tenant = prismContext.CurrentTenant;
        if (tenant?.HasSignInConfigured() != true || !tenant.MobileAppLinks.HasAny)
        {
            return NotFound();
        }

        if (!IsChallenge(challenge))
        {
            return BadRequest();
        }

        var safeReturnUrl = PrismReturnUrl.Normalize(returnUrl);
        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/{BasePath}/complete?challenge={challenge}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}"
        };

        if (prompt == "create")
        {
            properties.Items["PrismPrompt"] = "create";
        }

        return Challenge(properties, PrismAuthenticationSchemes.Oidc);
    }

    [HttpGet("complete")]
    public async Task<IActionResult> Complete(string? challenge, string? returnUrl, CancellationToken cancellationToken)
    {
        var tenant = prismContext.CurrentTenant;
        if (tenant?.HasSignInConfigured() != true || !tenant.MobileAppLinks.HasAny)
        {
            return NotFound();
        }

        var member = await HttpContext.AuthenticateAsync(PrismAuthenticationSchemes.MemberCookie);
        if (!IsChallenge(challenge) || member.Ticket is not { } ticket)
        {
            return BadRequest();
        }

        var code = await codes.IssueAsync(
            new PrismMobileSignInCodes.PendingSignIn(tenant.Id, challenge, PrismReturnUrl.Normalize(returnUrl), ticket),
            cancellationToken);

        // The session now belongs to the app; nothing should stay signed in here.
        await HttpContext.SignOutAsync(PrismAuthenticationSchemes.MemberCookie);

        return Redirect($"/{BasePath}/callback?code={code}");
    }

    /// <summary>
    /// Only reached when the callback is opened outside the app, since the app receives it
    /// directly. The code is useless without the app's verifier, so this page ignores it.
    /// </summary>
    [HttpGet("callback")]
    public IActionResult Callback()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex";

        return Content(
            """
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Return to the app</title></head>
            <body><h1>Return to the app</h1><p>Sign-in finished. Go back to the app to carry on.</p></body>
            </html>
            """,
            "text/html");
    }

    [HttpPost("redeem")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Redeem([FromForm] string? code, [FromForm] string? verifier, CancellationToken cancellationToken)
    {
        var tenant = prismContext.CurrentTenant;
        if (tenant is null || string.IsNullOrEmpty(code) || !IsVerifier(verifier))
        {
            return Redirect("/");
        }

        var signIn = await codes.TakeAsync(code, cancellationToken);
        if (!IsRedeemable(signIn, tenant, verifier))
        {
            logger.LogWarning("Prism mobile sign-in: rejected a code for tenant {TenantId}", tenant.Id);
            return Redirect("/");
        }

        var identity = signIn.Ticket.Principal.Identities.First();
        identity.AddClaim(new Claim("prism_auth_method", AuthMethod));

        var properties = signIn.Ticket.Properties;
        properties.IssuedUtc = null;
        properties.ExpiresUtc = null;
        properties.RedirectUri = null;

        await HttpContext.SignInAsync(PrismAuthenticationSchemes.MemberCookie, signIn.Ticket.Principal, properties);
        PrismOidcConfiguration.NotifySignedIn(HttpContext, identity);

        return LocalRedirect(signIn.ReturnUrl);
    }

    private static bool IsChallenge([NotNullWhen(true)] string? challenge) =>
        challenge is not null && ChallengePattern().IsMatch(challenge);

    private static bool IsVerifier([NotNullWhen(true)] string? verifier) =>
        verifier is not null && VerifierPattern().IsMatch(verifier);

    /// <summary>A code this tenant issued, redeemed with the verifier for the challenge it was issued for.</summary>
    private static bool IsRedeemable([NotNullWhen(true)] PrismMobileSignInCodes.PendingSignIn? signIn, PrismTenant tenant, string verifier) =>
        signIn is not null && signIn.TenantId == tenant.Id && ChallengeMatches(signIn.Challenge, verifier);

    private static bool ChallengeMatches(string challenge, string verifier) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(challenge),
            Encoding.ASCII.GetBytes(PrismMobileSignInCodes.ChallengeFor(verifier)));

    // S256 output: 32 bytes as unpadded base64url.
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengePattern();

    // RFC 7636 code_verifier.
    [GeneratedRegex("^[A-Za-z0-9._~-]{43,128}$")]
    private static partial Regex VerifierPattern();
}
