using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Wayfinder.Umbraco.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Settings for <see cref="ResolveWayfinderSupportSystemOutcomeAction"/>.
/// </summary>
public sealed class ResolveWayfinderSupportSystemOutcomeSettings
{
    [Field(Label = "Invocation id", Description = "The invocationId from the trigger body.", SupportsBindings = true)]
    public string InvocationId { get; set; } = string.Empty;

    [Field(Label = "Outcome key", Description = "One of the capability's declared outcomes.", SortOrder = 1, SupportsBindings = true)]
    public string OutcomeKey { get; set; } = string.Empty;

    [Field(Label = "Result payload (JSON)", Description = "Merged into the instance's field values under the capability's declared Outputs.", SortOrder = 2, SupportsBindings = true)]
    public string? ResultPayload { get; set; }
}

/// <summary>
/// A custom Umbraco Automate action that resolves a Wayfinder support-system invocation in
/// process, calling <see cref="UmbracoProcessManagerEngine.ResolveSupportSystemOutcome"/>
/// directly — TestSite's own copy of the exact pattern
/// <c>Wayfinder.Umbraco.ReferenceApp/ResolveSupportSystemOutcomeAction.cs</c> uses, and for the
/// same reason: Automate's built-in <c>HttpRequestAction</c> SSRF-blocks loopback/private
/// addresses, so an automation running on this same box can't call itself back over HTTP.
/// Wayfinder-specific (needs <see cref="UmbracoProcessManagerEngine"/>), so it lives here rather
/// than in the Wayfinder-free <c>UmbracoPrism.Automate</c> package — see that package's own
/// <c>PrismSendPushNotificationAction</c> for the generic counterpart any Prism host gets out of
/// the box. Auto-discovered by <c>[Action]</c>.
/// </summary>
[Action("wayfinder.resolveSupportSystemOutcome", "Resolve Wayfinder support-system outcome",
    Description = "Resolves a waiting Wayfinder support-system invocation in process.",
    Group = "Wayfinder",
    Icon = "icon-checkbox")]
public sealed class ResolveWayfinderSupportSystemOutcomeAction(
    ActionInfrastructure infrastructure,
    UmbracoProcessManagerEngine engine,
    ILogger<ResolveWayfinderSupportSystemOutcomeAction> logger)
    : ActionBase<ResolveWayfinderSupportSystemOutcomeSettings, ResolveWayfinderSupportSystemOutcomeAction.Output>(infrastructure)
{
    public sealed class Output
    {
        public string ResponseState { get; set; } = string.Empty;
    }

    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ResolveWayfinderSupportSystemOutcomeSettings>();

        if (string.IsNullOrWhiteSpace(settings.InvocationId) || string.IsNullOrWhiteSpace(settings.OutcomeKey))
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException("Both invocationId and outcomeKey are required."),
                StepRunErrorCategory.Validation));
        }

        JsonObject? payload = null;
        if (!string.IsNullOrWhiteSpace(settings.ResultPayload))
        {
            try
            {
                payload = JsonNode.Parse(settings.ResultPayload) as JsonObject;
            }
            catch (JsonException ex)
            {
                return Task.FromResult(ActionResult.Failed(ex, StepRunErrorCategory.Validation));
            }
        }

        var result = engine.ResolveSupportSystemOutcome(settings.InvocationId, settings.OutcomeKey, payload);

        if (result.ResponseState == "error")
        {
            var message = result.Problems.Count > 0 ? result.Problems[0].Message : "Failed to resolve the outcome.";
            var code = result.Problems.Count > 0 ? result.Problems[0].Code : "";

            // An unknown / already-resolved invocation is a safe no-op, not a run failure.
            if (code == "SUPPORT_SYSTEM_INVOCATION_NOT_FOUND")
            {
                logger.LogInformation(
                    "Wayfinder support-system invocation {InvocationId} was already resolved or not found; no-op.",
                    settings.InvocationId);
                return Task.FromResult(Success(new Output { ResponseState = "no-op" }));
            }

            return Task.FromResult(ActionResult.Failed(new InvalidOperationException(message), StepRunErrorCategory.InvalidResponse));
        }

        return Task.FromResult(Success(new Output { ResponseState = result.ResponseState }));
    }
}
