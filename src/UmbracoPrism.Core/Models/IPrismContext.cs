using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

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

    /// <summary>
    /// Asks the identity provider whether the member's session is still valid, using the stored
    /// refresh token, and updates the tokens in <paramref name="properties"/> when it is. Does not
    /// write the cookie; the caller renews it. The default does nothing.
    /// </summary>
    Task<PrismSessionCheck> RevalidateSessionAsync(HttpContext context, AuthenticationProperties properties, ClaimsPrincipal principal) =>
        Task.FromResult(PrismSessionCheck.Skipped);
}

/// <summary>The outcome of asking the identity provider whether a member's session is still valid.</summary>
public enum PrismSessionCheck
{
    /// <summary>There was nothing to check with, such as no refresh token.</summary>
    Skipped,

    /// <summary>The provider accepted the session.</summary>
    Active,

    /// <summary>The provider says the session is no longer valid.</summary>
    Revoked,

    /// <summary>The provider could not be reached or answered something other than a revocation.</summary>
    Unreachable
}
