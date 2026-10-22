using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Automate;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Security regression: a tapped notification navigates the mobile app to its deep link, so only
/// a path on the tenant's own site may get through. Anything else is a phishing link, or script
/// running in the member's signed-in session.
/// </summary>
public class NotificationDeepLinkTests
{
    public static TheoryData<string> OffSiteLinks =>
    [
        "https://evil.example/login",
        "//evil.example/login",
        "/\\evil.example/login",
        "/\t/evil.example",
        "/\n/evil.example",
        "javascript:alert(document.cookie)",
        "JavaScript:alert(1)",
        "data:text/html,<script>alert(1)</script>",
        "orders",
        " /orders",
    ];

    [Theory]
    [InlineData("/")]
    [InlineData("/orders")]
    [InlineData("/apply-for-a-juggling-licence")]
    [InlineData("/orders?id=7&view=full#latest")]
    public void APathOnThisSite_IsAccepted(string link) =>
        PrismDeepLinkPath.IsSitePath(link).Should().BeTrue();

    [Theory]
    [MemberData(nameof(OffSiteLinks))]
    public void ALinkThatCouldLeaveTheSiteOrRunScript_IsRefused(string link) =>
        PrismDeepLinkPath.IsSitePath(link).Should().BeFalse();

    [Theory]
    [MemberData(nameof(OffSiteLinks))]
    public async Task TheNotificationServiceDropsAnOffSiteLink_AndSaysSo(string link) =>
        (await SendAndCountDroppedLinkWarnings(link)).Should().Be(1);

    [Fact]
    public async Task TheNotificationServiceKeepsAPathOnThisSite_WithoutAWarning() =>
        (await SendAndCountDroppedLinkWarnings("/orders")).Should().Be(0);

    [Theory]
    [MemberData(nameof(OffSiteLinks))]
    public async Task TheAutomateActionFailsValidationForAnOffSiteLink_AndSendsNothing(string link)
    {
        var notifications = new Mock<IPrismNotificationService>();
        var action = BuildAction(notifications.Object);

        var result = await action.ExecuteAsync(ContextWith(link), CancellationToken.None);

        result.Status.Should().Be(ActionResultStatus.Failed);
        result.ErrorCategory.Should().Be(StepRunErrorCategory.Validation);
        notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheAutomateActionSendsAPathOnThisSite()
    {
        var notifications = new Mock<IPrismNotificationService>();
        notifications
            .Setup(n => n.SendNotificationToUserAsync("user-1", "tenant-1", "Title", "Body", "/orders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var action = BuildAction(notifications.Object);

        var result = await action.ExecuteAsync(ContextWith("/orders"), CancellationToken.None);

        result.Status.Should().Be(ActionResultStatus.Success);
        notifications.Verify(n => n.SendNotificationToUserAsync(
            "user-1", "tenant-1", "Title", "Body", "/orders", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task<int> SendAndCountDroppedLinkWarnings(string link)
    {
        var db = new Mock<IUmbracoDatabase>();
        db.Setup(d => d.Fetch<string>(It.IsAny<string>(), It.IsAny<object[]>())).Returns(["fcm-token"]);
        var dbFactory = new Mock<IUmbracoDatabaseFactory>();
        dbFactory.Setup(f => f.CreateDatabase()).Returns(db.Object);
        var logger = new Mock<ILogger<PrismNotificationService>>();
        var service = new PrismNotificationService(dbFactory.Object, new PrismFirebase(new ConfigurationBuilder().Build(), NullLogger<PrismFirebase>.Instance), logger.Object);

        await service.SendNotificationToUserAsync("user-1", "tenant-1", "Title", "Body", deepLinkPath: link);

        return logger.Invocations.Count(i =>
            i.Method.Name == nameof(ILogger.Log)
            && (LogLevel)i.Arguments[0] == LogLevel.Warning
            && i.Arguments[2].ToString()!.Contains("not a path on this site"));
    }

    private static PrismSendPushNotificationAction BuildAction(IPrismNotificationService notifications) =>
        new(new ActionInfrastructure(Mock.Of<IEditableModelResolver>()), notifications,
            NullLogger<PrismSendPushNotificationAction>.Instance);

    private static ActionContext ContextWith(string deepLinkPath) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "prism.sendPushNotification",
        Settings = new PrismSendPushNotificationSettings
        {
            UserId = "user-1",
            TenantId = "tenant-1",
            Title = "Title",
            Body = "Body",
            DeepLinkPath = deepLinkPath,
        },
    };
}
