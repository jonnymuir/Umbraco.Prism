using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Provides context about the currently authenticated Prism user.
/// </summary>
/// <param name="httpContextAccessor">Provides access to the current request user principal.</param>
/// <param name="prismContext">Provides the tenant resolved for the current request.</param>
public class PrismUserContext(
    IHttpContextAccessor httpContextAccessor, 
    IPrismContext prismContext) : IPrismUserContext
{
    /// <summary>
    /// The current user principal.
    /// </summary>
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// Indicates whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// The user's email address as the identity provider vouches for it, or <see langword="null"/>
    /// when it does not (see <see cref="PrismIdentityExtensions.GetEmailAssertion"/>). This is the
    /// identity that owns a visitor's service requests and is matched against rosters, so an
    /// unverified or self-chosen name must not reach it.
    /// </summary>
    public string? Email
    {
        get
        {
            if (User is null)
            {
                return null;
            }

            var (email, verified) = User.GetEmailAssertion(!string.IsNullOrEmpty(CurrentTenant?.OidcAuthority));
            return verified ? email : null;
        }
    }

    /// <summary>
    /// The user's name.
    /// </summary>
    public string? Name => User?.FindFirstValue("name");

    /// <summary>
    /// The Entra Tenant ID claim.
    /// </summary>
    public string? EntraTenantId => User?.FindFirstValue("tid");

    /// <summary>
    /// Returns the Tenant resolved by the Prism Middleware.
    /// </summary>
    public PrismTenant? CurrentTenant => prismContext.CurrentTenant;
}