using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Extensions;

public static class PrismIdentityExtensions
{
    public static string? GetTenantId(this ClaimsPrincipal user) =>
        user.FindFirst("tid")?.Value ?? 
        user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

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