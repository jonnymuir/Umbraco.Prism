using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Unit tests for PrismNotificationService.
/// Note: Firebase Cloud Messaging integration is mocked at the service level.
/// </summary>
public class PrismNotificationServiceTests
{
    // ------------------------------------------------------------------ Helpers

    private static (PrismNotificationService Service, Mock<IUmbracoDatabase> Db) BuildService(
        string? firebaseCredentialJson = null)
    {
        var mockDb = new Mock<IUmbracoDatabase>();
        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(mockDb.Object);

        var config = new ConfigurationBuilder();
        if (!string.IsNullOrWhiteSpace(firebaseCredentialJson))
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prism:Firebase:CredentialJson"] = firebaseCredentialJson
            });
        }

        var logger = new Mock<ILogger<PrismNotificationService>>();

        var service = new PrismNotificationService(
            dbFactory.Object,
            config.Build(),
            logger.Object);

        return (service, mockDb);
    }

    // ------------------------------------------------------------------ Token Registration

    [Fact]
    public async Task RegisterDeviceToken_SavesToDatabase_WhenNoExistingRecord()
    {
        var (service, db) = BuildService();

        db.Setup(d => d.FirstOrDefault<PrismDeviceCredentialSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((PrismDeviceCredentialSchema?)null);

        await service.RegisterDeviceTokenAsync("user-1", "tenant-1", "fcm-token-abc");

        db.Verify(d => d.Insert(It.Is<PrismDeviceCredentialSchema>(r =>
            r.UserId == "user-1" &&
            r.TenantId == "tenant-1" &&
            r.PushToken == "fcm-token-abc" &&
            r.DeviceId == "push-only-user-1" &&
            r.TokenHash == string.Empty
        )), Times.Once);
    }

    [Fact]
    public async Task RegisterDeviceToken_UpdatesToken_WhenRecordAlreadyExists()
    {
        var (service, db) = BuildService();

        var existingRecord = new PrismDeviceCredentialSchema
        {
            Id = 42,
            UserId = "user-1",
            TenantId = "tenant-1",
            DeviceId = "device-uuid-1",
            PushToken = "old-token"
        };

        db.Setup(d => d.FirstOrDefault<PrismDeviceCredentialSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(existingRecord);

        await service.RegisterDeviceTokenAsync("user-1", "tenant-1", "new-token-xyz");

        db.Verify(d => d.Execute(
            It.Is<string>(sql => sql.Contains("UPDATE prismDeviceCredentials SET PushToken")),
            "new-token-xyz", "user-1", "tenant-1"), Times.Once);

        db.Verify(d => d.Insert(It.IsAny<PrismDeviceCredentialSchema>()), Times.Never);
    }

    [Fact]
    public async Task UnregisterDeviceToken_NullsToken()
    {
        var (service, db) = BuildService();

        await service.UnregisterDeviceTokenAsync("user-1", "tenant-1");

        db.Verify(d => d.Execute(
            It.Is<string>(sql => sql.Contains("UPDATE prismDeviceCredentials SET PushToken = NULL")),
            "user-1", "tenant-1"), Times.Once);
    }

    // ------------------------------------------------------------------ Genre Subscriptions

    [Fact]
    public async Task SubscribeToGenre_CreatesSubscription_WhenNotAlreadySubscribed()
    {
        var (service, db) = BuildService();

        db.Setup(d => d.FirstOrDefault<PrismNotificationSubscriptionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((PrismNotificationSubscriptionSchema?)null);

        await service.SubscribeToGenreAsync("user-1", "tenant-1", "news");

        db.Verify(d => d.Insert(It.Is<PrismNotificationSubscriptionSchema>(s =>
            s.UserId == "user-1" &&
            s.TenantId == "tenant-1" &&
            s.Genre == "news"
        )), Times.Once);
    }

    [Fact]
    public async Task SubscribeToGenre_IsIdempotent_WhenAlreadySubscribed()
    {
        var (service, db) = BuildService();

        var existingSubscription = new PrismNotificationSubscriptionSchema
        {
            Id = 1,
            UserId = "user-1",
            TenantId = "tenant-1",
            Genre = "news",
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };

        db.Setup(d => d.FirstOrDefault<PrismNotificationSubscriptionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(existingSubscription);

        await service.SubscribeToGenreAsync("user-1", "tenant-1", "news");

        db.Verify(d => d.Insert(It.IsAny<PrismNotificationSubscriptionSchema>()), Times.Never);
    }

    [Fact]
    public async Task UnsubscribeFromGenre_RemovesSubscription()
    {
        var (service, db) = BuildService();

        await service.UnsubscribeFromGenreAsync("user-1", "tenant-1", "alerts");

        db.Verify(d => d.Execute(
            It.Is<string>(sql => sql.Contains("DELETE FROM prismNotificationSubscriptions")),
            "user-1", "tenant-1", "alerts"), Times.Once);
    }

    // ------------------------------------------------------------------ Notification Delivery

    [Fact]
    public async Task SendToGenreSubscribers_NoSubscribers_DoesNotThrow()
    {
        var (service, db) = BuildService();

        db.Setup(d => d.Fetch<PrismNotificationSubscriptionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new List<PrismNotificationSubscriptionSchema>());

        var act = async () => await service.SendNotificationToGenreSubscribersAsync(
            "tenant-1", "news", "Title", "Body");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToGenreSubscribers_WithSubscribers_QueuesTokensFromDatabase()
    {
        // Note: This test verifies that the service collects tokens from the database.
        // Actual FCM delivery is tested via integration tests or verified through logs.
        // Since Firebase is initialized internally and not easily mockable, we verify
        // the database query path. Full FCM coverage requires a test double for FirebaseMessaging.

        var (service, db) = BuildService();

        var subscriptions = new List<PrismNotificationSubscriptionSchema>
        {
            new() { UserId = "user-1", TenantId = "tenant-1", Genre = "news" },
            new() { UserId = "user-2", TenantId = "tenant-1", Genre = "news" }
        };

        db.Setup(d => d.Fetch<PrismNotificationSubscriptionSchema>(
                It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(subscriptions);

        db.Setup(d => d.Fetch<string>(
                It.Is<string>(sql => sql.Contains("SELECT PushToken FROM prismDeviceCredentials")),
                It.IsAny<object[]>()))
            .Returns(new List<string> { "token-1", "token-2" });

        // FCM is not initialised because no credential config was provided, so this will log a warning
        // and return early. The test verifies that the subscription and token resolution logic runs.
        await service.SendNotificationToGenreSubscribersAsync("tenant-1", "news", "Title", "Body");

        db.Verify(d => d.Fetch<PrismNotificationSubscriptionSchema>(
            It.Is<string>(sql => sql.Contains("Genre")),
            "tenant-1", "news"), Times.Once);

        db.Verify(d => d.Fetch<string>(
            It.Is<string>(sql => sql.Contains("SELECT PushToken FROM prismDeviceCredentials")),
            It.IsAny<object[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendToAllMembers_NoTokens_DoesNotThrow()
    {
        var (service, db) = BuildService();

        db.Setup(d => d.Fetch<string>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new List<string>());

        var act = async () => await service.SendNotificationToAllMembersAsync(
            "tenant-1", "Title", "Body");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToAllMembers_WithTokens_QueriesDatabase()
    {
        var (service, db) = BuildService();

        db.Setup(d => d.Fetch<string>(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns(new List<string> { "token-a", "token-b", "token-c" });

        // FCM not initialised — service will log a warning and return early
        await service.SendNotificationToAllMembersAsync("tenant-1", "Announcement", "All members message");

        db.Verify(d => d.Fetch<string>(
            It.Is<string>(sql => sql.Contains("SELECT PushToken FROM prismDeviceCredentials")),
            "tenant-1"), Times.Once);
    }
}
