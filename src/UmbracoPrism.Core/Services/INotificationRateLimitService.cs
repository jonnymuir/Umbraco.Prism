namespace UmbracoPrism.Core.Services;

/// <summary>
/// Rate limiting service for notification token registration and subscription operations.
/// Prevents spam and abuse by enforcing per-user limits on notification-related actions.
/// </summary>
public interface INotificationRateLimitService
{
    /// <summary>
    /// Checks if the user has exceeded the token registration rate limit.
    /// </summary>
    /// <param name="userId">The user's Entra Object ID.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>A tuple indicating if the user is rate-limited and retry-after seconds.</returns>
    (bool IsLimited, int RetryAfterSeconds) CheckTokenRegistrationLimit(string userId, string tenantId);

    /// <summary>
    /// Checks if the user has exceeded the subscription rate limit.
    /// </summary>
    /// <param name="userId">The user's Entra Object ID.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>A tuple indicating if the user is rate-limited and retry-after seconds.</returns>
    (bool IsLimited, int RetryAfterSeconds) CheckSubscriptionLimit(string userId, string tenantId);
}
