using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Services;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// A visitor who was browsing TestSite's public service request demo anonymously and has just
/// signed in gets their in-progress instance(s) re-keyed onto their new authenticated identity,
/// so what would otherwise become an orphaned, soon-to-expire anonymous session survives as a
/// resumable one under "My Service Requests" instead. Registered by TestSite only — a host
/// using Prism's auth without this demo gets no handler at all, which
/// <see cref="PrismOidcConfiguration"/>'s <c>IPrismPostSignInHandler</c> loop already treats as
/// a normal no-op.
/// </summary>
public sealed class PublicServiceRequestPostSignInHandler(
    UmbracoProcessManagerEngine engine,
    PublicVisitorIdentityResolver identityResolver,
    IPrismUserContext userContext,
    ILogger<PublicServiceRequestPostSignInHandler> logger) : IPrismPostSignInHandler
{
    /// <summary>
    /// Reads <paramref name="newIdentity"/> directly rather than <c>httpContext.User</c> — the
    /// sign-in cookie written moments ago only takes effect on the *next* request, so the
    /// incoming request's principal is still whatever it was before this callback ran
    /// (anonymous, or a different signed-in user), not the one just authenticated.
    /// </summary>
    public void OnSignedIn(HttpContext httpContext, ClaimsIdentity newIdentity)
    {
        var anonymousUserId = identityResolver.PeekAnonymousVisitorId();
        if (anonymousUserId is null)
        {
            return;
        }

        var newUserId = newIdentity.FindFirst("preferred_username")?.Value
            ?? newIdentity.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(newUserId))
        {
            return;
        }

        var tenantId = userContext.CurrentTenant?.Hostname ?? "default";

        var claimed = engine.ClaimInstances(tenantId, anonymousUserId, newUserId);
        if (claimed.Count > 0)
        {
            identityResolver.ClearAnonymousVisitorCookie();
            logger.LogInformation(
                "Claimed {Count} anonymous public service request instance(s) for tenant {TenantId} onto the newly signed-in user.",
                claimed.Count, tenantId);
        }
    }
}
