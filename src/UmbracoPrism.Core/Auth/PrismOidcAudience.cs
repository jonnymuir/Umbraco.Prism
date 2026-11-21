using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// The audience rule for a token from a generic OIDC tenant (one with an <see cref="BackOfficeTenant.OidcAuthority"/>):
/// the token must have been minted for this API, so <c>aud</c> contains <see cref="BackOfficeTenant.Audience"/>.
/// <c>azp</c> is deliberately not consulted, so a token issued to the web client for something else, including its
/// ID token, cannot be replayed against the API. A tenant with no <c>Audience</c> accepts no token
/// (<see cref="Extensions.PrismBearerConfigurationCheck"/> refuses to start with one).
/// </summary>
internal static class PrismOidcAudience
{
    public static bool IsAccepted(BackOfficeTenant tenant, IEnumerable<string> audiences) =>
        !string.IsNullOrWhiteSpace(tenant.Audience)
        && audiences.Any(aud => string.Equals(aud, tenant.Audience, StringComparison.Ordinal));
}
