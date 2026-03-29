namespace UmbracoPrism.Core.Services;

/// <summary>
/// Per-token and per-IP rate limiting for the biometric exchange endpoint.
/// </summary>
public interface IExchangeRateLimitService
{
    /// <summary>
    /// Checks whether the given token hash is rate-limited (locked after exceeding the
    /// failure threshold within the sliding window).
    /// </summary>
    /// <returns>
    /// A tuple where <c>IsLimited</c> indicates lockout status and <c>RetryAfterSeconds</c>
    /// provides the value for the Retry-After response header.
    /// </returns>
    (bool IsLimited, int RetryAfterSeconds) CheckTokenLimit(string tokenHash);

    /// <summary>
    /// Checks whether the given IP address has exceeded the per-IP request rate.
    /// Also records the current request for sliding-window tracking when under the limit.
    /// </summary>
    (bool IsLimited, int RetryAfterSeconds) CheckIpLimit(string ipAddress);

    /// <summary>
    /// Records a failed exchange attempt for the given token hash. When the failure
    /// count reaches the configured threshold, the token hash is permanently locked
    /// until re-registration (which produces a new hash).
    /// </summary>
    void RecordTokenFailure(string tokenHash);

    /// <summary>
    /// Resets the failure counter for a token hash after a successful exchange.
    /// </summary>
    void ResetTokenFailures(string tokenHash);
}
