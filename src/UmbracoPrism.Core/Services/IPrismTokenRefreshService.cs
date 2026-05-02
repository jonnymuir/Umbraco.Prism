namespace UmbracoPrism.Core.Services;

/// <summary>
/// Performs the outbound HTTP call to a token endpoint with retry and circuit-breaker resilience.
/// </summary>
public interface IPrismTokenRefreshService
{
    /// <summary>
    /// Posts form parameters to <paramref name="tokenEndpoint"/> and returns the parsed token response.
    /// Retries on transient HTTP errors; circuit breaker opens after repeated failures.
    /// Never logs token values — only status codes and retry counts.
    /// </summary>
    /// <param name="tokenEndpoint">Absolute token endpoint URL for the tenant authority.</param>
    /// <param name="formParameters">Form-encoded parameters required by the token endpoint.</param>
    /// <param name="cancellationToken">Cancellation token for the outbound refresh operation.</param>
    /// <param name="requestHeaders">
    /// Optional HTTP headers added to the outbound request (e.g. <c>X-Forwarded-Proto</c>,
    /// <c>X-Forwarded-Host</c>). Used when the token endpoint has been rewritten to an internal
    /// backchannel address so the upstream server can still derive the correct public issuer URL.
    /// </param>
    /// <returns>The parsed refresh result including success state and returned token values.</returns>
    Task<TokenRefreshResult> RefreshAsync(
        string tokenEndpoint,
        IReadOnlyDictionary<string, string> formParameters,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? requestHeaders = null);
}

/// <summary>Result of a token refresh request.</summary>
/// <param name="Success">Whether the refresh succeeded.</param>
/// <param name="AccessToken">The new access token, or null on failure.</param>
/// <param name="RefreshToken">The new refresh token, or null when not returned.</param>
/// <param name="ExpiresIn">Lifetime in seconds of the new access token, or null when not returned.</param>
/// <param name="FailureReason">High-level refresh failure category for diagnostics, or null on success.</param>
public record TokenRefreshResult(bool Success, string? AccessToken, string? RefreshToken, int? ExpiresIn, string? FailureReason = null);
