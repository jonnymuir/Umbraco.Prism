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
    Task<TokenRefreshResult> RefreshAsync(
        string tokenEndpoint,
        IReadOnlyDictionary<string, string> formParameters,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a token refresh request.</summary>
/// <param name="Success">Whether the refresh succeeded.</param>
/// <param name="AccessToken">The new access token, or null on failure.</param>
/// <param name="RefreshToken">The new refresh token, or null when not returned.</param>
/// <param name="ExpiresIn">Lifetime in seconds of the new access token, or null when not returned.</param>
public record TokenRefreshResult(bool Success, string? AccessToken, string? RefreshToken, int? ExpiresIn);
