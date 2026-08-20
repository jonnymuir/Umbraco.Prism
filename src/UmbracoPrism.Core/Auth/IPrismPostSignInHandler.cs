using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Extension point invoked right after a Prism Member signs in, so an optional feature can
/// react to "this identity just authenticated" without <see cref="PrismOidcConfiguration"/>
/// needing a hard reference to that feature's own package. Zero registered handlers is the
/// normal case for a host that hasn't added the feature — behaves identically to no hook
/// existing at all.
/// </summary>
/// <remarks>
/// Introduced for a service-design host's own anonymous-instance reattachment — that logic lives
/// in whatever consumes Wayfinder.Umbraco (e.g. UmbracoPrism.TestSite), never in Core itself,
/// which carries no Wayfinder dependency at all.
/// </remarks>
public interface IPrismPostSignInHandler
{
    /// <summary>
    /// Called synchronously as part of completing sign-in, with the newly authenticated
    /// identity. <paramref name="httpContext"/>'s own <c>User</c> is still whatever it was
    /// before this request's sign-in cookie takes effect (next request) — handlers needing
    /// "who just signed in" must read <paramref name="newIdentity"/>, not <c>httpContext.User</c>.
    /// </summary>
    void OnSignedIn(HttpContext httpContext, ClaimsIdentity newIdentity);
}
