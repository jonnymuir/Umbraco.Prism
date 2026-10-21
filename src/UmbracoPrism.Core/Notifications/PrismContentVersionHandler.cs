using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Notifications;

/// <summary>Moves <see cref="PrismContentVersion"/> on whenever published content changes.</summary>
public class PrismContentVersionHandler(PrismContentVersion contentVersion) :
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>
{
    public void Handle(ContentPublishedNotification notification) => contentVersion.Advance();

    public void Handle(ContentUnpublishedNotification notification) => contentVersion.Advance();
}
