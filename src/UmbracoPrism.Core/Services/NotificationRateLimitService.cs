using System.Collections.Concurrent;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// In-memory sliding-window rate limiter for notification operations.
/// Limits per userId+tenantId pair to prevent spam and abuse.
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public class NotificationRateLimitService : INotificationRateLimitService
{
    private const int TokenRegistrationLimitPerHour = 10;
    private const int SubscriptionLimitPerHour = 20;
    private static readonly TimeSpan SlidingWindow = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, List<DateTime>> _tokenRegistrations = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _subscriptions = new();

    public (bool IsLimited, int RetryAfterSeconds) CheckTokenRegistrationLimit(string userId, string tenantId)
    {
        var key = $"{tenantId}:{userId}";
        return CheckLimit(_tokenRegistrations, key, TokenRegistrationLimitPerHour);
    }

    public (bool IsLimited, int RetryAfterSeconds) CheckSubscriptionLimit(string userId, string tenantId)
    {
        var key = $"{tenantId}:{userId}";
        return CheckLimit(_subscriptions, key, SubscriptionLimitPerHour);
    }

    private (bool IsLimited, int RetryAfterSeconds) CheckLimit(
        ConcurrentDictionary<string, List<DateTime>> store,
        string key,
        int limit)
    {
        var now = DateTime.UtcNow;
        var windowStart = now - SlidingWindow;

        var attempts = store.GetOrAdd(key, _ => new List<DateTime>());

        lock (attempts)
        {
            // Remove expired entries
            attempts.RemoveAll(t => t < windowStart);

            if (attempts.Count >= limit)
            {
                var oldestInWindow = attempts.Min();
                var retryAfter = (int)(oldestInWindow.Add(SlidingWindow) - now).TotalSeconds + 1;
                return (true, Math.Max(1, retryAfter));
            }

            // Record this attempt
            attempts.Add(now);
            return (false, 0);
        }
    }
}
