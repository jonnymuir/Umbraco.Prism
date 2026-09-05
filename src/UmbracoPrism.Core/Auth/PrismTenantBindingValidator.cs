using System.Security.Claims;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Determines whether an authenticated principal's own token claims match the Prism tenant
/// resolved for the current request. This is the single implementation shared by the automatic
/// cookie-validation check (<see cref="Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents.OnValidatePrincipal"/>)
/// and the explicit <c>PrismStrictIsolation</c> authorization policy (<see cref="PrismTenantHandler"/>) —
/// neither should reimplement this comparison independently.
/// </summary>
public interface IPrismTenantBindingValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="principal"/>'s own tenant claims
    /// (Entra <c>tid</c>, or generic-OIDC <c>iss</c>/<c>aud</c>/<c>azp</c>) match <paramref name="tenant"/>.
    /// </summary>
    bool IsBound(ClaimsPrincipal principal, PrismTenant tenant);
}

/// <inheritdoc cref="IPrismTenantBindingValidator"/>
public class PrismTenantBindingValidator : IPrismTenantBindingValidator
{
    /// <inheritdoc />
    public bool IsBound(ClaimsPrincipal principal, PrismTenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.OidcAuthority))
        {
            return IsGenericOidcPrincipalBound(principal, tenant);
        }

        var tenantId = tenant.EntraTenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        var principalTenantId = principal.FindFirstValue("tid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");

        return !string.IsNullOrWhiteSpace(principalTenantId)
            && string.Equals(principalTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenericOidcPrincipalBound(ClaimsPrincipal principal, PrismTenant tenant)
    {
        var principalIssuer = principal.FindFirstValue("iss");
        if (!UrisMatch(principalIssuer, tenant.OidcAuthority))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(tenant.OidcClientId))
        {
            return true;
        }

        var audienceMatches = principal.FindAll("aud")
            .Select(claim => claim.Value)
            .Any(audience => string.Equals(audience, tenant.OidcClientId, StringComparison.OrdinalIgnoreCase));
        var authorizedPartyMatches = string.Equals(
            principal.FindFirstValue("azp"),
            tenant.OidcClientId,
            StringComparison.OrdinalIgnoreCase);

        return audienceMatches || authorizedPartyMatches;
    }

    private static bool UrisMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}
