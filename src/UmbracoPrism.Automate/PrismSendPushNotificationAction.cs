using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Automate;

/// <summary>
/// Settings for <see cref="PrismSendPushNotificationAction"/>.
/// </summary>
public sealed class PrismSendPushNotificationSettings
{
    [Field(Label = "User id", Description = "The Prism member to notify — the same id RegisterDeviceTokenAsync stored their push token under.", SupportsBindings = true)]
    public string UserId { get; set; } = string.Empty;

    [Field(Label = "Tenant id", Description = "The Prism tenant the user belongs to.", SortOrder = 1, SupportsBindings = true)]
    public string TenantId { get; set; } = string.Empty;

    [Field(Label = "Title", Description = "The notification's title.", SortOrder = 2, SupportsBindings = true)]
    public string Title { get; set; } = string.Empty;

    [Field(Label = "Body", Description = "The notification's body text.", SortOrder = 3, SupportsBindings = true)]
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// A custom Umbraco Automate action that sends a push notification to one Prism member,
/// calling <see cref="IPrismNotificationService.SendNotificationToUserAsync"/> directly in
/// process. Ships as part of <c>UmbracoPrism.Automate</c> — a host that already installs both
/// this package and Umbraco Automate gets it for free, no bespoke wiring, the same "no
/// duplication, config/attribute discovery only" shape Wayfinder.Umbraco's own
/// <c>ResolveSupportSystemOutcomeAction</c> demonstrates for resolving a support-system
/// invocation. This action carries no Wayfinder dependency at all — any Automate automation, for
/// any reason, can send a Prism member a push notification. Auto-discovered by <c>[Action]</c>,
/// no explicit registration needed.
/// </summary>
[Action("prism.sendPushNotification", "Send Prism push notification",
    Description = "Sends a push notification to one Prism member's registered device(s).",
    Group = "Prism",
    Icon = "icon-bell")]
public sealed class PrismSendPushNotificationAction(
    ActionInfrastructure infrastructure,
    IPrismNotificationService notificationService,
    ILogger<PrismSendPushNotificationAction> logger)
    : ActionBase<PrismSendPushNotificationSettings, PrismSendPushNotificationAction.Output>(infrastructure)
{
    public sealed class Output
    {
        public bool Sent { get; set; }
    }

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PrismSendPushNotificationSettings>();

        if (string.IsNullOrWhiteSpace(settings.UserId) || string.IsNullOrWhiteSpace(settings.TenantId))
        {
            return ActionResult.Failed(
                new ArgumentException("Both userId and tenantId are required."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Title) || string.IsNullOrWhiteSpace(settings.Body))
        {
            return ActionResult.Failed(
                new ArgumentException("Both title and body are required."),
                StepRunErrorCategory.Validation);
        }

        var sent = await notificationService.SendNotificationToUserAsync(
            settings.UserId, settings.TenantId, settings.Title, settings.Body, cancellationToken);

        if (sent)
        {
            logger.LogInformation(
                "Prism push notification sent to user {UserId} in tenant {TenantId}.",
                settings.UserId, settings.TenantId);
        }
        else
        {
            // Not a failure: a citizen who never installed the mobile app, or never signed in as
            // a member, has no registered device to send to — a legitimate outcome, not an
            // automation error. But it must be visible, unlike before: this action used to log
            // "sent" and report Success unconditionally, regardless of whether the underlying
            // send actually found a device.
            logger.LogInformation(
                "No registered device found for user {UserId} in tenant {TenantId} — push notification not sent.",
                settings.UserId, settings.TenantId);
        }

        return Success(new Output { Sent = sent });
    }
}
