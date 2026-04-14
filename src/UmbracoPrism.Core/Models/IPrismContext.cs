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
    /// Gets the most recent high-level reason why downstream authorization could not be produced.
    /// Intended for diagnostics only and never contains raw token material.
    /// </summary>
    string? LastAuthorizationFailureReason { get; }

    /// <summary>
    /// Gets an authorization header for downstream tenant-scoped API calls.
    /// </summary>
    /// <param name="forceRefresh">
    /// When <see langword="true"/>, bypasses any cached access token and attempts a refresh using the
    /// stored refresh token instead. Useful when a downstream service rejects a still-unexpired token
    /// after an identity-provider restart.
    /// </param>
    /// <returns>
    /// A bearer authorization header when a valid token is available; otherwise <see langword="null"/>.
    /// </returns>
    Task<AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false);
}
