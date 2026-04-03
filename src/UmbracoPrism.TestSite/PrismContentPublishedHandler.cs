using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Handles ContentPublishedNotification events to send push notifications
/// when new vinyl records are published.
/// Checks for vinylRecord content type and sends notification to subscribers of the genre.
/// </summary>
public class PrismContentPublishedHandler : INotificationAsyncHandler<ContentPublishedNotification>
{
    private readonly IPrismNotificationService _notificationService;
    private readonly ILogger<PrismContentPublishedHandler> _logger;

    public PrismContentPublishedHandler(
        IPrismNotificationService notificationService,
        ILogger<PrismContentPublishedHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.PublishedEntities)
        {
            // Only process vinylRecord content types
            if (entity.ContentType.Alias != "vinylRecord")
                continue;

            try
            {
                // Extract notification properties
                var title = entity.GetValue<string>("title") ?? entity.Name;
                var artist = entity.GetValue<string>("artist") ?? "";
                var notificationGenre = entity.GetValue<string>("notificationGenre");

                if (string.IsNullOrWhiteSpace(notificationGenre))
                {
                    _logger.LogWarning(
                        "VinylRecord '{Name}' (ID: {Id}) published but has no notificationGenre set; skipping notification",
                        entity.Name, entity.Id);
                    continue;
                }

                // Get tenant ID from somewhere - for now using a placeholder
                // In a real scenario, this would come from IPrismUserContext or similar
                var tenantId = "default-tenant"; // TODO: Get from tenant context

                // Build notification message
                var notificationTitle = $"🎵 New arrival in {notificationGenre}";
                var notificationBody = $"{artist} '{title}' just landed at Vinyl Vault!";

                _logger.LogInformation(
                    "Sending notification for vinyl '{Title}' to {Genre} subscribers",
                    title, notificationGenre);

                // Send to all subscribers of this genre
                await _notificationService.SendNotificationToGenreSubscribersAsync(
                    tenantId,
                    notificationGenre,
                    notificationTitle,
                    notificationBody,
                    cancellationToken);

                _logger.LogInformation(
                    "Successfully sent notification for vinyl '{Title}' (Genre: {Genre})",
                    title, notificationGenre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending notification for published vinyl '{Name}' (ID: {Id})",
                    entity.Name, entity.Id);
            }
        }
    }
}
