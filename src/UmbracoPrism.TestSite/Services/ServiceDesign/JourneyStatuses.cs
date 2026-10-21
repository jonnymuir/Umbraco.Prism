using Umbraco.Cms.Core.Events;
using UmbracoPrism.Core.Services.JourneyStatus;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite.Services.ServiceDesign;

/// <summary>
/// Shows a signed-in member where their journeys have got to, on their phone's lock screen, as
/// their requests move on (Design/mobile-journey-status.md). Only the stages listed here have a
/// status. Each line says what kind of thing is happening, never the outcome: the decision itself
/// reaches the member through its own push notification and the page behind sign-in.
/// </summary>
public sealed class JourneyStatuses(IServiceRequestStore requests, IPrismJourneyStatusService journeyStatus)
    : INotificationHandler<ServiceRequestTransitionedNotification>
{
    private sealed record Line(string ServiceName, string Path, string Status, PrismJourneyStatusKind Kind);

    private static readonly Dictionary<(string Blueprint, string Stage), Line> Lines = new()
    {
        [(TestSiteSeedContract.JugglingLicenceBlueprintSlug, "processing-application")] = new(
            TestSiteSeedContract.JugglingLicencePageName, TestSiteSeedContract.JugglingLicencePageUrl,
            "Deciding your application", PrismJourneyStatusKind.Waiting),
        [(TestSiteSeedContract.JugglingLicenceBlueprintSlug, "confirmation")] = new(
            TestSiteSeedContract.JugglingLicencePageName, TestSiteSeedContract.JugglingLicencePageUrl,
            "A decision has been made", PrismJourneyStatusKind.Done),
    };

    public void Handle(ServiceRequestTransitionedNotification notification)
    {
        var move = notification.Transition;
        if (move.ToStageKey is not { } stage
            || !requests.TryGet(move.InstanceId, out var request)
            || !request.IsAuthenticated
            || !Lines.TryGetValue((request.BlueprintKey, stage), out var line))
        {
            return;
        }

        journeyStatus.Publish(request.UserId, request.TenantId, new PrismJourneyStatus(
            request.InstanceId, line.ServiceName, line.Status, Step: null, Steps: null, line.Path, line.Kind));
    }
}
