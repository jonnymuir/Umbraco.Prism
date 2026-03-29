using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// In-memory sliding-window rate limiter for biometric exchange requests.
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> with per-entry locking.
///
/// Trade-off: state is not shared across instances. This is acceptable for v1
/// because Umbraco.Prism targets single-instance backoffice deployments. If the
/// app moves to a multi-instance topology, replace with a distributed store
/// (e.g. Redis sorted sets with ZRANGEBYSCORE).
/// </summary>
public class ExchangeRateLimitService : IExchangeRateLimitService
{
    private readonly ConcurrentDictionary<string, TokenRateState> _tokenStates = new();
    private readonly ConcurrentDictionary<string, IpRateState> _ipStates = new();
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _failureWindow;
    private readonly int _ipRequestsPerMinute;
    private readonly TimeProvider _timeProvider;

    /// <summary>Production constructor resolved by DI.</summary>
    public ExchangeRateLimitService(IOptions<PrismBiometricOptions> options)
        : this(options, TimeProvider.System) { }

    /// <summary>Test-seam constructor that accepts a controllable <see cref="TimeProvider"/>.</summary>
    public ExchangeRateLimitService(IOptions<PrismBiometricOptions> options, TimeProvider timeProvider)
    {
        _maxFailedAttempts = options.Value.MaxFailedAttempts;
        _failureWindow = TimeSpan.FromMinutes(options.Value.FailureWindowMinutes);
        _ipRequestsPerMinute = options.Value.PerIpRequestsPerMinute;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public (bool IsLimited, int RetryAfterSeconds) CheckTokenLimit(string tokenHash)
    {
        var state = _tokenStates.GetOrAdd(tokenHash, _ => new TokenRateState());
        var now = _timeProvider.GetUtcNow();

        lock (state.SyncRoot)
        {
            if (state.IsLocked)
                return (true, (int)_failureWindow.TotalSeconds);

            PruneWindow(state.Failures, now, _failureWindow);

            if (state.Failures.Count >= _maxFailedAttempts)
            {
                state.IsLocked = true;
                return (true, (int)_failureWindow.TotalSeconds);
            }

            return (false, 0);
        }
    }

    /// <inheritdoc />
    public (bool IsLimited, int RetryAfterSeconds) CheckIpLimit(string ipAddress)
    {
        var state = _ipStates.GetOrAdd(ipAddress, _ => new IpRateState());
        var now = _timeProvider.GetUtcNow();
        var window = TimeSpan.FromMinutes(1);

        lock (state.SyncRoot)
        {
            PruneWindow(state.Requests, now, window);

            if (state.Requests.Count >= _ipRequestsPerMinute)
            {
                var oldest = state.Requests[0]; // sorted ascending
                var retryAfter = (int)Math.Ceiling((oldest + window - now).TotalSeconds);
                return (true, Math.Max(1, retryAfter));
            }

            state.Requests.Add(now);
            return (false, 0);
        }
    }

    /// <inheritdoc />
    public void RecordTokenFailure(string tokenHash)
    {
        var state = _tokenStates.GetOrAdd(tokenHash, _ => new TokenRateState());
        var now = _timeProvider.GetUtcNow();

        lock (state.SyncRoot)
        {
            if (state.IsLocked) return;

            PruneWindow(state.Failures, now, _failureWindow);
            state.Failures.Add(now);

            if (state.Failures.Count >= _maxFailedAttempts)
                state.IsLocked = true;
        }
    }

    /// <inheritdoc />
    public void ResetTokenFailures(string tokenHash)
    {
        _tokenStates.TryRemove(tokenHash, out _);
    }

    private static void PruneWindow(List<DateTimeOffset> timestamps, DateTimeOffset now, TimeSpan window)
    {
        var cutoff = now - window;
        timestamps.RemoveAll(t => t < cutoff);
    }

    private sealed class TokenRateState
    {
        public readonly object SyncRoot = new();
        public readonly List<DateTimeOffset> Failures = [];
        public bool IsLocked;
    }

    private sealed class IpRateState
    {
        public readonly object SyncRoot = new();
        public readonly List<DateTimeOffset> Requests = [];
    }
}
