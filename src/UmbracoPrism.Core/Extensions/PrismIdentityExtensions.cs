using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Extensions;

public static class PrismIdentityExtensions
{
    public static string? GetTenantId(this ClaimsPrincipal user) =>
        user.FindFirst("tid")?.Value ?? 
        user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

    /// <summary>
    /// The person's email as the identity provider vouches for it, for anything that decides who
    /// they are or what they may see: case ownership, a team roster, a membership match.
    /// <para/>
    /// For a tenant with its own identity provider (<paramref name="tenantOwnsIdentityProvider"/>, a
    /// generic OIDC authority such as Keycloak) people can usually register themselves, and the
    /// account's login name (<c>preferred_username</c>) is whatever they typed, so it can be set to
    /// another person's email address. There the email is the <c>email</c> claim and is
    /// <c>Verified</c> only when <c>email_verified</c> says so. A managed Entra directory is
    /// trusted as before: its sign-in name is issued, not chosen.
    /// </summary>
    public static (string? Email, bool Verified) GetEmailAssertion(this ClaimsPrincipal user, bool tenantOwnsIdentityProvider)
    {
        if (!tenantOwnsIdentityProvider)
        {
            var managed = user.GetEmail();
            return (managed, !string.IsNullOrWhiteSpace(managed));
        }

        var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
        var verified = !string.IsNullOrWhiteSpace(email)
            && bool.TryParse(user.FindFirst("email_verified")?.Value, out var claimed) && claimed;
        return (email, verified);
    }

    /// <summary>
    /// The sign-in name of a managed (Entra) directory, falling back to the email claim. Do not use
    /// this to identify a person at a tenant that has its own identity provider: use
    /// <see cref="GetEmailAssertion"/>, which does not trust a login name the person chose.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value ?? 
        user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

    public static BackOfficeTenant? GetPrismTenant(this ClaimsPrincipal user, PrismTenantResolver resolver)
    {
        var tid = user.GetTenantId();
        if (!string.IsNullOrEmpty(tid)) return resolver(tid);
        var iss = user.FindFirst("iss")?.Value;
        return string.IsNullOrEmpty(iss) ? null : resolver(iss);
    }
}

public delegate BackOfficeTenant? PrismTenantResolver(string tenantId);

public static class PrismResolvers
{
    // A factory method that returns a resolver bound to your configuration
    public static PrismTenantResolver FromConfig(IConfiguration config) => (key) =>
    {
        var tenants = config.GetSection("PrismBusinessApp:Tenants").Get<List<BackOfficeTenant>>();
        return tenants?.FirstOrDefault(t =>
            string.Equals(t.EntraTenantId, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.OidcAuthority?.TrimEnd('/'), key.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    };
}