using AwesomeAssertions;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Core.Services.JourneyStatus;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Journey statuses (Design/mobile-journey-status.md): a waiting journey starts a Live Activity on
/// an iPhone and a Live Update on Android, updates it in place, and ends it when the wait is over.
/// </summary>
public class PrismJourneyStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

    private static readonly PrismJourneyStatus Deciding = new(
        "request-1", "Apply for a juggling licence", "Deciding your application", 2, 3, "/apply-for-a-juggling-licence", PrismJourneyStatusKind.Waiting);

    private static readonly PrismJourneyStatus Decided = Deciding with { Status = "A decision has been made", Step = 3, Kind = PrismJourneyStatusKind.Done };

    [Fact]
    public void AWaitingJourney_StartsALiveActivity_OnAnIPhoneWithNothingRunning()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([IPhone()], stored: null, Update(Deciding), Now);

        var aps = Aps(plan.Messages.Should().ContainSingle().Subject, "start-token");
        aps["event"].Should().Be("start");
        aps["attributes-type"].Should().Be("PrismJourneyStatusAttributes");
        aps["attributes"].Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["key"] = "request-1",
            ["serviceName"] = "Apply for a juggling licence",
            ["path"] = "/apply-for-a-juggling-licence"
        });
        aps["content-state"].Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["status"] = "Deciding your application",
            ["step"] = 2,
            ["steps"] = 3
        });
        plan.Save.Should().BeEquivalentTo(new { JourneyKey = "request-1", Kind = "Waiting", ActivityToken = (string?)null });
    }

    [Fact]
    public void AStartedActivity_WhoseTokenHasNotArrived_IsSentNothing_AndItsLatestStatusIsKept()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([IPhone()], Stored("Checking your details"), Update(Deciding), Now);

        plan.Messages.Should().BeEmpty();
        plan.Save!.Status.Should().Be("Deciding your application");
    }

    [Fact]
    public void ARunningActivity_IsUpdatedInPlace_ThroughItsOwnToken()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([IPhone()], Stored("Checking your details", "activity-token"), Update(Deciding), Now);

        var aps = Aps(plan.Messages.Should().ContainSingle().Subject, "activity-token");
        aps["event"].Should().Be("update");
        ((Dictionary<string, object>)aps["content-state"])["status"].Should().Be("Deciding your application");
        plan.Save!.ActivityToken.Should().Be("activity-token");
    }

    [Fact]
    public void AJourneyThatIsDone_EndsItsActivity_WhichShowsTheLastStatusForAShortWhile()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([IPhone()], Stored("Deciding your application", "activity-token"), Update(Decided), Now);

        var aps = Aps(plan.Messages.Should().ContainSingle().Subject, "activity-token");
        aps["event"].Should().Be("end");
        aps["dismissal-date"].Should().Be(Now.AddMinutes(15).ToUnixTimeSeconds());
        ((Dictionary<string, object>)aps["content-state"])["status"].Should().Be("A decision has been made");
        plan.Delete.Should().BeTrue();
    }

    [Fact]
    public void AJourneyDoneBeforeItsActivitysTokenArrived_EndsTheActivity_WhenTheTokenArrives()
    {
        var waiting = PrismJourneyStatusPlan.ForStatus([IPhone()], Stored("Deciding your application"), Update(Decided), Now);
        waiting.Messages.Should().BeEmpty();

        var arrived = PrismJourneyStatusPlan.ForActivityToken(IPhone(), waiting.Save, "activity-token", Now);

        Aps(arrived.Messages.Should().ContainSingle().Subject, "activity-token")["event"].Should().Be("end");
        arrived.Delete.Should().BeTrue();
    }

    [Fact]
    public void AnActivitysTokenArriving_SendsItTheLatestStatus()
    {
        var plan = PrismJourneyStatusPlan.ForActivityToken(IPhone(), Stored("Deciding your application"), "activity-token", Now);

        Aps(plan.Messages.Should().ContainSingle().Subject, "activity-token")["event"].Should().Be("update");
        plan.Save!.ActivityToken.Should().Be("activity-token");
    }

    [Fact]
    public void AJourneyDoneWithNothingStarted_SendsNothingToAnIPhone()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([IPhone()], stored: null, Update(Decided), Now);

        plan.Messages.Should().BeEmpty();
        plan.Save.Should().BeNull();
        plan.Delete.Should().BeFalse();
    }

    [Fact]
    public void AnAndroidPhone_IsSentEveryStatus_AsADataMessageItsAppHandles()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([Android()], stored: null, Update(Deciding), Now);

        var message = plan.Messages.Should().ContainSingle().Subject;
        message.Notification.Should().BeNull();
        message.Android.Priority.Should().Be(Priority.High);
        message.Data.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["prismJourneyStatus"] = "1",
            ["key"] = "request-1",
            ["kind"] = "Waiting",
            ["serviceName"] = "Apply for a juggling licence",
            ["status"] = "Deciding your application",
            ["path"] = "/apply-for-a-juggling-licence",
            ["step"] = "2",
            ["steps"] = "3"
        });
        plan.Save.Should().BeNull();
    }

    [Fact]
    public void AStatusWithoutSteps_ShowsNoSteps()
    {
        var plan = PrismJourneyStatusPlan.ForStatus([IPhone()], stored: null, Update(Deciding with { Step = null, Steps = null }), Now);

        Aps(plan.Messages.Single(), "start-token")["content-state"].Should().BeEquivalentTo(new Dictionary<string, object>
        {
            ["status"] = "Deciding your application"
        });
    }

    [Fact]
    public void AnIPhoneWithoutAStartToken_IsSentNothing()
    {
        var device = IPhone();
        device.LiveActivityStartToken = null;

        PrismJourneyStatusPlan.ForStatus([device], stored: null, Update(Deciding), Now).Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://elsewhere.example/phish")]
    [InlineData("//elsewhere.example")]
    [InlineData("javascript:alert(1)")]
    public void AStatusThatWouldOpenAnotherSite_IsRefused(string path)
    {
        var queue = new PrismJourneyStatusQueue(NullLogger<PrismJourneyStatusQueue>.Instance);

        var publish = () => queue.Publish("member@example.com", "contoso.example", Deciding with { Path = path });

        publish.Should().Throw<ArgumentException>().WithMessage("Path must be a path on this site*");
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(1, 0)]
    public void AStepOutsideItsSteps_IsRefused(int step, int steps) =>
        (Deciding with { Step = step, Steps = steps }).Problem().Should().Be("Step must be between 0 and Steps.");

    [Fact]
    public void PublishingAStatus_QueuesItForTheBackgroundSender()
    {
        var queue = new PrismJourneyStatusQueue(NullLogger<PrismJourneyStatusQueue>.Instance);

        queue.Publish("member@example.com", "contoso.example", Deciding);

        queue.Reader.TryRead(out var queued).Should().BeTrue();
        queued.Should().Be(new PrismJourneyStatusUpdate("member@example.com", "contoso.example", Deciding));
    }

    private static PrismJourneyStatusUpdate Update(PrismJourneyStatus status) => new("member@example.com", "contoso.example", status);

    private static PrismDeviceCredentialSchema IPhone() => new()
    {
        PushToken = "fcm-token",
        JourneyStatusPlatform = "ios",
        LiveActivityStartToken = "start-token"
    };

    private static PrismDeviceCredentialSchema Android() => new() { PushToken = "fcm-token", JourneyStatusPlatform = "android" };

    private static PrismJourneyStatusSchema Stored(string status, string? activityToken = null) => new()
    {
        Id = 7,
        UserId = "member@example.com",
        TenantId = "contoso.example",
        JourneyKey = "request-1",
        ServiceName = "Apply for a juggling licence",
        Status = status,
        Path = "/apply-for-a-juggling-licence",
        Kind = "Waiting",
        ActivityToken = activityToken
    };

    /// <summary>The message's aps dictionary, after checking it goes to the device's push token and the given Live Activity token.</summary>
    private static IDictionary<string, object> Aps(Message message, string liveActivityToken)
    {
#pragma warning disable CS0618 // Firebase's Fid replaces Token, which PrismNotificationService also still uses.
        message.Token.Should().Be("fcm-token");
#pragma warning restore CS0618
        message.Apns.LiveActivityToken.Should().Be(liveActivityToken);
        message.Apns.Headers.Should().Contain("apns-priority", "10");
        return message.Apns.Aps.CustomData;
    }
}
