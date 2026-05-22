using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.MockBusinessApp.Services.WorkflowActions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.MockBusinessApp.Services;

public class BusinessAppWorkflowEngine : WorkflowRuntimeEngine
{
    private readonly IWorkflowActionRegistry? _actionRegistry;

    public WorkflowResponseEnvelope AdvanceAsReviewer(string instanceId, string action)
    {
        if (!TryGetInstance(instanceId, out var instance))
        {
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
        }

        var definition = GetDefinition(instance.WorkflowKey);
        if (definition == null)
        {
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");
        }

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState && t.Action == action
                 && string.Equals(t.RequiresRole, "reviewer", StringComparison.OrdinalIgnoreCase));

        if (transition == null)
        {
            return ErrorEnvelope(
                $"Reviewer action '{action}' is not valid from state '{instance.CurrentState}'.",
                "INVALID_TRANSITION");
        }

        var sourceState = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (sourceState == null)
        {
            return ErrorEnvelope(
                $"State '{instance.CurrentState}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var targetState = definition.States.FirstOrDefault(s => s.StateKey == transition.ToState);
        if (targetState == null)
        {
            return ErrorEnvelope(
                $"State '{transition.ToState}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var updated = instance with
        {
            CurrentState = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (ExecuteRegisteredActions(
                updated,
                definition,
                sourceState,
                targetState,
                transition,
                action,
                updated.FieldValues,
                GetOrderedActions(sourceState, transition, targetState)) is { } actionError)
        {
            return actionError;
        }

        SaveInstance(updated);
        Logger.LogInformation(
            "Reviewer advanced instance {Id}: {From} → {To}",
            instanceId,
            instance.CurrentState,
            transition.ToState);

        return BuildEnvelope(updated, definition);
    }

    public override WorkflowResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues)
    {
        if (!TryGetInstance(instanceId, out var instance))
        {
            return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
        }

        if (!string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(instance.UserId, userId, StringComparison.Ordinal))
        {
            return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");
        }

        if (instance.StateVersion != expectedStateVersion)
        {
            return ErrorEnvelope(
                $"State version mismatch: expected {expectedStateVersion}, actual {instance.StateVersion}.",
                "VERSION_MISMATCH");
        }

        var definition = GetDefinition(instance.WorkflowKey);
        if (definition == null)
        {
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");
        }

        if (action.StartsWith("change:", StringComparison.OrdinalIgnoreCase))
        {
            var targetStateKey = action["change:".Length..];
            if (definition.States.All(s => s.StateKey != targetStateKey))
            {
                return ErrorEnvelope($"State '{targetStateKey}' not found in definition.", "STATE_NOT_FOUND");
            }

            var jumped = instance with
            {
                CurrentState = targetStateKey,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            SaveInstance(jumped);
            Logger.LogInformation(
                "Change-link: jumped instance {Id} to state '{State}'",
                instanceId,
                targetStateKey);
            return BuildEnvelope(jumped, definition);
        }

        var transition = definition.Transitions.FirstOrDefault(
            t => t.FromState == instance.CurrentState
                 && t.Action == action
                 && t.RequiresRole == null);

        if (transition == null)
        {
            return ErrorEnvelope(
                $"Action '{action}' is not valid from state '{instance.CurrentState}'.",
                "INVALID_TRANSITION");
        }

        if (ValidateAdvance(instance, definition, fieldValues) is { } validationEnvelope)
        {
            return validationEnvelope;
        }

        var sourceState = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        if (sourceState == null)
        {
            return ErrorEnvelope(
                $"State '{instance.CurrentState}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var targetState = definition.States.FirstOrDefault(s => s.StateKey == transition.ToState);
        if (targetState == null)
        {
            return ErrorEnvelope(
                $"State '{transition.ToState}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var mergedFieldValues = Merge(instance.FieldValues, fieldValues);
        var updated = instance with
        {
            CurrentState = transition.ToState,
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
            instance.CurrentState,
            transition.ToState);

        return BuildEnvelope(updated, definition);
    }

    public BusinessAppWorkflowEngine(
        ILogger<BusinessAppWorkflowEngine> logger,
        IWebHostEnvironment env,
        IWorkflowContentSanitizer sanitizer,
        IWorkflowDefinitionStore? definitionStore = null,
        IWorkflowActionRegistry? actionRegistry = null)
        : base(
            logger,
            definitionStore ?? new FilesystemWorkflowDefinitionStore(Path.Combine(env.ContentRootPath, "workflow-seeds")),
            sanitizer)
    {
        _actionRegistry = actionRegistry;
    }

    protected override WorkflowResponseEnvelope? ValidateAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        Dictionary<string, object?>? fieldValues)
    {
        if (string.Equals(definition.DefinitionKey, "payment-demo", StringComparison.OrdinalIgnoreCase)
            && fieldValues is not null
            && fieldValues.TryGetValue("amount", out var amountValue)
            && decimal.TryParse(amountValue?.ToString(), out var amount)
            && amount <= 0)
        {
            return new WorkflowResponseEnvelope
            {
                InstanceId = instance.InstanceId,
                StateVersion = instance.StateVersion,
                ResponseState = "validation_error",
                CorrelationId = instance.InstanceId,
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Problems =
                [
                    new WorkflowProblem
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
        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            StateVersion = instance.StateVersion,
            ResponseState = "validation_error",
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems =
            [
                new WorkflowProblem
                {
                    FieldKey = "message",
                    Code = "diagnostic-info-required",
                    Message = "Technical support requests should include a version number (e.g. v1.2.3), a URL, or an error reference so our team can help you faster."
                }
            ]
        };
    }

    protected override WorkflowResponseEnvelope? InitializeNewInstance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        string? action)
    {
        if (_actionRegistry is null)
        {
            return null;
        }

        var initialState = definition.States.FirstOrDefault(state => state.StateKey == instance.CurrentState);
        if (initialState == null)
        {
            return ErrorEnvelope(
                $"State '{instance.CurrentState}' not found in definition '{definition.DefinitionKey}'.",
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

    private WorkflowResponseEnvelope? ExecuteRegisteredActions(
        WorkflowInstanceState updatedInstance,
        WorkflowDefinitionFile definition,
        StepDefinition? sourceState,
        StepDefinition targetState,
        WorkflowTransitionFile? transition,
        string? triggerAction,
        IReadOnlyDictionary<string, object?> fieldValues,
        IReadOnlyList<WorkflowActionDefinition> actions)
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

    private static IReadOnlyList<WorkflowActionDefinition> GetOrderedActions(
        StepDefinition sourceState,
        WorkflowTransitionFile transition,
        StepDefinition targetState)
    {
        var actions = new List<WorkflowActionDefinition>();
        AddMatchingActions(actions, sourceState.Metadata?.Actions, "OnExit");
        AddMatchingActions(actions, transition.Metadata?.Actions, "OnTransition");
        AddMatchingActions(actions, targetState.Metadata?.Actions, "OnEntry");
        return actions;
    }

    private static IReadOnlyList<WorkflowActionDefinition> GetStateEntryActions(StepDefinition targetState)
    {
        var actions = new List<WorkflowActionDefinition>();
        AddMatchingActions(actions, targetState.Metadata?.Actions, "OnEntry");
        return actions;
    }

    private static void AddMatchingActions(
        List<WorkflowActionDefinition> destination,
        IReadOnlyList<WorkflowActionDefinition>? candidates,
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
