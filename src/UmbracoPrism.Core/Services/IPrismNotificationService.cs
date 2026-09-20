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

    /// <summary>
    /// Sends a push notification to a single user's registered device(s) within the tenant — the
    /// targeted counterpart to <see cref="SendNotificationToGenreSubscribersAsync"/>/
    /// <see cref="SendNotificationToAllMembersAsync"/>'s broadcast delivery. A no-op if the user
    /// has no registered push token.
    /// </summary>
    /// <param name="deepLinkPath">
    /// Optional site-relative path (e.g. <c>/apply-for-a-juggling-licence</c>) delivered to the
    /// device as the FCM message's <c>data.url</c> field, so the mobile shell can route straight
    /// to that page when the notification is tapped instead of just opening the app to wherever
    /// it last was.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only if at least one device was actually sent to — never throws for
    /// "nothing to send to" (no registered device, Firebase not configured, every send rejected),
    /// so a caller must check this to tell a real send from a silent no-op. Found live: an
    /// automation action that only awaited this without checking the (previously <c>void</c>)
    /// result logged "sent" and reported success regardless of whether anything was actually
    /// delivered.
    /// </returns>
    Task<bool> SendNotificationToUserAsync(
        string userId,
        string tenantId,
        string title,
        string body,
        string? deepLinkPath = null,
        CancellationToken ct = default);
}
