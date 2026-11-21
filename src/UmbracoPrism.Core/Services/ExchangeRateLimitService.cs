using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// In-memory sliding-window rate limiter for biometric exchange requests.
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> with per-entry locking.
///
/// A token that reaches the failure threshold is locked for the failure window, the same
/// duration the Retry-After header reports, and then starts afresh. Idle entries are swept, and the
/// number of tracked keys is capped, so unique garbage tokens or addresses cannot grow memory
/// without bound.
///
/// Trade-off: state is not shared across instances. This is acceptable because Umbraco.Prism targets
/// single-instance deployments. If the app moves to a multi-instance topology, replace with a
/// distributed store (e.g. Redis sorted sets with ZRANGEBYSCORE).
/// </summary>
public class ExchangeRateLimitService : IExchangeRateLimitService
{
    /// <summary>The most keys tracked per dictionary. Beyond it new keys are simply not tracked.</summary>
    internal const int MaxTrackedKeys = 50_000;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, TokenRateState> _tokenStates = new();
    private readonly ConcurrentDictionary<string, IpRateState> _ipStates = new();
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _failureWindow;
    private readonly int _ipRequestsPerMinute;
    private readonly TimeProvider _timeProvider;
    private long _lastSweepTicks;

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
        _lastSweepTicks = timeProvider.GetUtcNow().UtcTicks;
    }

    /// <summary>Number of token hashes currently tracked.</summary>
    internal int TrackedTokenCount => _tokenStates.Count;

    /// <summary>Number of addresses currently tracked.</summary>
    internal int TrackedIpCount => _ipStates.Count;

    /// <inheritdoc />
    public (bool IsLimited, int RetryAfterSeconds) CheckTokenLimit(string tokenHash)
    {
        var now = _timeProvider.GetUtcNow();
        SweepIfDue(now);

        // Looking up never creates an entry: only a recorded failure does.
        if (!_tokenStates.TryGetValue(tokenHash, out var state))
            return (false, 0);

        lock (state.SyncRoot)
        {
            if (state.LockedUntil is { } lockedUntil)
            {
                if (now < lockedUntil)
                    return (true, (int)Math.Ceiling((lockedUntil - now).TotalSeconds));

                state.LockedUntil = null;
                state.Failures.Clear();
            }

            PruneWindow(state.Failures, now, _failureWindow);

            if (state.Failures.Count >= _maxFailedAttempts)
            {
                state.LockedUntil = now + _failureWindow;
                return (true, (int)_failureWindow.TotalSeconds);
            }

            return (false, 0);
        }
    }

    /// <inheritdoc />
    public (bool IsLimited, int RetryAfterSeconds) CheckIpLimit(string ipAddress)
    {
        var now = _timeProvider.GetUtcNow();
        SweepIfDue(now);

        var state = GetOrAddBounded(_ipStates, ipAddress, () => new IpRateState());
        if (state is null)
            return (false, 0);

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
        var state = GetOrAddBounded(_tokenStates, tokenHash, () => new TokenRateState());
        if (state is null)
            return;

        var now = _timeProvider.GetUtcNow();

        lock (state.SyncRoot)
        {
            if (state.LockedUntil is { } lockedUntil && now < lockedUntil) return;

            PruneWindow(state.Failures, now, _failureWindow);
            state.Failures.Add(now);

            if (state.Failures.Count >= _maxFailedAttempts)
                state.LockedUntil = now + _failureWindow;
        }
    }

    /// <inheritdoc />
    public void ResetTokenFailures(string tokenHash)
    {
        _tokenStates.TryRemove(tokenHash, out _);
    }

    private static TState? GetOrAddBounded<TState>(ConcurrentDictionary<string, TState> states, string key, Func<TState> create)
        where TState : class
    {
        if (states.TryGetValue(key, out var existing))
            return existing;

        return states.Count >= MaxTrackedKeys ? null : states.GetOrAdd(key, _ => create());
    }

    private void SweepIfDue(DateTimeOffset now)
    {
        var last = Interlocked.Read(ref _lastSweepTicks);
        if (now.UtcTicks - last < SweepInterval.Ticks
            || Interlocked.CompareExchange(ref _lastSweepTicks, now.UtcTicks, last) != last)
            return;

        foreach (var (key, state) in _tokenStates)
        {
            lock (state.SyncRoot)
            {
                var lockActive = state.LockedUntil is { } until && now < until;
                if (!lockActive)
                {
                    PruneWindow(state.Failures, now, _failureWindow);
                    if (state.Failures.Count == 0)
                        _tokenStates.TryRemove(key, out _);
                }
            }
        }

        foreach (var (key, state) in _ipStates)
        {
            lock (state.SyncRoot)
            {
                PruneWindow(state.Requests, now, TimeSpan.FromMinutes(1));
                if (state.Requests.Count == 0)
                    _ipStates.TryRemove(key, out _);
            }
        }
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
        public DateTimeOffset? LockedUntil;
    }

    private sealed class IpRateState
    {
        public readonly object SyncRoot = new();
        public readonly List<DateTimeOffset> Requests = [];
    }
}
