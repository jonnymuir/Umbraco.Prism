using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Settings for <see cref="ResolveWayfinderInstanceOwnerAction"/>.
/// </summary>
public sealed class ResolveWayfinderInstanceOwnerSettings
{
    [Field(Label = "Instance id", Description = "The instanceId from the trigger body.", SupportsBindings = true)]
    public string InstanceId { get; set; } = string.Empty;
}

/// <summary>
/// A custom Umbraco Automate action that looks up which member owns a Wayfinder service request
/// instance — the webhook envelope a support-system-call action posts
/// (<c>{ invocationId, instanceId, supportSystemKey, capabilityKey, inputs{…} }</c>, see
/// docs/guides/support-systems.md in the Wayfinder repo) carries no member identity at all, only
/// blueprint field values, so an automation that needs to notify the applicant back has nowhere
/// else to get it from. Reads <see cref="ServiceRequest.UserId"/>/<see cref="ServiceRequest.TenantId"/>
/// directly off the stored instance, unrestricted by any <c>ActorProfile</c> access check — this
/// runs as trusted, server-side automation, not on a human's own request.
/// <para/>
/// <see cref="ServiceRequest.UserId"/> only matches a <c>prismDeviceCredentials</c> row (and so
/// only reaches a real device) once the applicant has signed in as a Prism member — an anonymous
/// visitor's own correlation-cookie id was never used to register a push token in the first
/// place. <see cref="UmbracoPrism.Automate.PrismSendPushNotificationAction"/>'s own send is a
/// silent no-op for a user with no registered token, so this is safe to chain regardless, it just
/// won't produce a visible notification for an application never claimed by a signed-in member.
/// </para>
/// Wayfinder-specific (needs <see cref="IServiceRequestStore"/>), so it lives here rather than in
/// the Wayfinder-free <c>UmbracoPrism.Automate</c> package. Auto-discovered by <c>[Action]</c>.
/// </summary>
[Action("wayfinder.resolveInstanceOwner", "Resolve Wayfinder instance owner",
    Description = "Looks up which Prism member owns a Wayfinder service request instance.",
    Group = "Wayfinder",
    Icon = "icon-user")]
public sealed class ResolveWayfinderInstanceOwnerAction(
    ActionInfrastructure infrastructure,
    IServiceRequestStore instanceStore,
    ILogger<ResolveWayfinderInstanceOwnerAction> logger)
    : ActionBase<ResolveWayfinderInstanceOwnerSettings, ResolveWayfinderInstanceOwnerAction.Output>(infrastructure)
{
    public sealed class Output
    {
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
    }

    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ResolveWayfinderInstanceOwnerSettings>();

        if (string.IsNullOrWhiteSpace(settings.InstanceId))
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException("instanceId is required."),
                StepRunErrorCategory.Validation));
        }

        if (!instanceStore.TryGet(settings.InstanceId, out var instance))
        {
            logger.LogWarning("Wayfinder instance {InstanceId} was not found; cannot resolve its owner.", settings.InstanceId);
            return Task.FromResult(ActionResult.Failed(
                new InvalidOperationException($"Instance '{settings.InstanceId}' was not found."),
                StepRunErrorCategory.InvalidResponse));
        }

        return Task.FromResult(Success(new Output { UserId = instance.UserId, TenantId = instance.TenantId }));
    }
}
