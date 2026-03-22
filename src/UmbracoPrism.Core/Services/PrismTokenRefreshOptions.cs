namespace UmbracoPrism.Core.Services;

/// <summary>
/// Configurable options for the token refresh resilience pipeline.
/// Bind from appsettings.json under "Prism:TokenRefresh".
/// </summary>
public class PrismTokenRefreshOptions
{
    public const string SectionName = "Prism:TokenRefresh";

    /// <summary>Number of retry attempts after the initial failure (default: 3).</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay in seconds for exponential backoff (default: 1s → 1s, 2s, 4s).</summary>
    public double InitialBackoffSeconds { get; set; } = 1.0;

    /// <summary>
    /// Minimum number of calls in the sampling window before the circuit breaker can trip.
    /// Approximates "open after N failures" (default: 5).
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Proportion of failures required to open the circuit (0.0–1.0, default: 1.0 = all calls failing).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 1.0;

    /// <summary>Sliding window in seconds for failure-rate sampling (default: 30s).</summary>
    public int CircuitBreakerSamplingWindowSeconds { get; set; } = 30;

    /// <summary>Duration in seconds the circuit stays open before moving to half-open (default: 60s).</summary>
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 60;
}
