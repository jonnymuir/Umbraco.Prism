using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace UmbracoPrism.Core;

/// <summary>
/// Ensures required Prism document types exist on application startup.
/// Runs idempotently — only creates types if they don't already exist.
/// </summary>
public class PrismContentTypeSeeder(
    IContentTypeService contentTypeService,
    IShortStringHelper shortStringHelper,
    IRuntimeState runtimeState)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        // Only run if Umbraco is fully installed and running
        if (runtimeState.Level < RuntimeLevel.Run) return;

        await Task.Run(() =>
        {
            EnsureDocumentType("homePage", "Home Page", allowedAsRoot: true);
            EnsureDocumentType("memberDashboard", "Member Dashboard", allowedAsRoot: false);
        }, cancellationToken);
    }

    private void EnsureDocumentType(string alias, string name, bool allowedAsRoot)
    {
        if (contentTypeService.Get(alias) != null) return; // Already exists

        var contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            AllowedAsRoot = allowedAsRoot,
            Icon = alias == "homePage" ? "icon-home" : "icon-dashboard"
        };

        // In v17, Save is used for both create and update
        contentTypeService.Save(contentType);
    }
}
