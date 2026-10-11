using System.Net.Http.Headers;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Releases the signed-in member's own access token for a call to a business app, and refuses
/// whenever doing so would be unsafe. This is the one place a support-system client gets a
/// credential, so the rules live here and not in each client.
/// <list type="bullet">
/// <item>The call must be made inside the member's own request: the token is read from their
/// session cookie through <see cref="IPrismContext"/>, which first checks it belongs to the
/// resolved tenant and refreshes it if it has expired. A caller with no session gets an exception,
/// never an anonymous call.</item>
/// <item>The destination must be HTTPS. A bearer token sent over plain HTTP is exposed to anyone on
/// the path. The one exception is a business app on this same host, reached on a loopback address,
/// and only when the deployment opts in (<c>PrismBusinessApp:AllowLoopbackHttp</c>): the token then
/// never leaves the machine.</item>
/// </list>
/// The token is returned for the outgoing request only. Callers must not put it in an invocation
/// envelope, a field value or a log line.
/// </summary>
public sealed class MemberBearerProvider(IHttpContextAccessor httpContextAccessor, bool allowLoopbackHttp = false)
{
    public async Task<AuthenticationHeaderValue> GetForAsync(HttpClient client)
    {
        if (!IsSafeDestination(client.BaseAddress))
        {
            throw new InvalidOperationException(
                "Refusing to send the member's token: the business app address is not an HTTPS URL"
                + " (or, where explicitly allowed, a loopback address).");
        }

        var prismContext = httpContextAccessor.HttpContext?.RequestServices.GetService<IPrismContext>()
            ?? throw new InvalidOperationException(
                "No signed-in request to act for: this call can only run inside the member's own request.");

        return await prismContext.GetAuthorizationHeaderAsync()
            ?? throw new InvalidOperationException(
                $"No bearer token could be released for the signed-in member ({prismContext.LastAuthorizationFailureReason ?? "unknown"}).");
    }

    private bool IsSafeDestination(Uri? address) => address switch
    {
        { Scheme: "https" } => true,
        { Scheme: "http" } => allowLoopbackHttp && address.IsLoopback,
        _ => false,
    };
}
