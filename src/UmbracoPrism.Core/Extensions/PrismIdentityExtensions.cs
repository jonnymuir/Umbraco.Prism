using System.Security.Claims;

namespace UmbracoPrism.Core.Extensions;

public static class PrismIdentityExtensions
{
    public static string? GetTenantId(this ClaimsPrincipal user) =>
        user.FindFirst("tid")?.Value ?? 
        user.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value ?? 
        user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

    public static BackOfficeTenant? GetPrismTenant(this ClaimsPrincipal user, IConfiguration config)
    {
        var tid = user.GetTenantId();

        // Resolve Tenant from Config
        var tenants = config.GetSection("PrismBackOffice:Tenants").Get<List<BackOfficeTenant>>();
        var tenant = tenants?.FirstOrDefault(t => t.EntraTenantId == tid);
        return tenant;
    }
}