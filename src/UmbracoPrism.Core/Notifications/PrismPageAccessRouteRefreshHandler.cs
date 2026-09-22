using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Notifications;

/// <summary>
/// Keeps PrismPageAccessPolicySchema.ContentRoute fresh whenever a guarded page is (re)published
/// — including after a rename or move. Pure bookkeeping: ContentRoute is never read for
/// enforcement (that's ContentKey-only, so this handler never running, or running late, can
/// never un-protect a page), only for uSync export/import portability across environments (see
/// PrismPageAccessPolicySerializer). No cache invalidation needed here either, for the same
/// reason — IPrismPageAccessResolver never reads this column.
/// </summary>
public class PrismPageAccessRouteRefreshHandler(
    IUmbracoDatabaseFactory databaseFactory,
    IDocumentUrlService documentUrlService,
    ILogger<PrismPageAccessRouteRefreshHandler> logger) : INotificationAsyncHandler<ContentPublishedNotification>
{
    public Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        var policies = db.Fetch<PrismPageAccessPolicySchema>();
        if (policies.Count == 0) return Task.CompletedTask;

        var policiesByContentKey = policies.ToDictionary(p => p.ContentKey);

        foreach (var content in notification.PublishedEntities)
        {
            if (!policiesByContentKey.TryGetValue(content.Key, out var policy)) continue;

            var freshRoute = documentUrlService.GetLegacyRouteFormat(content.Key, culture: null, isDraft: false);
            if (freshRoute == policy.ContentRoute) continue;

            policy.ContentRoute = freshRoute;
            db.Update(policy);

            logger.LogInformation(
                "Prism page-access policy {Id}: refreshed route to '{Route}' after {Name} was published.",
                policy.Id, freshRoute, content.Name);
        }

        return Task.CompletedTask;
    }
}
