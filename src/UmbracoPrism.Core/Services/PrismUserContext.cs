using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Provides context about the currently authenticated Prism user.
/// </summary>
/// <param name="httpContextAccessor"></param>
/// <param name="prismContext"></param>
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
    /// The user's email address.
    /// </summary>
    public string? Email => User?.FindFirstValue("preferred_username") ?? User?.FindFirstValue(ClaimTypes.Email);

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