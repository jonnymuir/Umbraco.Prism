using AwesomeAssertions;
using UmbracoPrism.Core.Services.JourneyStatus;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Models;
using Wayfinder.Engine.Stores;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// The reference site shows a signed-in member's juggling licence application on their lock
/// screen while it is decided, and ends it once the decision is made.
/// </summary>
public class JourneyStatusesTests
{
    private readonly InMemoryServiceRequestStore _requests = new();
    private readonly RecordingJourneyStatus _published = new();

    [Fact]
    public void ASignedInApplicant_SeesTheirApplicationBeingDecided()
    {
        Save(signedIn: true);

        Move("check-answers", "processing-application");

        _published.Statuses.Should().ContainSingle().Which.Should().Be((
            "demo@prism.local", "localhost",
            new PrismJourneyStatus("request-1", "Apply for a juggling licence", "Deciding your application", null, null,
                "/apply-for-a-juggling-licence", PrismJourneyStatusKind.Waiting)));
    }

    [Fact]
    public void TheDecision_EndsTheStatus_WithoutSayingWhatItWas()
    {
        Save(signedIn: true);

        Move("application-decided", "confirmation");

        var status = _published.Statuses.Should().ContainSingle().Subject.Status;
        status.Status.Should().Be("A decision has been made");
        status.Kind.Should().Be(PrismJourneyStatusKind.Done);
    }

    [Fact]
    public void AVisitorWhoIsNotSignedIn_HasNoStatus()
    {
        Save(signedIn: false);

        Move("check-answers", "processing-application");

        _published.Statuses.Should().BeEmpty();
    }

    [Fact]
    public void AStageWithoutAStatus_ChangesNothing()
    {
        Save(signedIn: true);

        Move("eligibility", "applicant-details");

        _published.Statuses.Should().BeEmpty();
    }

    private void Save(bool signedIn) => _requests.Save(new ServiceRequest
    {
        InstanceId = "request-1",
        BlueprintKey = "apply-for-a-juggling-licence",
        UserId = "demo@prism.local",
        TenantId = "localhost",
        IsAuthenticated = signedIn
    });

    private void Move(string from, string to) =>
        new JourneyStatuses(_requests, _published).Handle(new ServiceRequestTransitionedNotification(new AuditEvent
        {
            EventId = "event-1",
            InstanceId = "request-1",
            EventType = AuditEventType.Transition,
            Actor = "demo@prism.local",
            FromStageKey = from,
            ToStageKey = to,
            Severity = AuditEventSeverity.Info,
            OccurredAt = DateTimeOffset.UtcNow
        }));

    private sealed class RecordingJourneyStatus : IPrismJourneyStatusService
    {
        public List<(string UserId, string TenantId, PrismJourneyStatus Status)> Statuses { get; } = [];

        public void Publish(string userId, string tenantId, PrismJourneyStatus status) => Statuses.Add((userId, tenantId, status));
    }
}
