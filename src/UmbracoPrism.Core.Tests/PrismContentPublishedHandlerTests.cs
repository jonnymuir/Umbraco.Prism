using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Notifications;
using UmbracoPrism.Core.Notifications;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Unit tests for PrismContentPublishedHandler.
/// Tests notification handler logic and routing to the correct service methods.
/// </summary>
public class PrismContentPublishedHandlerTests
{
    // ------------------------------------------------------------------ Helpers

    private static PrismContentPublishedHandler BuildHandler(
        IConfiguration? config = null,
        Mock<IPrismNotificationService>? serviceMock = null)
    {
        serviceMock ??= new Mock<IPrismNotificationService>();

        config ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prism:Notifications:NotifiableContentTypes"] = "newsArticle,announcement"
            })
            .Build();

        var logger = new Mock<ILogger<PrismContentPublishedHandler>>();

        return new PrismContentPublishedHandler(
            serviceMock.Object,
            config,
            logger.Object);
    }

    private static IContent CreateMockContent(
        string contentTypeAlias,
        string name = "Test Content",
        string? tenantId = "tenant-1",
        string? notificationGenre = null)
    {
        var contentTypeMock = new Mock<ISimpleContentType>();
        contentTypeMock.Setup(ct => ct.Alias).Returns(contentTypeAlias);

        var contentMock = new Mock<IContent>();
        contentMock.Setup(c => c.ContentType).Returns(contentTypeMock.Object);
        contentMock.Setup(c => c.Name).Returns(name);

        // Mock GetValue<string> for property access
        contentMock.Setup(c => c.GetValue<string>("prismTenantId", null, null, false))
            .Returns(tenantId);

        contentMock.Setup(c => c.GetValue<string>("notificationGenre", null, null, false))
            .Returns(notificationGenre);

        return contentMock.Object;
    }

    // ------------------------------------------------------------------ Notification Routing

    [Fact]
    public async Task Handle_ContentWithNotificationGenre_SendsToGenreSubscribers()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "newsArticle",
            name: "Breaking News",
            tenantId: "tenant-42",
            notificationGenre: "news");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            "tenant-42", "news", "Breaking News", "New content has been published.", default), Times.Once);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ContentWithoutNotificationGenre_SendsToAllMembers()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "announcement",
            name: "System Announcement",
            tenantId: "tenant-99",
            notificationGenre: null);

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            "tenant-99", "System Announcement", "New content has been published.", default), Times.Once);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ContentWithWhitespaceGenre_SendsToAllMembers()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "newsArticle",
            name: "Article",
            tenantId: "tenant-1",
            notificationGenre: "   ");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            "tenant-1", "Article", "New content has been published.", default), Times.Once);
    }

    [Fact]
    public async Task Handle_ContentTypeNotInNotifiableList_DoesNotSend()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "blogPost", // Not in the configured notifiable types
            name: "Blog Post",
            tenantId: "tenant-1",
            notificationGenre: "blog");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoConfiguredNotifiableTypes_DoesNotSend()
    {
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(config: emptyConfig, serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "newsArticle",
            name: "Article",
            tenantId: "tenant-1");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ContentWithoutTenantId_DoesNotSend()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "newsArticle",
            name: "Orphaned Content",
            tenantId: null);

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MultiplePublishedEntities_ProcessesEach()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content1 = CreateMockContent(
            contentTypeAlias: "newsArticle",
            name: "News 1",
            tenantId: "tenant-1",
            notificationGenre: "news");

        var content2 = CreateMockContent(
            contentTypeAlias: "announcement",
            name: "Announcement 1",
            tenantId: "tenant-2",
            notificationGenre: null);

        var notification = new ContentPublishedNotification(
            new[] { content1, content2 },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            "tenant-1", "news", "News 1", "New content has been published.", default), Times.Once);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            "tenant-2", "Announcement 1", "New content has been published.", default), Times.Once);
    }

    [Fact]
    public async Task Handle_CaseInsensitiveContentTypeMatch_SendsNotification()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "NEWSARTICLE", // Uppercase, but should match "newsArticle"
            name: "Case Test",
            tenantId: "tenant-1",
            notificationGenre: "news");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            "tenant-1", "news", "Case Test", "New content has been published.", default), Times.Once);
    }

    // ------------------------------------------------------------------ Exception Handling

    [Fact]
    public async Task Handle_ServiceThrows_DoesNotRethrow()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        serviceMock.Setup(s => s.SendNotificationToAllMembersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FCM quota exceeded"));

        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "announcement",
            name: "Test",
            tenantId: "tenant-1");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        var act = async () => await handler.HandleAsync(notification, CancellationToken.None);

        await act.Should().NotThrowAsync("exceptions must never break the publish pipeline");
    }

    [Fact]
    public async Task Handle_GenreServiceThrows_DoesNotRethrow()
    {
        var serviceMock = new Mock<IPrismNotificationService>();
        serviceMock.Setup(s => s.SendNotificationToGenreSubscribersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Firebase timeout"));

        var handler = BuildHandler(serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "newsArticle",
            name: "Test",
            tenantId: "tenant-1",
            notificationGenre: "news");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        var act = async () => await handler.HandleAsync(notification, CancellationToken.None);

        await act.Should().NotThrowAsync("exceptions must never break the publish pipeline");
    }

    // ------------------------------------------------------------------ vinylRecord Boundary Regression Guards

    /// <summary>
    /// Regression guard: when vinylRecord is explicitly listed in NotifiableContentTypes,
    /// the Core config-driven handler must route to genre subscribers (genre present).
    /// This proves the boundary refactor preserved the send-to-genre path for vinyl content.
    /// </summary>
    [Fact]
    public async Task Handle_VinylRecord_ConfigDriven_WithGenre_SendsToGenreSubscribers()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prism:Notifications:NotifiableContentTypes"] = "vinylRecord"
            })
            .Build();

        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(config: config, serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "vinylRecord",
            name: "Kind of Blue",
            tenantId: "tenant-vinyl",
            notificationGenre: "jazz");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            "tenant-vinyl", "jazz", "Kind of Blue", "New content has been published.", default), Times.Once);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression guard: when vinylRecord is in config but has no genre,
    /// the Core handler must fall back to all-members broadcast.
    /// </summary>
    [Fact]
    public async Task Handle_VinylRecord_ConfigDriven_WithoutGenre_SendsToAllMembers()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prism:Notifications:NotifiableContentTypes"] = "vinylRecord"
            })
            .Build();

        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(config: config, serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "vinylRecord",
            name: "Rumours",
            tenantId: "tenant-vinyl",
            notificationGenre: null);

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            "tenant-vinyl", "Rumours", "New content has been published.", default), Times.Once);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Double-fire prevention guard: when vinylRecord is NOT in NotifiableContentTypes,
    /// the Core handler must remain completely silent — allowing the TestSite handler
    /// (if registered) to be the sole sender. This prevents double notifications
    /// when both composers are active in the TestSite runtime.
    /// </summary>
    [Fact]
    public async Task Handle_VinylRecord_NotInConfig_CoreHandlerIsSilent_DoubleFirGuard()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prism:Notifications:NotifiableContentTypes"] = "newsArticle,announcement"
                // vinylRecord deliberately absent — TestSite handler owns this type
            })
            .Build();

        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(config: config, serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "vinylRecord",
            name: "Abbey Road",
            tenantId: "tenant-vinyl",
            notificationGenre: "rock");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Core handler must not fire for vinylRecord when it is absent from NotifiableContentTypes — prevents double-fire with TestSite handler");

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Core handler must not fire for vinylRecord when it is absent from NotifiableContentTypes — prevents double-fire with TestSite handler");
    }

    /// <summary>
    /// Double-fire prevention guard: config with empty NotifiableContentTypes means
    /// the Core handler is entirely inert — regardless of content type published.
    /// </summary>
    [Fact]
    public async Task Handle_EmptyNotifiableTypes_CoreHandlerIsSilent_ForAnyContentType()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prism:Notifications:NotifiableContentTypes"] = ""
            })
            .Build();

        var serviceMock = new Mock<IPrismNotificationService>();
        var handler = BuildHandler(config: config, serviceMock: serviceMock);

        var content = CreateMockContent(
            contentTypeAlias: "vinylRecord",
            name: "Nevermind",
            tenantId: "tenant-vinyl",
            notificationGenre: "grunge");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        await handler.HandleAsync(notification, CancellationToken.None);

        serviceMock.Verify(s => s.SendNotificationToGenreSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Empty config must produce a fully inert Core handler");

        serviceMock.Verify(s => s.SendNotificationToAllMembersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Empty config must produce a fully inert Core handler");
    }
}
