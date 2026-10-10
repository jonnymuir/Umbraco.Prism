using System.Security.Claims;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Who is calling and which tenant they belong to, read from the validated token's claims and
/// nothing else. Every route that acts for a caller resolves this first, so no handler ever takes
/// a tenant or user from the request body, query or headers.
/// </summary>
public sealed record CallerIdentity(BackOfficeTenant Tenant, string Email)
{
    /// <summary>
    /// The key under which anything this caller creates is stored. It includes the tenant, so the
    /// same person under two tenants owns two separate sets of data.
    /// </summary>
    public string OwnerKey => $"{Tenant.Code}\n{Email.ToLowerInvariant()}";

    /// <summary>
    /// <see langword="null"/> when the token carries no email, or its tenant is not one this app
    /// trusts. Token validation has already pinned the issuer to a configured tenant, so a null
    /// here is a malformed or incomplete token, which the caller should answer with 403.
    /// </summary>
    public static CallerIdentity? From(ClaimsPrincipal user, IConfiguration config)
    {
        var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
        var email = user.GetEmail();
        return tenant is null || string.IsNullOrWhiteSpace(email) ? null : new CallerIdentity(tenant, email);
    }

    /// <summary>The caller's directory entry for their own tenant, or <see langword="null"/> if they are not a member of it.</summary>
    public BackOfficeMember? FindMember(IConfiguration config) =>
        config.GetSection("PrismBusinessApp:Members").Get<List<BackOfficeMember>>()?
            .FirstOrDefault(m => m.Email.Equals(Email, StringComparison.OrdinalIgnoreCase) && m.TenantCode == Tenant.Code);
}
