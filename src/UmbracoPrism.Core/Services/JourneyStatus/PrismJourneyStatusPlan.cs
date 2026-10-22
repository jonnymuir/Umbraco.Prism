using FirebaseAdmin.Messaging;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services.JourneyStatus;

/// <summary>
/// What showing a status takes: the messages to send, and what becomes of the journey's stored
/// status. An iOS Live Activity is started with the device's start token, then reached through
/// its own token, which the device sends once the activity is running. Until it does, the latest
/// status is stored and goes with that token when it arrives.
/// </summary>
internal sealed record PrismJourneyStatusPlan(IReadOnlyList<Message> Messages, PrismJourneyStatusSchema? Save, bool Delete)
{
    public const string Ios = "ios";
    public const string Android = "android";

    private static readonly PrismJourneyStatusPlan Nothing = new([], null, false);

    /// <summary>A newly published status, for the member's devices that show statuses.</summary>
    public static PrismJourneyStatusPlan ForStatus(
        IReadOnlyList<PrismDeviceCredentialSchema> devices,
        PrismJourneyStatusSchema? stored,
        PrismJourneyStatusUpdate update,
        DateTimeOffset now)
    {
        var messages = devices
            .Where(device => device.JourneyStatusPlatform == Android && device.PushToken is not null)
            .Select(device => PrismJourneyStatusMessages.Android(device.PushToken!, update.Status))
            .ToList();

        var iPhone = devices.FirstOrDefault(device => device.JourneyStatusPlatform == Ios && device.PushToken is not null);
        if (iPhone is null)
        {
            return new PrismJourneyStatusPlan(messages, null, false);
        }

        var status = Stored(update, stored, now);
        var ios = update.Status.Kind == PrismJourneyStatusKind.Waiting
            ? Waiting(iPhone, stored, status, now)
            : Done(iPhone, stored, status, now);
        return ios with { Messages = [.. messages, .. ios.Messages] };
    }

    /// <summary>A running Live Activity's own token, sent by the device once the activity has started.</summary>
    public static PrismJourneyStatusPlan ForActivityToken(
        PrismDeviceCredentialSchema? iPhone,
        PrismJourneyStatusSchema? stored,
        string activityToken,
        DateTimeOffset now)
    {
        if (iPhone?.PushToken is not { } pushToken || stored is null)
        {
            return Nothing;
        }

        if (stored.Kind == nameof(PrismJourneyStatusKind.Done))
        {
            return new([PrismJourneyStatusMessages.IosEnd(pushToken, activityToken, stored, now)], null, true);
        }

        stored.ActivityToken = activityToken;
        stored.UpdatedAt = now.UtcDateTime;
        return new([PrismJourneyStatusMessages.IosUpdate(pushToken, activityToken, stored, now)], stored, false);
    }

    private static PrismJourneyStatusPlan Waiting(
        PrismDeviceCredentialSchema iPhone, PrismJourneyStatusSchema? stored, PrismJourneyStatusSchema status, DateTimeOffset now)
    {
        if (status.ActivityToken is { } activityToken)
        {
            return new([PrismJourneyStatusMessages.IosUpdate(iPhone.PushToken!, activityToken, status, now)], status, false);
        }

        // Started already and its token is on its way: the token's arrival sends this status.
        if (stored is not null)
        {
            return new([], status, false);
        }

        return iPhone.LiveActivityStartToken is { } startToken
            ? new([PrismJourneyStatusMessages.IosStart(iPhone.PushToken!, startToken, status, now)], status, false)
            : Nothing;
    }

    private static PrismJourneyStatusPlan Done(
        PrismDeviceCredentialSchema iPhone, PrismJourneyStatusSchema? stored, PrismJourneyStatusSchema status, DateTimeOffset now)
    {
        if (status.ActivityToken is { } activityToken)
        {
            return new([PrismJourneyStatusMessages.IosEnd(iPhone.PushToken!, activityToken, status, now)], null, true);
        }

        // Nothing was started, so there is nothing to end; or it has started and its token's
        // arrival ends it.
        return stored is null ? Nothing : new([], status, false);
    }

    private static PrismJourneyStatusSchema Stored(PrismJourneyStatusUpdate update, PrismJourneyStatusSchema? stored, DateTimeOffset now) => new()
    {
        Id = stored?.Id ?? 0,
        UserId = update.UserId,
        TenantId = update.TenantId,
        JourneyKey = update.Status.Key,
        ServiceName = update.Status.ServiceName,
        Status = update.Status.Status,
        Step = update.Status.Step,
        Steps = update.Status.Steps,
        Path = update.Status.Path,
        Kind = update.Status.Kind.ToString(),
        ActivityToken = stored?.ActivityToken,
        UpdatedAt = now.UtcDateTime
    };
}
