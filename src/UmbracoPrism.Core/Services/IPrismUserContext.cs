using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;
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
    /// A stable identifier for the person that survives a change of email, sign-in name or display
    /// name (see <see cref="PrismIdentityExtensions.GetSubject"/>), or <see langword="null"/> when the
    /// user is not signed in. Key anything that must stay with the person on this, not on
    /// <see cref="Email"/>.
    /// </summary>
    string? Subject => null;

    /// <summary>
    /// The user's name.
    /// </summary>
    string? Name { get; }
    
    /// <summary>
    /// The Entra Tenant ID.
    /// </summary>
    string? EntraTenantId { get; }
    
    /// <summary>
    /// Gets the tenant resolved for the current request.
    /// </summary>
    PrismTenant? CurrentTenant { get; }
}