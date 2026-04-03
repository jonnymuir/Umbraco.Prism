namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for managing push notification device tokens, genre subscriptions,
/// and FCM fan-out delivery.
/// </summary>
public interface IPrismNotificationService
{
    /// <summary>
    /// Registers (or updates) a Firebase push token for the given user + tenant device record.
    /// Performs an upsert on <c>prismDeviceCredentials</c> matching on UserId and TenantId.
    /// </summary>
    Task RegisterDeviceTokenAsync(string userId, string tenantId, string pushToken, CancellationToken ct = default);

    /// <summary>
    /// Clears the push token for the given user + tenant so notifications are no longer delivered.
    /// </summary>
    Task UnregisterDeviceTokenAsync(string userId, string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Subscribes the user to a notification genre within the given tenant.
    /// Idempotent — repeated calls are safe.
    /// </summary>
    Task SubscribeToGenreAsync(string userId, string tenantId, string genre, CancellationToken ct = default);

    /// <summary>
    /// Removes the user's subscription to a notification genre within the given tenant.
    /// </summary>
    Task UnsubscribeFromGenreAsync(string userId, string tenantId, string genre, CancellationToken ct = default);

    /// <summary>
    /// Sends a push notification to all users subscribed to <paramref name="genre"/> within the tenant.
    /// FCM delivery uses batches of 500 tokens. Stale/unregistered tokens are nullified automatically.
    /// </summary>
    Task SendNotificationToGenreSubscribersAsync(
        string tenantId,
        string genre,
        string title,
        string body,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a push notification to every member with a registered push token in the tenant.
    /// Use sparingly for global announcements.
    /// </summary>
    Task SendNotificationToAllMembersAsync(
        string tenantId,
        string title,
        string body,
        CancellationToken ct = default);
}
