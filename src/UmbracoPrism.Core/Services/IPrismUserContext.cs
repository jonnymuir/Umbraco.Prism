using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Provides context about the currently authenticated Prism user.
/// </summary>
public interface IPrismUserContext
{
    /// <summary>
    /// Indicates whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// The user's email address.
    /// </summary>
    string? Email { get; }
    
    /// <summary>
    /// The user's name.
    /// </summary>
    string? Name { get; }
    
    /// <summary>
    /// The Entra Tenant ID.
    /// </summary>
    string? EntraTenantId { get; }
    
    // The "Prism" specific data
    PrismTenant? CurrentTenant { get; }
}