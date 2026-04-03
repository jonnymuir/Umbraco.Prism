using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Notifications;

/// <summary>
/// Fires push notifications when Umbraco content is published.
/// Content types that trigger notifications are configured via
/// <c>Prism:Notifications:NotifiableContentTypes</c> (comma-separated aliases).
/// If the published content has a <c>notificationGenre</c> property, the notification
/// is sent to genre subscribers; otherwise it is broadcast to all tenant members.
/// </summary>
public class PrismContentPublishedHandler(
    IPrismNotificationService notificationService,
    IConfiguration configuration,
    ILogger<PrismContentPublishedHandler> logger) : INotificationAsyncHandler<ContentPublishedNotification>
{
    private const string NotifiableContentTypesKey = "Prism:Notifications:NotifiableContentTypes";
    private const string NotificationGenreAlias = "notificationGenre";
    private const string TenantIdAlias = "prismTenantId";

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        var notifiableTypes = GetNotifiableContentTypes();
        if (notifiableTypes.Count == 0)
            return;

        foreach (var content in notification.PublishedEntities)
        {
            if (!notifiableTypes.Contains(content.ContentType.Alias, StringComparer.OrdinalIgnoreCase))
                continue;

            // Resolve tenantId from a content property; fall back to the content name for diagnostic context.
            var tenantId = content.GetValue<string>(TenantIdAlias);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                logger.LogDebug(
                    "PrismContentPublishedHandler: content '{Name}' has no '{Alias}' property — skipping notification.",
                    content.Name, TenantIdAlias);
                continue;
            }

            var title = content.Name ?? "New content";
            var body = "New content has been published.";

            var genre = content.GetValue<string>(NotificationGenreAlias);

            try
            {
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    await notificationService.SendNotificationToGenreSubscribersAsync(
                        tenantId, genre, title, body, cancellationToken);
                }
                else
                {
                    await notificationService.SendNotificationToAllMembersAsync(
                        tenantId, title, body, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Swallow: must never break the publish pipeline
                logger.LogError(
                    ex,
                    "PrismContentPublishedHandler: failed to send notification for content '{Name}' in tenant {TenantId}.",
                    content.Name, tenantId);
            }
        }
    }

    private IReadOnlyList<string> GetNotifiableContentTypes()
    {
        var raw = configuration[NotifiableContentTypesKey];
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
