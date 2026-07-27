using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.ServiceDesign;
using UmbracoPrism.MockBusinessApp.Services.MoneyModeller;
using UmbracoPrism.MockBusinessApp.Services.Actions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.ProcessManager.Abstractions;
using UmbracoPrism.ProcessManager.Models;
using UmbracoPrism.ProcessManager.Services;
using UmbracoPrism.ProcessManager.Stores;

namespace UmbracoPrism.MockBusinessApp.Services;

public class BusinessAppProcessManager : ProcessManagerEngine
{
    private readonly IWorkflowActionRegistry? _actionRegistry;
    private readonly MemberRecordService? _memberRecords;

    public ServiceRequestResponseEnvelope AdvanceAsReviewer(string instanceId, string action)
    {
        if (!TryGetInstance(instanceId, out var instance))
        {
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
        }

        return Advance(
            instanceId,
            instance.TenantId,
            instance.UserId,
            ReferenceQueues.BusinessUserProfile(),
            action,
            instance.StateVersion,
            fieldValues: null);
    }

    public override ServiceRequestResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues) =>
        Advance(
            instanceId,
            tenantId,
            userId,
            ActorProfile.UnrestrictedOwner,
            action,
            expectedStateVersion,
            fieldValues);

    public override ServiceRequestResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        ActorProfile accessProfile,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues)
    {
        if (!TryGetInstance(instanceId, out var instance))
        {
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
        }

        if (!CanAccessInstance(instance, tenantId, userId, accessProfile))
        {
            return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");
        }

        if (instance.StateVersion != expectedStateVersion)
        {
            return ErrorEnvelope(
                $"State version mismatch: expected {expectedStateVersion}, actual {instance.StateVersion}.",
                "VERSION_MISMATCH");
        }

        var definition = GetDefinition(instance.BlueprintKey);
        if (definition == null)
        {
            return ErrorEnvelope($"Workflow '{instance.BlueprintKey}' not found.", "DEFINITION_NOT_FOUND");
        }

        // Delegate to the base implementation, same as the gateway case below — this override
        // exists to run MockBusinessApp-specific registered actions around a plain single-cursor
        // stage transition, not to reimplement "change:" jump handling. This branch used to have
        // its own copy of the (older, cursor-blind) base logic; that copy silently rotted out of
        // sync when the base class was fixed to also move the right cursor
        // (ProcessManagerEngine.Advance), making every "Change" link on a summary-list a no-op in
        // the real app despite unit tests against the base class passing (confirmed live).
        if (action.StartsWith("change:", StringComparison.OrdinalIgnoreCase))
        {
            return base.Advance(instanceId, tenantId, userId, accessProfile, action, expectedStateVersion, fieldValues);
        }

        var visibleWorkItem = FindAccessibleWorkItems(instance, definition, accessProfile)
            .FirstOrDefault(item => item.AvailableActions.Any(candidate =>
                string.Equals(candidate.ActionKey, action, StringComparison.Ordinal)));

        if (visibleWorkItem is null)
        {
            return ErrorEnvelope(
                $"Action '{action}' is not valid from the current queue view.",
                "INVALID_TRANSITION");
        }

        var transition = GetOutgoingTransitions(definition, visibleWorkItem.StageKey).FirstOrDefault(
            t => t.FromState == visibleWorkItem.StageKey
                 && t.Action == action);

        if (transition == null)
        {
            return ErrorEnvelope(
                $"Action '{action}' is not valid from state '{visibleWorkItem.StageKey}'.",
                "INVALID_TRANSITION");
        }

        if (ValidateAdvance(instance, definition, fieldValues) is { } validationEnvelope)
        {
            return validationEnvelope;
        }

        var nextGateway = FindGateway(definition, transition.ToState);
        if (nextGateway != null)
        {
            return base.Advance(instanceId, tenantId, userId, accessProfile, action, expectedStateVersion, fieldValues);
        }

        var sourceState = definition.Stages.FirstOrDefault(s => s.StageKey == visibleWorkItem.StageKey);
        if (sourceState == null)
        {
            return ErrorEnvelope(
                $"State '{visibleWorkItem.StageKey}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var targetState = definition.Stages.FirstOrDefault(s => s.StageKey == transition.ToState);
        if (targetState == null)
        {
            return ErrorEnvelope(
                $"State '{transition.ToState}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var mergedFieldValues = Merge(instance.FieldValues, fieldValues);
        var updated = instance with
        {
            CurrentStage = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = mergedFieldValues
        };

        if (ExecuteRegisteredActions(
                updated,
                definition,
                sourceState,
                targetState,
                transition,
                action,
                mergedFieldValues,
                GetOrderedActions(sourceState, transition, targetState)) is { } actionError)
        {
            return actionError;
        }

        SaveInstance(updated);
        Logger.LogInformation(
            "Advanced instance {Id}: {From} → {To}",
            instanceId,
            visibleWorkItem.StageKey,
            transition.ToState);

        return BuildEnvelope(updated, definition, accessProfile, allowFallbackWhenHidden: true);
    }

    public BusinessAppProcessManager(
        ILogger<BusinessAppProcessManager> logger,
        IWebHostEnvironment env,
        IServiceContentSanitizer sanitizer,
        IServiceBlueprintStore? definitionStore = null,
        IWorkflowActionRegistry? actionRegistry = null,
        MemberRecordService? memberRecords = null)
        : base(
            logger,
            definitionStore ?? new FilesystemServiceBlueprintStore(Path.Combine(env.ContentRootPath, "service-blueprints")),
            sanitizer)
    {
        _actionRegistry = actionRegistry;
        _memberRecords = memberRecords;
    }

    /// <summary>
    /// Supplies the money-modeller definition's single service-sourced calculation input:
    /// the member record from the (mock) scheme administration system. All maths, display
    /// formatting and visibility live in the definition's own calculations block and
    /// components — this is the entire host-side involvement.
    /// </summary>
    protected override IReadOnlyDictionary<string, object?>? ResolveServiceInputs(
        ServiceRequest instance,
        ServiceBlueprint definition,
        StageDefinition state)
    {
        if (_memberRecords is null
            || !string.Equals(definition.DefinitionKey, "money-modeller", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var member = _memberRecords.GetForUser(instance.UserId);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["member"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = member.Name,
                ["active"] = member.Active,
                ["age"] = (decimal)member.Age,
                ["salary"] = member.Salary,
                ["accruedPension"] = member.AccruedPension,
                ["accruedLump"] = member.AccruedLump,
                ["dcPot"] = member.DcPot
            }
        };
    }

    protected override ServiceRequestResponseEnvelope? ValidateAdvance(
        ServiceRequest instance,
        ServiceBlueprint definition,
        Dictionary<string, object?>? fieldValues)
    {
        if (string.Equals(definition.DefinitionKey, "payment-demo", StringComparison.OrdinalIgnoreCase)
            && fieldValues is not null
            && fieldValues.TryGetValue("amount", out var amountValue)
            && decimal.TryParse(amountValue?.ToString(), out var amount)
            && amount <= 0)
        {
            return new ServiceRequestResponseEnvelope
            {
                InstanceId = instance.InstanceId,
                StateVersion = instance.StateVersion,
                ResponseState = "validation_error",
                CorrelationId = instance.InstanceId,
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Problems =
                [
                    new ServiceRequestProblem
                    {
                        FieldKey = "amount",
                        Code = "minimum_amount_required",
                        Message = "Amount (£) must be at least 0.01."
                    }
                ]
            };
        }

        if (fieldValues == null
            || !fieldValues.TryGetValue("enquiry-type", out var enquiryTypeObj)
            || enquiryTypeObj?.ToString() != "Technical support"
            || !fieldValues.TryGetValue("message", out var messageObj))
        {
            return null;
        }

        var message = messageObj?.ToString() ?? string.Empty;
        var hasVersionNumber = Regex.IsMatch(message, @"\bv?\d+\.\d+", RegexOptions.IgnoreCase);
        var hasUrl = Regex.IsMatch(message, @"https?://\S+", RegexOptions.IgnoreCase);
        var hasErrorRef = Regex.IsMatch(message, @"\b(ERR[-_]\w+|0x[0-9A-Fa-f]+|#\d{3,})\b");

        if (hasVersionNumber || hasUrl || hasErrorRef)
        {
            return null;
        }
        return new ServiceRequestResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            StateVersion = instance.StateVersion,
            ResponseState = "validation_error",
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems =
            [
                new ServiceRequestProblem
                {
                    FieldKey = "message",
                    Code = "diagnostic-info-required",
                    Message = "Technical support requests should include a version number (e.g. v1.2.3), a URL, or an error reference so our team can help you faster."
                }
            ]
        };
    }

    protected override ServiceRequestResponseEnvelope? InitializeNewInstance(
        ServiceRequest instance,
        ServiceBlueprint definition,
        string? action)
    {
        if (_actionRegistry is null)
        {
            return null;
        }

        var initialState = definition.Stages.FirstOrDefault(state => state.StageKey == instance.CurrentStage);
        if (initialState == null)
        {
            return ErrorEnvelope(
                $"State '{instance.CurrentStage}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        return ExecuteRegisteredActions(
            instance,
            definition,
            sourceState: null,
            targetState: initialState,
            transition: null,
            triggerAction: action,
            fieldValues: instance.FieldValues,
            actions: GetStateEntryActions(initialState));
    }

    private ServiceRequestResponseEnvelope? ExecuteRegisteredActions(
        ServiceRequest updatedInstance,
        ServiceBlueprint definition,
        StageDefinition? sourceState,
        StageDefinition targetState,
        RouteFile? transition,
        string? triggerAction,
        IReadOnlyDictionary<string, object?> fieldValues,
        IReadOnlyList<ActionDefinition> actions)
    {
        if (_actionRegistry is null)
        {
            return null;
        }
        if (actions.Count == 0)
        {
            return null;
        }

        var context = new WorkflowActionExecutionContext
        {
            Definition = definition,
            Instance = updatedInstance,
            SourceState = sourceState,
            TargetState = targetState,
            Transition = transition,
            TriggerAction = triggerAction,
            FieldValues = fieldValues
        };

        foreach (var action in actions)
        {
            var handler = _actionRegistry.Resolve(action.Type);
            if (handler is null)
            {
                return ErrorEnvelope(
                    $"No workflow action handler is registered for '{action.Type}'.",
                    "ACTION_HANDLER_NOT_FOUND");
            }

            var result = handler.ExecuteAsync(action, context, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Succeeded)
            {
                return ErrorEnvelope(
                    result.ErrorMessage ?? $"Workflow action '{action.Type}' failed.",
                    result.ErrorCode ?? "ACTION_EXECUTION_FAILED");
            }

            Logger.LogInformation(
                "Executed workflow action {ActionType} for instance {InstanceId}: {Summary}",
                action.Type,
                updatedInstance.InstanceId,
                result.Summary ?? "(no summary)");
        }

        return null;
    }

    private static IReadOnlyList<ActionDefinition> GetOrderedActions(
        StageDefinition sourceState,
        RouteFile transition,
        StageDefinition targetState)
    {
        var actions = new List<ActionDefinition>();
        AddMatchingActions(actions, sourceState.Metadata?.Actions, "OnExit");
        AddMatchingActions(actions, transition.Metadata?.Actions, "OnTransition");
        AddMatchingActions(actions, targetState.Metadata?.Actions, "OnEntry");
        return actions;
    }

    private static IReadOnlyList<ActionDefinition> GetStateEntryActions(StageDefinition targetState)
    {
        var actions = new List<ActionDefinition>();
        AddMatchingActions(actions, targetState.Metadata?.Actions, "OnEntry");
        return actions;
    }

    private static void AddMatchingActions(
        List<ActionDefinition> destination,
        IReadOnlyList<ActionDefinition>? candidates,
        string expectedTiming)
    {
        if (candidates is null)
        {
            return;
        }

        destination.AddRange(candidates.Where(action =>
            string.Equals(action.Timing, expectedTiming, StringComparison.OrdinalIgnoreCase)));
    }

    private static Dictionary<string, object?> Merge(
        Dictionary<string, object?> existing,
        Dictionary<string, object?>? updates)
    {
        if (updates == null || updates.Count == 0)
        {
            return new Dictionary<string, object?>(existing, StringComparer.Ordinal);
        }

        var merged = new Dictionary<string, object?>(existing, StringComparer.Ordinal);
        foreach (var pair in updates)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }
}
