using System.Net.Http.Headers;

namespace UmbracoPrism.Core.Models;

/// <summary>
/// Context interface for managing the current tenant.
/// </summary>
public interface IPrismContext
{
    /// <summary>
    /// Gets or sets the current tenant.
    /// </summary>
    PrismTenant? CurrentTenant { get; set; }

    /// <summary>
    /// Gets an authorization header for downstream tenant-scoped API calls.
    /// </summary>
    /// <returns>
    /// A bearer authorization header when a valid token is available; otherwise <see langword="null"/>.
    /// </returns>
    Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync();
}
