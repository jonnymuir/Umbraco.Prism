using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// The audience rule for a token from a generic OIDC tenant (one with an <see cref="BackOfficeTenant.OidcAuthority"/>).
/// <list type="bullet">
/// <item>With <see cref="BackOfficeTenant.Audience"/> set, the token must have been minted for this API:
/// <c>aud</c> contains it. <c>azp</c> is deliberately not consulted, so a token issued to the web client for
/// something else, including its ID token, cannot be replayed against the API.</item>
/// <item>Without it, the token is accepted if <c>aud</c> or <c>azp</c> is the tenant's client id. That keeps existing
/// hosts working, but it trusts anything issued to that client.</item>
/// </list>
/// </summary>
internal static class PrismOidcAudience
{
    public static bool IsAccepted(BackOfficeTenant tenant, IEnumerable<string> audiences, string? authorizedParty)
    {
        if (!string.IsNullOrWhiteSpace(tenant.Audience))
        {
            return audiences.Any(aud => string.Equals(aud, tenant.Audience, StringComparison.Ordinal));
        }

        if (string.IsNullOrWhiteSpace(tenant.ClientId))
        {
            return false;
        }

        return audiences.Any(aud => string.Equals(aud, tenant.ClientId, StringComparison.OrdinalIgnoreCase))
            || string.Equals(authorizedParty, tenant.ClientId, StringComparison.OrdinalIgnoreCase);
    }
}
