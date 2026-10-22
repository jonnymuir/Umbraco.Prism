using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Logging;
using UmbracoPrism.Core.Persistence;

namespace UmbracoPrism.Core.Services.JourneyStatus;

/// <summary>
/// Carries out a <see cref="PrismJourneyStatusPlan"/>: reads the member's devices and the journey's
/// stored status, sends the messages through Firebase and saves what the plan says. Also records
/// which devices show statuses, as the apps register them.
/// </summary>
public sealed class PrismJourneyStatusDelivery(
    IUmbracoDatabaseFactory databaseFactory,
    PrismFirebase firebase,
    TimeProvider time,
    ILogger<PrismJourneyStatusDelivery> logger)
{
    private const string DevicesWhere = "WHERE UserId = @0 AND TenantId = @1 AND JourneyStatusPlatform IS NOT NULL AND PushToken IS NOT NULL";
    private const string StatusWhere = "WHERE UserId = @0 AND TenantId = @1 AND JourneyKey = @2";

    internal async Task DeliverAsync(PrismJourneyStatusUpdate update, CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        var devices = db.Fetch<PrismDeviceCredentialSchema>(DevicesWhere, update.UserId, update.TenantId);
        if (devices.Count == 0)
        {
            logger.LogDebug("No device shows journey statuses for this member; journey {Key} skipped.", LogScrub.Line(update.Status.Key));
            return;
        }

        var stored = db.FirstOrDefault<PrismJourneyStatusSchema>(StatusWhere, update.UserId, update.TenantId, update.Status.Key);
        var plan = PrismJourneyStatusPlan.ForStatus(devices, stored, update, time.GetUtcNow());
        await CarryOutAsync(db, plan, stored, update.Status.Key, cancellationToken);
    }

    /// <summary>A Live Activity's own token, from the member's device that is running it.</summary>
    public async Task ActivityStartedAsync(PrismJourneyStatusMember member, string journeyKey, string activityToken, CancellationToken cancellationToken)
    {
        using var db = databaseFactory.CreateDatabase();
        var iPhone = db.Fetch<PrismDeviceCredentialSchema>(DevicesWhere, member.UserId, member.TenantId)
            .FirstOrDefault(device => device.JourneyStatusPlatform == PrismJourneyStatusPlan.Ios);
        var stored = db.FirstOrDefault<PrismJourneyStatusSchema>(StatusWhere, member.UserId, member.TenantId, journeyKey);
        var plan = PrismJourneyStatusPlan.ForActivityToken(iPhone, stored, activityToken, time.GetUtcNow());
        await CarryOutAsync(db, plan, stored, journeyKey, cancellationToken);
    }

    /// <summary>
    /// Marks the member's app on this tenant as one that shows statuses. False when the app has not
    /// registered for push notifications yet, which makes the device's row; it registers again later.
    /// Signing out clears this with the push token (<see cref="PrismNotificationService.UnregisterDeviceTokenAsync"/>).
    /// </summary>
    public bool RegisterDevice(PrismJourneyStatusMember member, string platform, string? startToken)
    {
        using var db = databaseFactory.CreateDatabase();
        return db.Execute(
            "UPDATE prismDeviceCredentials SET JourneyStatusPlatform = @0, LiveActivityStartToken = @1 WHERE UserId = @2 AND TenantId = @3 AND PushToken IS NOT NULL",
            platform, (object?)startToken ?? DBNull.Value, member.UserId, member.TenantId) > 0;
    }

    private async Task CarryOutAsync(
        IUmbracoDatabase db, PrismJourneyStatusPlan plan, PrismJourneyStatusSchema? stored, string journeyKey, CancellationToken cancellationToken)
    {
        await SendAsync(plan.Messages, journeyKey, cancellationToken);

        if (plan.Delete && stored is not null)
        {
            db.Delete(stored);
        }
        else if (plan.Save is { Id: 0 } added)
        {
            db.Insert(added);
        }
        else if (plan.Save is { } changed)
        {
            db.Update(changed);
        }
    }

    private async Task SendAsync(IReadOnlyList<Message> messages, string journeyKey, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        if (firebase.Messaging is not { } messaging)
        {
            logger.LogWarning("Firebase is not configured, so journey {Key}'s status was not sent.", LogScrub.Line(journeyKey));
            return;
        }

        var response = await messaging.SendEachAsync(messages, cancellationToken);
        foreach (var failure in response.Responses.Where(result => !result.IsSuccess))
        {
            logger.LogWarning(
                "A journey status for journey {Key} was refused: {Code} {Reason}",
                LogScrub.Line(journeyKey), failure.Exception?.MessagingErrorCode, failure.Exception?.Message);
        }
    }
}
