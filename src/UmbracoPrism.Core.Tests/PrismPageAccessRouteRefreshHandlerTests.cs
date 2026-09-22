using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Notifications;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Tests;

public class PrismPageAccessRouteRefreshHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesContentRoute_WhenAGuardedPageIsRepublishedUnderANewRoute()
    {
        var contentKey = Guid.NewGuid();
        var policy = new PrismPageAccessPolicySchema { Id = 1, ContentKey = contentKey, ContentRoute = "/old-name" };

        var db = new Mock<IUmbracoDatabase>();
        db.Setup(d => d.Fetch<PrismPageAccessPolicySchema>()).Returns([policy]);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(db.Object);

        var documentUrlService = new Mock<IDocumentUrlService>();
        documentUrlService.Setup(s => s.GetLegacyRouteFormat(contentKey, null, false)).Returns("/new-name");

        var handler = new PrismPageAccessRouteRefreshHandler(
            dbFactory.Object, documentUrlService.Object, NullLogger<PrismPageAccessRouteRefreshHandler>.Instance);

        var content = Mock.Of<IContent>(c => c.Key == contentKey && c.Name == "Renamed Page");
        var notification = new ContentPublishedNotification([content], new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        policy.ContentRoute.Should().Be("/new-name");
        db.Verify(d => d.Update(policy), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_ForAPublishedPageWithNoPolicy()
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(d => d.Fetch<PrismPageAccessPolicySchema>()).Returns([]);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(db.Object);

        var documentUrlService = new Mock<IDocumentUrlService>();

        var handler = new PrismPageAccessRouteRefreshHandler(
            dbFactory.Object, documentUrlService.Object, NullLogger<PrismPageAccessRouteRefreshHandler>.Instance);

        var content = Mock.Of<IContent>(c => c.Key == Guid.NewGuid() && c.Name == "Unrelated Page");
        var notification = new ContentPublishedNotification([content], new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        documentUrlService.Verify(
            s => s.GetLegacyRouteFormat(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
        db.Verify(d => d.Update(It.IsAny<PrismPageAccessPolicySchema>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LeavesRouteUnchanged_WhenItAlreadyMatches()
    {
        var contentKey = Guid.NewGuid();
        var policy = new PrismPageAccessPolicySchema { Id = 1, ContentKey = contentKey, ContentRoute = "/same-route" };

        var db = new Mock<IUmbracoDatabase>();
        db.Setup(d => d.Fetch<PrismPageAccessPolicySchema>()).Returns([policy]);

        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(db.Object);

        var documentUrlService = new Mock<IDocumentUrlService>();
        documentUrlService.Setup(s => s.GetLegacyRouteFormat(contentKey, null, false)).Returns("/same-route");

        var handler = new PrismPageAccessRouteRefreshHandler(
            dbFactory.Object, documentUrlService.Object, NullLogger<PrismPageAccessRouteRefreshHandler>.Instance);

        var content = Mock.Of<IContent>(c => c.Key == contentKey && c.Name == "Same Page");
        var notification = new ContentPublishedNotification([content], new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        db.Verify(d => d.Update(It.IsAny<PrismPageAccessPolicySchema>()), Times.Never);
    }
}
