using System.Security.Claims;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Who is calling and which tenant they belong to, read from the validated token's claims and
/// nothing else. Every route that acts for a caller resolves this first, so no handler ever takes
/// a tenant or user from the request body, query or headers.
/// </summary>
/// <param name="Subject">The identity provider's stable id for the person (<c>sub</c>), empty when the token has none.</param>
/// <param name="EmailVerified">Whether the identity provider vouches that the person controls <paramref name="Email"/>.</param>
public sealed record CallerIdentity(BackOfficeTenant Tenant, string Email, string Subject, bool EmailVerified)
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
    /// <para/>
    /// For a tenant with its own identity provider (one that lets people register themselves) the
    /// email is the <c>email</c> claim and only counts as verified when <c>email_verified</c> says so.
    /// It is never <c>preferred_username</c>: that is the account's login name, which a person
    /// chooses at registration, so it can be set to someone else's email address. An Entra tenant's
    /// directory is managed, so its sign-in name is trusted as before.
    /// </summary>
    public static CallerIdentity? From(ClaimsPrincipal user, IConfiguration config)
    {
        var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));
        if (tenant is null)
        {
            return null;
        }

        var (email, verified) = user.GetEmailAssertion(!string.IsNullOrEmpty(tenant.OidcAuthority));
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        return new CallerIdentity(tenant, email, subject, verified);
    }
}
