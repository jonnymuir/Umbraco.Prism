using System.Text.Json;
using System.Text.Json.Nodes;
using UmbracoPrism.Shared.Models.Workflow.Calculations;
using UmbracoPrism.Shared.Services.Calculations;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>
/// Generic in-memory runtime engine that executes Prism workflow definitions.
/// </summary>
public class WorkflowRuntimeEngine : IWorkflowRuntimeEngine
{
    private readonly IWorkflowContentSanitizer _sanitizer;
    private readonly Dictionary<string, WorkflowDefinitionFile> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly Func<WorkflowInstanceState, WorkflowDefinitionFile, StepDefinition, IReadOnlyDictionary<string, object?>?>? _serviceInputsResolver;

    public WorkflowRuntimeEngine(
        ILogger logger,
        IWorkflowDefinitionStore definitionStore,
        IWorkflowContentSanitizer sanitizer,
        Func<WorkflowInstanceState, WorkflowDefinitionFile, StepDefinition, IReadOnlyDictionary<string, object?>?>? serviceInputsResolver = null,
        IWorkflowInstanceStore? instanceStore = null)
    {
        Logger = logger;
        _sanitizer = sanitizer;
        _serviceInputsResolver = serviceInputsResolver;
        _instanceStore = instanceStore ?? new InMemoryWorkflowInstanceStore();

        foreach (var (lookupKey, definition) in definitionStore.LoadDefinitions(logger))
        {
            var runtimeLookupKey = !string.IsNullOrWhiteSpace(lookupKey)
                ? lookupKey
                : definition.DefinitionKey;

            if (!string.IsNullOrWhiteSpace(runtimeLookupKey))
            {
                _definitions[runtimeLookupKey] = definition;
            }
        }

        Logger.LogInformation("Workflow runtime ready: {Defs} definition(s).", _definitions.Count);
    }

    protected ILogger Logger { get; }

    public WorkflowResponseEnvelope GetCurrent(
        string workflowKey,
        string tenantId,
        string userId,
        string? instanceId = null,
        string? action = null) =>
        GetCurrent(
            workflowKey,
            tenantId,
            userId,
            WorkflowAccessProfile.UnrestrictedOwner,
            instanceId,
            action);

    public WorkflowResponseEnvelope GetCurrent(
        string workflowKey,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile,
        string? instanceId = null,
        string? action = null)
    {
        if (!_definitions.TryGetValue(workflowKey, out var definition))
        {
            Logger.LogWarning("Workflow definition not found: {Key}", workflowKey);
            return ErrorEnvelope(
                $"Workflow '{workflowKey}' is not registered with this application.",
                "DEFINITION_NOT_FOUND");
        }

        if (!string.IsNullOrEmpty(instanceId))
        {
            if (!_instanceStore.TryGet(instanceId, out var specificInstance))
            {
                return ErrorEnvelope($"Workflow instance '{instanceId}' not found.", "INSTANCE_NOT_FOUND");
            }

            if (!CanAccessInstance(specificInstance, tenantId, userId, accessProfile))
            {
                return ErrorEnvelope("Access denied to this workflow instance.", "ACCESS_DENIED");
            }

            Logger.LogInformation("Resuming specific instance {Id}", instanceId);
            return BuildEnvelope(specificInstance, definition, accessProfile, false);
        }

        var existingInstance = FindLatestInstance(tenantId, userId, workflowKey);

        if (!CanStartInitialState(definition, accessProfile))
        {
            return ErrorEnvelope("Access denied to start this workflow queue.", "ACCESS_DENIED");
        }

        if (string.Equals(action, "start-new", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                accessProfile,
                action,
                "Created new workflow instance {Id} for key={Key} (action=start-new)",
                workflowKey);
        }

        if (string.Equals(action, "resume", StringComparison.OrdinalIgnoreCase))
        {
            if (existingInstance is not null)
            {
                Logger.LogInformation("Resuming existing instance {Id} (action=resume)", existingInstance.InstanceId);
                return BuildEnvelope(existingInstance, definition, accessProfile, false);
            }

            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                accessProfile,
                action,
                "Created workflow instance {Id} for key={Key} (action=resume, no existing)",
                workflowKey);
        }

        var policy = definition.InstancePolicy;

        if (string.Equals(policy, "multiple", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                accessProfile,
                action,
                "Created new workflow instance {Id} for key={Key} (policy=multiple)",
                workflowKey);
        }

        if (string.Equals(policy, "prompt", StringComparison.OrdinalIgnoreCase))
        {
            if (existingInstance is not null)
            {
                var currentState = definition.States.FirstOrDefault(s => s.StateKey == existingInstance.CurrentState);

                if (!IsTerminalInstance(existingInstance, definition))
                {
                    Logger.LogInformation(
                        "Active instance {Id} exists for key={Key}; returning instance_picker",
                        existingInstance.InstanceId,
                        workflowKey);

                    return new WorkflowResponseEnvelope
                    {
                        InstanceId = existingInstance.InstanceId,
                        ResponseState = "instance_picker",
                        StateVersion = existingInstance.StateVersion,
                        CorrelationId = existingInstance.InstanceId,
                        ServerTimeUtc = DateTimeOffset.UtcNow,
                        InstancePolicy = "prompt",
                        Render = new StepContent
                        {
                            StepType = currentState?.Components.InferStepType() ?? "question",
                            StateDisplayName = currentState?.DisplayName ?? definition.DisplayName,
                            Components = Array.Empty<PrismComponentRenderPayload>(),
                            AvailableActions = Array.Empty<WorkflowAction>()
                        }
                    };
                }
            }

            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                accessProfile,
                action,
                "Created workflow instance {Id} for key={Key} (policy=prompt, no active)",
                workflowKey);
        }

        if (existingInstance is null)
        {
            return CreateAndRegisterNewInstance(
                workflowKey,
                tenantId,
                userId,
                definition,
                accessProfile,
                action,
                "Created workflow instance {Id} for key={Key} tenant={Tenant}",
                workflowKey,
                tenantId);
        }

        // "single" means at most one instance per user for this workflow, full stop — once it
        // reaches a terminal state it keeps being shown on every subsequent visit (the community
        // enquiry demo depends on this: a member returning to the page sees "Thank you", not a
        // silently-reset blank form). PrismWorkflowPageController's PRG redirect after a POST
        // relies on this same fallthrough to show the confirmation page for the visit that just
        // submitted it.
        return BuildEnvelope(existingInstance, definition, accessProfile, false);
    }

    public virtual WorkflowResponseEnvelope Advance(
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
            WorkflowAccessProfile.UnrestrictedOwner,
            action,
            expectedStateVersion,
            fieldValues);

    public virtual WorkflowResponseEnvelope Advance(
        string instanceId,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile,
        string action,
        int expectedStateVersion,
        Dictionary<string, object?>? fieldValues)
    {
        if (!_instanceStore.TryGet(instanceId, out var instance))
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

        if (!_definitions.TryGetValue(instance.WorkflowKey, out var definition))
        {
            return ErrorEnvelope($"Workflow '{instance.WorkflowKey}' not found.", "DEFINITION_NOT_FOUND");
        }

        if (action.StartsWith("change:", StringComparison.OrdinalIgnoreCase))
        {
            var targetStateKey = action["change:".Length..];
            var targetState = definition.States.FirstOrDefault(s => s.StateKey == targetStateKey);
            if (targetState is null)
            {
                return ErrorEnvelope($"State '{targetStateKey}' not found in definition.", "STATE_NOT_FOUND");
            }

            // FindAccessibleWorkItems (called by BuildEnvelope below) renders from instance.Cursors,
            // not instance.CurrentState, the moment ANY cursor exists — which happens for every
            // workflow that's passed through a gateway, i.e. effectively all of them, since Prism
            // requires state routes to always target a gateway. Updating only CurrentState left a
            // "change:" jump a silent no-op past the first stage: the render kept coming from the
            // stale cursor position and the user landed right back where they started (confirmed
            // live). Move whichever active, non-gateway cursor belongs to the target state's own
            // queue — same cursor a normal forward Advance would move — so the jump actually takes.
            var updatedCursors = instance.Cursors.Count == 0
                ? instance.Cursors
                : MoveCursor(
                    instance.Cursors,
                    instance.Cursors.FirstOrDefault(c => !c.IsAtGateway && c.QueueKey == GetQueueKey(targetState))?.CursorId,
                    targetStateKey,
                    isAtGateway: false);

            var jumped = instance with
            {
                CurrentState = targetStateKey,
                Cursors = updatedCursors,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            SaveInstance(jumped);
            Logger.LogInformation(
                "Change-link: jumped instance {Id} to state '{State}'",
                instanceId,
                targetStateKey);
            return BuildEnvelope(jumped, definition, accessProfile, allowFallbackWhenHidden: true);
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

        var transition = GetOutgoingTransitions(definition, visibleWorkItem.StateKey).FirstOrDefault(
            t => t.FromState == visibleWorkItem.StateKey
                 && t.Action == action);

        if (transition == null)
        {
            return ErrorEnvelope(
                $"Action '{action}' is not valid from state '{visibleWorkItem.StateKey}'.",
                "INVALID_TRANSITION");
        }

        if (ValidateAdvance(instance, definition, fieldValues) is { } validationEnvelope)
        {
            return validationEnvelope;
        }

        // Check if the target is a gateway rather than a plain stage.
        var nextGateway = FindGateway(definition, transition.ToState);
        if (nextGateway != null)
        {
            return string.Equals(nextGateway.GatewayType, "Split", StringComparison.Ordinal)
                ? HandleSplitGatewayAdvance(instance, definition, transition, nextGateway, fieldValues, accessProfile)
                : HandleJoinGatewayAdvance(instance, definition, transition, nextGateway, fieldValues, accessProfile);
        }

        // Regular stage transition (single- or multi-cursor).
        if (instance.Cursors.Count > 0)
        {
            // Multi-cursor: advance only the cursor currently at this stage.
            var sourceCursor = instance.Cursors.FirstOrDefault(c =>
                c.CurrentNodeKey == visibleWorkItem.StateKey && !c.IsAtGateway);
            var updatedCursors = MoveCursor(instance.Cursors, sourceCursor?.CursorId, transition.ToState, isAtGateway: false);
            var primaryState = FirstActiveStageCursorKey(updatedCursors) ?? transition.ToState;
            var updatedMulti = instance with
            {
                CurrentState = primaryState,
                Cursors = updatedCursors,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                FieldValues = Merge(instance.FieldValues, fieldValues)
            };
            SaveInstance(updatedMulti);
            Logger.LogInformation(
                "Multi-cursor advance instance {Id}: cursor {CursorId} → {To}",
                instanceId, sourceCursor?.CursorId ?? "(none)", transition.ToState);
            return BuildEnvelope(updatedMulti, definition, accessProfile, allowFallbackWhenHidden: true);
        }

        var updated = instance with
        {
            CurrentState = transition.ToState,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        SaveInstance(updated);
        Logger.LogInformation(
            "Advanced instance {Id}: {From} → {To}",
            instanceId,
            visibleWorkItem.StateKey,
            transition.ToState);

        return BuildEnvelope(updated, definition, accessProfile, allowFallbackWhenHidden: true);
    }

    public IEnumerable<WorkflowInstanceState> GetAllInstances() => _instanceStore.GetAll();

    public WorkflowInstanceListEnvelope GetInstances(string tenantId, string userId)
    {
        var userInstances = _instanceStore.GetAll()
            .Where(i => string.Equals(i.TenantId, tenantId, StringComparison.Ordinal)
                     && string.Equals(i.UserId, userId, StringComparison.Ordinal))
            .Select(instance =>
            {
                _definitions.TryGetValue(instance.WorkflowKey, out var definition);
                var state = definition?.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
                var stepType = state?.Components.InferStepType() ?? "question";

                return new WorkflowInstanceSummary
                {
                    InstanceId = instance.InstanceId,
                    WorkflowKey = instance.WorkflowKey,
                    WorkflowDisplayName = definition?.DisplayName ?? instance.WorkflowKey,
                    CurrentStateKey = instance.CurrentState,
                    CurrentStateDisplayName = state?.DisplayName ?? instance.CurrentState,
                    StepType = stepType,
                    CreatedAt = instance.CreatedAt.DateTime,
                    LastUpdatedAt = instance.UpdatedAt.DateTime,
                    CanContinue = stepType != "confirmation",
                    IsCompleted = stepType == "confirmation",
                    WorkflowPageUrl = null,
                    InstancePolicy = definition?.InstancePolicy ?? "single"
                };
            })
            .ToList();

        return new WorkflowInstanceListEnvelope
        {
            Instances = userInstances
        };
    }

    public IReadOnlyList<string> ClaimInstances(string tenantId, string fromUserId, string toUserId)
    {
        if (string.IsNullOrWhiteSpace(fromUserId)
            || string.IsNullOrWhiteSpace(toUserId)
            || string.Equals(fromUserId, toUserId, StringComparison.Ordinal))
        {
            return [];
        }

        var allInstances = _instanceStore.GetAll().ToList();
        var workflowsAlreadyOwned = allInstances
            .Where(i => string.Equals(i.TenantId, tenantId, StringComparison.Ordinal)
                     && string.Equals(i.UserId, toUserId, StringComparison.Ordinal))
            .Select(i => i.WorkflowKey)
            .ToHashSet(StringComparer.Ordinal);

        var claimed = new List<string>();
        foreach (var instance in allInstances)
        {
            if (!string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal)
                || !string.Equals(instance.UserId, fromUserId, StringComparison.Ordinal))
            {
                continue;
            }

            if (workflowsAlreadyOwned.Contains(instance.WorkflowKey))
            {
                // The signed-in user already has their own instance of this workflow — leave
                // the anonymous one behind rather than silently discarding whichever loses.
                continue;
            }

            _instanceStore.Save(instance with
            {
                UserId = toUserId,
                IsAuthenticated = true,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            claimed.Add(instance.InstanceId);
        }

        return claimed;
    }

    public WorkflowQueueWorkListEnvelope GetQueueWorkItems(WorkflowAccessProfile accessProfile)
    {
        var items = _instanceStore.GetAll()
            .SelectMany(instance =>
            {
                if (!_definitions.TryGetValue(instance.WorkflowKey, out var definition))
                {
                    return Array.Empty<WorkflowQueueWorkItem>();
                }

                return FindAccessibleWorkItems(instance, definition, accessProfile)
                    .Where(item => item.AvailableActions.Count > 0)
                    .Select(item => item.ToEnvelopeItem(instance, definition))
                    .ToArray();
            })
            .OrderBy(item => item.WorkflowDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.StateDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.InstanceId, StringComparer.Ordinal)
            .ToArray();

        return new WorkflowQueueWorkListEnvelope
        {
            Items = items
        };
    }

    public IEnumerable<WorkflowDefinitionFile> GetAllDefinitions() => _definitions.Values;

    public WorkflowDefinitionFile? GetDefinition(string key) =>
        _definitions.TryGetValue(key, out var definition) ? definition : null;

    /// <summary>
    /// Registers or updates a definition in the live engine — an upsert, not update-only. A brand
    /// new key (one this engine has never seen, e.g. a workflow an agent or human just authored
    /// from scratch via save_workflow) must actually become servable here, or the documented
    /// promise that "a save reaches the live engine immediately" is false for exactly the scenario
    /// — authoring a new service — the whole toolkit exists for. Always returns true.
    /// </summary>
    public bool UpdateDefinition(string key, WorkflowDefinitionFile updated)
    {
        var isNewKey = !_definitions.ContainsKey(key);
        _definitions[key] = updated;
        Logger.LogInformation(
            isNewKey ? "Workflow definition registered in-memory: {Key}" : "Workflow definition updated in-memory: {Key}",
            key);
        return true;
    }

    /// <summary>
    /// Removes a definition from the live engine — the delete-side counterpart to
    /// <see cref="UpdateDefinition"/>. Existing instances already running against this key are
    /// left untouched (they keep whatever state they have; the definition lookups they depend on,
    /// e.g. in <see cref="GetCurrent(string,string,string,WorkflowAccessProfile,string?,string?)"/>,
    /// will simply start failing with DEFINITION_NOT_FOUND) — deleting a workflow definition that
    /// still has active instances is a host-authoring concern to guard against, not this engine's.
    /// </summary>
    public bool RemoveDefinition(string key) => _definitions.Remove(key);

    public bool Reset(string instanceId)
    {
        if (!_instanceStore.Remove(instanceId))
        {
            return false;
        }

        Logger.LogInformation("Reset (deleted) instance {Id}", instanceId);
        return true;
    }

    public void ResetAll()
    {
        _instanceStore.Clear();
        Logger.LogInformation("ResetAll: all workflow instances cleared");
    }

    protected virtual WorkflowResponseEnvelope? ValidateAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        Dictionary<string, object?>? fieldValues) => null;

    protected virtual WorkflowResponseEnvelope? InitializeNewInstance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        string? action) => null;

    /// <summary>
    /// Host hook invoked before a state's components are rendered. Returns structured
    /// display data for the step (surfaced as <see cref="StepContent.Data"/> and resolved
    /// into "interactive" components via their DataKey), or null when the state needs none.
    /// Implementations may enrich <paramref name="instance"/>.FieldValues (e.g. with freshly
    /// computed results) before rendering; the shared FieldValues dictionary makes such
    /// enrichment visible to the stored instance.
    /// </summary>
    protected virtual System.Text.Json.Nodes.JsonObject? BuildRenderData(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        StepDefinition state) => null;

    /// <summary>
    /// Host hook supplying typed values for the definition's <c>source: "service"</c>
    /// calculation fields (e.g. a member record from a system of record). Values may be
    /// scalars (decimal/bool/string) or nested string-keyed dictionaries for dotted access.
    /// A subclassing host overrides this method directly; a composed caller (e.g. the
    /// simulation runner) supplies the constructor's <c>serviceInputsResolver</c> delegate
    /// instead — this default implementation prefers that delegate when one was given.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, object?>? ResolveServiceInputs(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        StepDefinition state) => _serviceInputsResolver?.Invoke(instance, definition, state);

    protected bool TryGetInstance(string instanceId, out WorkflowInstanceState instance) =>
        _instanceStore.TryGet(instanceId, out instance!);

    protected void SaveInstance(WorkflowInstanceState instance) =>
        _instanceStore.Save(instance);

    /// <summary>
    /// The most recently computed <see cref="CalculationResult"/> for an instance, if its
    /// current state has a calculations block and it evaluated cleanly — <c>null</c> if the
    /// instance doesn't exist, its state has no calculations block, or evaluation failed. A
    /// composed caller (e.g. the simulation runner) uses this to read raw calculated values
    /// without duplicating evaluation itself.
    /// </summary>
    public CalculationResult? GetLastCalculationResult(string instanceId) =>
        TryGetInstance(instanceId, out var instance) ? instance.LastCalculationResult : null;

    protected WorkflowResponseEnvelope BuildEnvelope(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowAccessProfile accessProfile,
        bool allowFallbackWhenHidden = false)
    {
        var workItems = FindAccessibleWorkItems(instance, definition, accessProfile);
        var visibleItem = workItems.FirstOrDefault()
            ?? (allowFallbackWhenHidden ? FindFallbackWorkItem(instance, definition, accessProfile) : null);

        if (visibleItem is null)
        {
            return ErrorEnvelope(
                "Access denied to the current workflow queue.",
                "ACCESS_DENIED");
        }

        if (visibleItem.IsJoinGateway)
        {
            var joinGateway = FindGateway(definition, visibleItem.StateKey);
            if (joinGateway is not null)
            {
                return BuildJoinWaitingEnvelope(instance, definition, joinGateway);
            }
        }

        var state = definition.States.FirstOrDefault(s => s.StateKey == visibleItem.StateKey);
        if (state == null)
        {
            return ErrorEnvelope(
                $"State '{visibleItem.StateKey}' not found in definition '{definition.DefinitionKey}'.",
                "STATE_NOT_FOUND");
        }

        var renderData = BuildRenderData(instance, definition, state);
        var calc = EvaluateDefinitionCalculations(instance, definition, state);
        if (calc is not null)
        {
            renderData ??= new JsonObject();
            renderData["live"] = BuildLiveModel(definition, calc);
        }

        var components = BuildComponents(state.Components, instance.FieldValues, calc);
        var effectiveStepType = state.Components.InferStepType();
        var waitingComponent = state.Components.OfType<WaitingComponent>().FirstOrDefault();

        var render = new StepContent
        {
            StepType = effectiveStepType,
            StateDisplayName = state.DisplayName,
            Components = components,
            AvailableActions = visibleItem.AvailableActions.ToArray(),
            Data = renderData
        };

        var responseState = effectiveStepType switch
        {
            "status-timeline" => "defer",
            "confirmation" => "complete",
            _ => "render"
        };

        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            ResponseState = responseState,
            StateVersion = instance.StateVersion,
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = waitingComponent?.PollIntervalMs,
            Render = render,
            InstancePolicy = definition.InstancePolicy
        };
    }

    protected bool CanAccessInstance(
        WorkflowInstanceState instance,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile)
    {
        if (!string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal))
        {
            return false;
        }

        return !accessProfile.RestrictToInstanceOwner
               || string.Equals(instance.UserId, userId, StringComparison.Ordinal);
    }

    protected bool CanStartInitialState(WorkflowDefinitionFile definition, WorkflowAccessProfile accessProfile)
    {
        var initialState = definition.States.FirstOrDefault(state =>
            string.Equals(state.StateKey, definition.InitialState, StringComparison.Ordinal));

        var queueName = initialState is null ? null : ResolveQueueName(definition, initialState);
        return accessProfile.CanViewQueue(queueName) && accessProfile.CanStartQueue(queueName);
    }

    protected IReadOnlyList<AccessibleWorkItem> FindAccessibleWorkItems(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowAccessProfile accessProfile)
    {
        var items = new List<AccessibleWorkItem>();

        if (instance.Cursors.Count == 0)
        {
            var state = definition.States.FirstOrDefault(candidate =>
                string.Equals(candidate.StateKey, instance.CurrentState, StringComparison.Ordinal));

            if (state is not null)
            {
                var queueKey = GetQueueKey(state);
                var queueName = ResolveQueueName(definition, state);
                if (CanViewQueue(definition, queueKey, queueName, accessProfile))
                {
                    items.Add(new AccessibleWorkItem(
                        state.StateKey,
                        state.DisplayName,
                        queueName,
                        IsJoinGateway: false,
                        BuildAvailableActions(definition, state.StateKey, queueName, accessProfile)));
                }
            }

            return items;
        }

        foreach (var cursor in instance.Cursors.Where(candidate => !candidate.IsAtGateway))
        {
            var state = definition.States.FirstOrDefault(candidate =>
                string.Equals(candidate.StateKey, cursor.CurrentNodeKey, StringComparison.Ordinal));

            if (state is null)
            {
                continue;
            }

            var queueName = ResolveQueueName(definition, cursor.QueueKey);
            if (!CanViewQueue(definition, cursor.QueueKey, queueName, accessProfile))
            {
                continue;
            }

            items.Add(new AccessibleWorkItem(
                state.StateKey,
                state.DisplayName,
                queueName,
                IsJoinGateway: false,
                BuildAvailableActions(definition, state.StateKey, queueName, accessProfile)));
        }

        foreach (var cursor in instance.Cursors.Where(candidate => candidate.IsAtGateway))
        {
            var gateway = FindGateway(definition, cursor.CurrentNodeKey);
            if (gateway is null || !string.Equals(gateway.GatewayType, "Join", StringComparison.Ordinal))
            {
                continue;
            }

            var queueName = ResolveQueueName(definition, gateway);
            if (!CanViewQueue(definition, gateway.QueueKey, queueName, accessProfile))
            {
                continue;
            }

            items.Add(new AccessibleWorkItem(
                gateway.Key,
                gateway.DisplayName,
                queueName,
                IsJoinGateway: true,
                []));
        }

        return items
            .OrderByDescending(item => string.Equals(item.StateKey, instance.CurrentState, StringComparison.Ordinal))
            .ThenBy(item => item.IsJoinGateway)
            .ThenBy(item => item.StateKey, StringComparer.Ordinal)
            .ToArray();
    }

    private AccessibleWorkItem? FindFallbackWorkItem(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowAccessProfile accessProfile)
    {
        var fallbackStageCursor = instance.Cursors.FirstOrDefault(candidate => !candidate.IsAtGateway);
        if (fallbackStageCursor is not null)
        {
            var state = definition.States.FirstOrDefault(candidate =>
                string.Equals(candidate.StateKey, fallbackStageCursor.CurrentNodeKey, StringComparison.Ordinal));

            if (state is not null)
            {
                var queueName = ResolveQueueName(definition, fallbackStageCursor.QueueKey);
                return new AccessibleWorkItem(
                    state.StateKey,
                    state.DisplayName,
                    queueName,
                    IsJoinGateway: false,
                    BuildAvailableActions(definition, state.StateKey, queueName, accessProfile));
            }
        }

        if (instance.Cursors.Count > 0)
        {
            var gateway = instance.Cursors
                .Where(candidate => candidate.IsAtGateway)
                .Select(candidate => FindGateway(definition, candidate.CurrentNodeKey))
                .FirstOrDefault(candidate => candidate is not null && string.Equals(candidate.GatewayType, "Join", StringComparison.Ordinal));

            if (gateway is not null)
            {
                return new AccessibleWorkItem(
                    gateway.Key,
                    gateway.DisplayName,
                    ResolveQueueName(definition, gateway),
                    IsJoinGateway: true,
                    []);
            }
        }

        var currentState = definition.States.FirstOrDefault(candidate =>
            string.Equals(candidate.StateKey, instance.CurrentState, StringComparison.Ordinal));

        if (currentState is null)
        {
            return null;
        }

        var currentQueue = ResolveQueueName(definition, currentState);
        return new AccessibleWorkItem(
            currentState.StateKey,
            currentState.DisplayName,
            currentQueue,
            IsJoinGateway: false,
            BuildAvailableActions(definition, currentState.StateKey, currentQueue, accessProfile));
    }

    protected bool CanViewQueue(
        WorkflowDefinitionFile definition,
        string? queueKey,
        string? queueName,
        WorkflowAccessProfile accessProfile)
    {
        return accessProfile.CanViewQueue(queueName);
    }

    protected IReadOnlyList<WorkflowAction> BuildAvailableActions(
        WorkflowDefinitionFile definition,
        string stateKey,
        string? queueName,
        WorkflowAccessProfile accessProfile)
    {
        var transitions = GetOutgoingTransitions(definition, stateKey);

        if (string.IsNullOrWhiteSpace(queueName))
        {
            transitions = transitions.Where(transition => transition.RequiresRole is null).ToArray();
        }
        else if (!accessProfile.CanActInQueue(queueName))
        {
            return [];
        }

        return transitions
            .Select(transition => new WorkflowAction
            {
                ActionKey = transition.Action,
                // `??` alone doesn't catch an empty-but-non-null Label, which is exactly what an
                // agent leaving the field blank (rather than omitting it) produces — treat blank
                // the same as absent. transition.Action is never blank by the time it gets here;
                // GetOutgoingTransitions defaults it below, the one place raw route.Trigger values
                // are read, so every consumer (this, and the action-matching in Advance) agrees.
                Label = string.IsNullOrWhiteSpace(transition.Label) ? ActionLabel(transition.Action) : transition.Label,
                Style = transition.Style ?? ActionStyle(transition.Action)
            })
            .ToArray();
    }

    /// <summary>
    /// A route's trigger can be authored blank (an AI agent leaving it empty rather than omitting
    /// it, so it survives as "" not null) — default it to "continue" here, the single place raw
    /// route.Trigger values are read into a transition's Action, so the rendered button's value
    /// and the action-matching in <see cref="Advance"/> always agree on the same non-empty key.
    /// </summary>
    private static string ResolveTrigger(string? trigger) =>
        string.IsNullOrWhiteSpace(trigger) ? "continue" : trigger;

    protected static string? ResolveQueueName(WorkflowDefinitionFile definition, string? queueKey)
    {
        if (string.IsNullOrWhiteSpace(queueKey))
        {
            return null;
        }

        var queue = GetQueues(definition).FirstOrDefault(candidate =>
            string.Equals(candidate.Key, queueKey, StringComparison.Ordinal));

        return queue?.Key ?? queueKey;
    }

    protected static string? ResolveQueueName(WorkflowDefinitionFile definition, StepDefinition? state) =>
        state is null
            ? null
            : ResolveQueueName(definition, GetQueueKey(state));

    protected static string? ResolveQueueName(WorkflowDefinitionFile definition, WorkflowGatewayDefinition? gateway) =>
        gateway is null
            ? null
            : ResolveQueueName(definition, gateway.QueueKey);

    protected static string? GetQueueKey(StepDefinition? state) =>
        FirstNonEmpty(state?.QueueKey, state?.Metadata?.QueueKey);

    protected static IReadOnlyList<WorkflowQueueDefinition> GetQueues(WorkflowDefinitionFile definition) =>
        definition.Queues ?? [];

    protected static IReadOnlyList<WorkflowGatewayDefinition> GetGateways(WorkflowDefinitionFile definition) =>
        definition.Gateways ?? definition.Metadata?.Gateways ?? [];

    protected static IReadOnlyList<WorkflowTransitionFile> GetOutgoingTransitions(
        WorkflowDefinitionFile definition,
        string sourceKey)
    {
        var state = definition.States.FirstOrDefault(candidate =>
            string.Equals(candidate.StateKey, sourceKey, StringComparison.Ordinal));
        if (state?.Routes is { Count: > 0 })
        {
            return state.Routes
                .Select(route => new WorkflowTransitionFile
                {
                    FromState = sourceKey,
                    ToState = route.Target,
                    Action = ResolveTrigger(route.Trigger),
                    Label = route.Label,
                    Style = route.Style,
                    RequiresRole = route.RequiresRole,
                    Conditions = route.Conditions,
                    Actions = route.Actions
                })
                .OrderBy(transition => transition.ToState, StringComparer.Ordinal)
                .ThenBy(transition => transition.Action, StringComparer.Ordinal)
                .ToArray();
        }

        var gateway = FindGateway(definition, sourceKey);
        if (gateway?.Routes is { Count: > 0 })
        {
            return gateway.Routes
                .Select(route => new WorkflowTransitionFile
                {
                    FromState = gateway.Key,
                    ToState = route.Target,
                    Action = ResolveTrigger(route.Trigger),
                    Label = route.Label,
                    Style = route.Style,
                    RequiresRole = route.RequiresRole,
                    Conditions = route.Conditions,
                    Actions = route.Actions
                })
                .OrderBy(transition => transition.ToState, StringComparer.Ordinal)
                .ThenBy(transition => transition.Action, StringComparer.Ordinal)
                .ToArray();
        }

        var declaredTransitions = definition.Transitions
            ?.Where(transition => string.Equals(transition.FromState, sourceKey, StringComparison.Ordinal))
            .ToArray();

        if (declaredTransitions is { Length: > 0 })
        {
            return declaredTransitions;
        }

        var sourceGateway = GetGateways(definition)
            .Where(candidate =>
                string.Equals(candidate.Source, sourceKey, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Key, StringComparer.Ordinal)
            .ToArray();

        if (sourceGateway.Length == 0)
        {
            return [];
        }

        var transitions = new List<WorkflowTransitionFile>();

        foreach (var candidate in sourceGateway)
        {
            var routes = candidate.Routes ?? [];
            var distinctTriggers = routes
                .Select(route => route.Trigger)
                .Where(trigger => !string.IsNullOrWhiteSpace(trigger))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var isParallelFork = string.Equals(candidate.GatewayType, "Split", StringComparison.OrdinalIgnoreCase)
                                 && routes.Count >= 2
                                 && distinctTriggers.Length == 1;

            if (isParallelFork)
            {
                transitions.Add(new WorkflowTransitionFile
                {
                    FromState = sourceKey,
                    ToState = candidate.Key,
                    Action = distinctTriggers[0]
                });

                continue;
            }

            transitions.AddRange(routes.Select(route => new WorkflowTransitionFile
            {
                FromState = sourceKey,
                ToState = route.Target,
                Action = ResolveTrigger(route.Trigger),
                Label = route.Label,
                Style = route.Style,
                RequiresRole = route.RequiresRole,
                Conditions = route.Conditions,
                Actions = route.Actions
            }));
        }

        return transitions
            .OrderBy(transition => transition.ToState, StringComparer.Ordinal)
            .ThenBy(transition => transition.Action, StringComparer.Ordinal)
            .ToArray();
    }


    protected sealed record AccessibleWorkItem(
        string StateKey,
        string DisplayName,
        string? QueueName,
        bool IsJoinGateway,
        IReadOnlyList<WorkflowAction> AvailableActions)
    {
        public WorkflowQueueWorkItem ToEnvelopeItem(
            WorkflowInstanceState instance,
            WorkflowDefinitionFile definition) =>
            new()
            {
                InstanceId = instance.InstanceId,
                WorkflowKey = instance.WorkflowKey,
                WorkflowDisplayName = definition.DisplayName,
                StateKey = StateKey,
                StateDisplayName = DisplayName,
                QueueName = QueueName,
                TenantId = instance.TenantId,
                UserId = instance.UserId,
                StateVersion = instance.StateVersion,
                AvailableActions = AvailableActions
            };
    }

    protected static WorkflowResponseEnvelope ErrorEnvelope(string message, string code) =>
        new()
        {
            InstanceId = string.Empty,
            ResponseState = "error",
            StateVersion = 0,
            CorrelationId = Guid.NewGuid().ToString(),
            ServerTimeUtc = DateTimeOffset.UtcNow,
            Problems = [new WorkflowProblem { FieldKey = string.Empty, Message = message, Code = code }]
        };

    private WorkflowResponseEnvelope CreateAndRegisterNewInstance(
        string workflowKey,
        string tenantId,
        string userId,
        WorkflowDefinitionFile definition,
        WorkflowAccessProfile accessProfile,
        string? action,
        string logMessage,
        params object?[] additionalLogArgs)
    {
        var instance = CreateNewInstance(
            workflowKey, tenantId, userId, definition.InitialState, ResolveIsAuthenticated(tenantId, userId));
        if (InitializeNewInstance(instance, definition, action) is { } error)
        {
            return error;
        }

        _instanceStore.Save(instance);

        Logger.LogInformation(logMessage, [instance.InstanceId, .. additionalLogArgs]);
        return BuildEnvelope(instance, definition, accessProfile, false);
    }

    private static WorkflowInstanceState CreateNewInstance(
        string workflowKey,
        string tenantId,
        string userId,
        string initialState,
        bool isAuthenticated)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowInstanceState
        {
            InstanceId = Guid.NewGuid().ToString(),
            WorkflowKey = workflowKey,
            TenantId = tenantId,
            UserId = userId,
            IsAuthenticated = isAuthenticated,
            CurrentState = initialState,
            StateVersion = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Whether <paramref name="userId"/> identifies a signed-in user, for a store that wants
    /// to apply a different retention policy for authenticated vs anonymous instances (see
    /// <see cref="WorkflowInstanceState.IsAuthenticated"/>). The base engine has no identity
    /// model of its own — always false — a host overrides this using whatever identity
    /// resolution its own request pipeline already performs.
    /// </summary>
    protected virtual bool ResolveIsAuthenticated(string tenantId, string userId) => false;

    /// <summary>Evaluated calculation state for one render pass.</summary>
    protected sealed record CalculationRenderContext(
        WorkflowCalculationSet Set,
        IReadOnlyDictionary<string, object?> Scope,
        CalculationResult Result,
        IReadOnlyDictionary<string, object?> DisplayValues);

    private readonly CalculationEvaluator _calculationEvaluator = new();

    private CalculationRenderContext? EvaluateDefinitionCalculations(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        StepDefinition state)
    {
        if (definition.Calculations is null)
        {
            return null;
        }

        try
        {
            var serviceInputs = ResolveServiceInputs(instance, definition, state);
            var scope = CalculationScopeBuilder.Build(definition, instance.FieldValues, serviceInputs);
            var result = _calculationEvaluator.Evaluate(definition.Calculations, scope);

            // Full scope (inputs + calculated fields) for showWhen evaluation.
            var fullScope = new Dictionary<string, object?>(scope, StringComparer.Ordinal);
            foreach (var (name, value) in result.Fields)
            {
                fullScope[name] = value;
            }

            // Display overlay: saved values, then calculated fields formatted per their
            // declared format — this is what stat-groups and summary-lists resolve from.
            var display = new Dictionary<string, object?>(instance.FieldValues, StringComparer.Ordinal);
            foreach (var (name, value) in result.Fields)
            {
                var format = definition.Calculations.Fields.TryGetValue(name, out var field) ? field.Format : null;
                display[name] = FormatCalculatedValue(value, format);
            }

            // Last computed result is kept on the instance so a composed caller (e.g. the
            // simulation runner, which builds this engine rather than subclassing it) can
            // read raw calculated values without duplicating evaluation itself.
            SaveInstance(instance with { LastCalculationResult = result });

            return new CalculationRenderContext(definition.Calculations, fullScope, result, display);
        }
        catch (CalculationException exception)
        {
            Logger.LogWarning(
                exception,
                "Calculation evaluation failed for workflow {Key}, state {State}; rendering without calculated values.",
                definition.DefinitionKey,
                state.StateKey);
            return null;
        }
    }

    private static string? FormatCalculatedValue(object? value, string? format) => value switch
    {
        null => null,
        decimal d when string.Equals(format, "gbp", StringComparison.OrdinalIgnoreCase) =>
            string.Create(
                System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
                $"£{Math.Round(d, 0, MidpointRounding.AwayFromZero):N0}"),
        decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => value.ToString()
    };

    private JsonObject BuildLiveModel(WorkflowDefinitionFile definition, CalculationRenderContext calc)
    {
        var inputTypes = new JsonObject();
        var defaults = new JsonObject();
        foreach (var (fieldKey, (type, defaultValue)) in CalculationScopeBuilder.DescribeInputs(definition))
        {
            inputTypes[fieldKey] = type;
            if (defaultValue is not null)
            {
                defaults[fieldKey] = defaultValue;
            }
        }

        var serviceValues = new JsonObject();
        foreach (var (name, field) in calc.Set.Fields)
        {
            if (string.Equals(field.Source, "service", StringComparison.OrdinalIgnoreCase)
                && calc.Scope.TryGetValue(name, out var value))
            {
                serviceValues[name] = ScopeValueToJson(value);
            }
        }

        return new JsonObject
        {
            ["calculations"] = JsonSerializer.SerializeToNode(calc.Set, LiveModelJsonOptions),
            ["inputTypes"] = inputTypes,
            ["defaults"] = defaults,
            ["service"] = serviceValues
        };
    }

    private static readonly JsonSerializerOptions LiveModelJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static JsonNode? ScopeValueToJson(object? value) => value switch
    {
        null => null,
        decimal d => JsonValue.Create(d),
        bool b => JsonValue.Create(b),
        string text => JsonValue.Create(text),
        IReadOnlyDictionary<string, object?> map => new JsonObject(
            map.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, ScopeValueToJson(pair.Value)))),
        _ => JsonValue.Create(value.ToString())
    };

    private bool EvaluateShowWhen(string? showWhen, CalculationRenderContext? calc)
    {
        if (string.IsNullOrWhiteSpace(showWhen) || calc is null)
        {
            return true;
        }

        try
        {
            return _calculationEvaluator.EvaluateExpression(showWhen, calc.Scope, calc.Set) is not false;
        }
        catch (CalculationException exception)
        {
            Logger.LogWarning(exception, "showWhen expression '{Expr}' failed; component stays visible.", showWhen);
            return true;
        }
    }

    private PrismComponentRenderPayload[] BuildComponents(
        IReadOnlyList<PrismComponent> componentDefinitions,
        Dictionary<string, object?> savedValues,
        CalculationRenderContext? calc = null)
    {
        // Stat-groups and summary-lists resolve display values from the calculation
        // overlay when one exists; plain input values come from the instance as before.
        var displayValues = calc is null
            ? savedValues
            : new Dictionary<string, object?>(calc.DisplayValues, StringComparer.Ordinal);

        var result = new List<PrismComponentRenderPayload>();

        foreach (var component in componentDefinitions)
        {
            var payloadsBefore = result.Count;
            switch (component)
            {
                case FieldsetComponent fieldset:
                {
                    var fields = BuildFields(fieldset.Children, displayValues, calc);
                    if (fields.Length == 0)
                    {
                        Logger.LogWarning("Fieldset component contains no renderable fields");
                        continue;
                    }

                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "fieldset",
                        Legend = fieldset.Legend,
                        LegendSize = fieldset.LegendSize,
                        Fields = fields
                    });
                    break;
                }

                case SummaryListComponent summary:
                {
                    var fields = BuildFields(summary.Children, displayValues, calc);
                    if (fields.Length == 0)
                    {
                        Logger.LogWarning("Summary-list component contains no renderable fields");
                        continue;
                    }

                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "summary-list",
                        Title = summary.Title,
                        SourceStateKey = summary.ChangeStateKey,
                        Fields = fields
                    });
                    break;
                }

                case AccordionComponent accordion:
                {
                    var sections = accordion.Sections
                        .Select(section => new PrismAccordionSectionPayload
                        {
                            Heading = section.Heading,
                            Summary = section.Summary,
                            Fields = BuildFields(section.Children, displayValues, calc)
                        })
                        .ToArray();

                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "accordion",
                        AccordionSections = sections
                    });
                    break;
                }

                case WaitingComponent waiting:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "waiting",
                        Content = _sanitizer.Sanitize(waiting.Content),
                        ExpectedWaitSeconds = waiting.ExpectedWaitSeconds,
                        PollIntervalMs = waiting.PollIntervalMs,
                        AllowDefer = waiting.AllowDefer,
                        DeferMessage = waiting.DeferMessage
                    });
                    break;

                case PanelComponent panel:
                    result.Add(new PrismComponentRenderPayload { Type = "panel", Heading = panel.Heading });
                    break;

                case BodyComponent body:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "body",
                        Content = _sanitizer.Sanitize(body.Content)
                    });
                    break;

                case HeadingComponent heading:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "heading",
                        Content = heading.Content,
                        Level = heading.Level
                    });
                    break;

                case InsetTextComponent inset:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "inset-text",
                        Content = _sanitizer.Sanitize(inset.Content)
                    });
                    break;

                case WarningTextComponent warning:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "warning-text",
                        Content = _sanitizer.Sanitize(warning.Content)
                    });
                    break;

                case DetailsComponent details:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "details",
                        Heading = details.Heading,
                        Content = _sanitizer.Sanitize(details.Content)
                    });
                    break;

                case NotificationBannerComponent banner:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "notification-banner",
                        Heading = banner.Heading,
                        Content = _sanitizer.Sanitize(banner.Content),
                        BannerType = banner.BannerType
                    });
                    break;

                case TaskListComponent taskList:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "task-list",
                        TaskSections = taskList.Sections?.Select(section => new PrismTaskSection
                        {
                            Heading = section.Heading,
                            Tasks = section.Tasks.Select(task => new PrismTaskItem
                            {
                                Label = task.Label,
                                Href = task.Href ?? task.StateKey,
                                Status = "not-started"
                            }).ToArray()
                        }).ToArray()
                    });
                    break;

                case StatGroupComponent statGroup:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "stat-group",
                        Title = statGroup.Title,
                        Stats = statGroup.Items.Select(item => new PrismStatItem
                        {
                            Label = item.Label,
                            FieldKey = item.FieldKey,
                            Value = displayValues.TryGetValue(item.FieldKey, out var statValue)
                                ? statValue?.ToString()
                                : null,
                            Qualifier = item.Qualifier,
                            Emphasis = item.Emphasis
                        }).ToArray()
                    });
                    break;

                case ChartComponent chart:
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "chart",
                        Heading = chart.Title,
                        ChartJson = BuildChartJson(chart, calc)
                    });
                    break;

                case InputComponent input:
                {
                    var fields = BuildFields(new[] { (PrismComponent)input }, displayValues, calc);
                    result.Add(new PrismComponentRenderPayload
                    {
                        Type = "fieldset",
                        Fields = fields
                    });
                    break;
                }
            }

            if (component.ShowWhen is { Length: > 0 } showWhen)
            {
                var visible = EvaluateShowWhen(showWhen, calc);
                for (var i = payloadsBefore; i < result.Count; i++)
                {
                    result[i] = result[i] with { ShowWhen = showWhen, Hidden = !visible };
                }
            }
        }

        return result.ToArray();
    }

    private static string BuildChartJson(ChartComponent chart, CalculationRenderContext? calc)
    {
        var bands = new JsonArray();
        foreach (var band in chart.Bands)
        {
            bands.Add(new JsonObject
            {
                ["key"] = band.Key,
                ["label"] = band.Label,
                ["color"] = band.Color
            });
        }

        var rows = new JsonArray();
        if (calc is not null && calc.Result.Series.TryGetValue(chart.Series, out var seriesRows))
        {
            foreach (var seriesRow in seriesRows)
            {
                var row = new JsonObject();
                foreach (var (column, value) in seriesRow)
                {
                    row[column] = ScopeValueToJson(value);
                }

                rows.Add(row);
            }
        }

        return new JsonObject
        {
            ["kind"] = chart.Kind,
            ["x"] = chart.X,
            ["xLabelEvery"] = chart.XLabelEvery,
            ["series"] = chart.Series,
            ["bands"] = bands,
            ["rows"] = rows
        }.ToJsonString();
    }

    private static FieldRenderPayload[] BuildFields(
        IEnumerable<PrismComponent> children,
        Dictionary<string, object?> savedValues,
        CalculationRenderContext? calc = null)
    {
        var fields = new List<FieldRenderPayload>();

        foreach (var child in children)
        {
            switch (child)
            {
                case InputComponent input:
                    fields.Add(BuildInputPayload(input, savedValues, calc));

                    var conditional = (child as RadiosComponent)?.ConditionalChildren
                                      ?? (child as CheckboxesComponent)?.ConditionalChildren;
                    if (conditional != null)
                    {
                        foreach (var (optionValue, subComponents) in conditional)
                        {
                            foreach (var sub in subComponents.GetAllInputs())
                            {
                                fields.Add(BuildInputPayload(sub, savedValues, calc) with
                                {
                                    ConditionalOn = input.FieldKey,
                                    VisibleWhen = optionValue
                                });
                            }
                        }
                    }

                    break;

                case FieldsetComponent nestedFieldset:
                    fields.AddRange(BuildFields(nestedFieldset.Children, savedValues, calc));
                    break;
            }
        }

        return fields.ToArray();
    }

    private static FieldRenderPayload BuildInputPayload(
        InputComponent input,
        Dictionary<string, object?> savedValues,
        CalculationRenderContext? calc = null)
    {
        var fieldType = InputFieldType(input);
        return new FieldRenderPayload
        {
            FieldKey = input.FieldKey,
            Label = input.Label,
            Hint = input.Hint,
            FieldType = fieldType,
            Required = input.Required,
            Options = input switch
            {
                SelectComponent select => select.Options,
                RadiosComponent radios => radios.Options,
                CheckboxesComponent checkboxes => checkboxes.Options,
                GuidanceChecklistComponent guidance => guidance.Items.Select(i => i.Key).ToList(),
                _ => null
            },
            Value = GetDisplayValue(input, fieldType, savedValues) ?? ResolveDefaultFrom(input, calc) ?? input.Default,
            MinLength = input switch
            {
                TextInputComponent text => text.MinLength,
                TextareaComponent textarea => textarea.MinLength,
                _ => null
            },
            MaxLength = input switch
            {
                TextInputComponent text => text.MaxLength,
                TextareaComponent textarea => textarea.MaxLength,
                _ => null
            },
            Pattern = input switch
            {
                TextInputComponent text => text.Pattern,
                EmailComponent email => email.Pattern,
                TelComponent tel => tel.Pattern,
                _ => null
            },
            Min = input switch
            {
                NumberInputComponent number => number.Min,
                DecimalInputComponent decimalInput => decimalInput.Min,
                SliderComponent slider => slider.Min,
                _ => null
            },
            Max = input switch
            {
                NumberInputComponent number => number.Max,
                DecimalInputComponent decimalInput => decimalInput.Max,
                SliderComponent slider => slider.Max,
                _ => null
            },
            Step = input switch
            {
                SliderComponent slider => slider.Step,
                _ => null
            },
            Suffix = input switch
            {
                SliderComponent slider => slider.Suffix,
                _ => null
            },
            Prefix = input switch
            {
                TextInputComponent text => text.Prefix,
                NumberInputComponent number => number.Prefix,
                DecimalInputComponent decimalInput => decimalInput.Prefix,
                SliderComponent slider => slider.Prefix,
                _ => null
            },
            ConditionalOn = input.ConditionalOn,
            VisibleWhen = input.VisibleWhen,
            ChangeStateKey = input.ChangeStateKey,
            AcceptedFileTypes = input switch
            {
                FileUploadComponent file => file.AcceptedFileTypes,
                _ => null
            },
            MaxSizeBytes = input switch
            {
                FileUploadComponent file => file.MaxSizeBytes,
                _ => null
            },
            GuidanceItems = input switch
            {
                GuidanceChecklistComponent guidance => guidance.Items,
                _ => null
            }
        };
    }

    private static string InputFieldType(InputComponent input) => input switch
    {
        TextInputComponent => "text",
        NumberInputComponent => "number",
        DecimalInputComponent => "decimal",
        SelectComponent => "select",
        RadiosComponent => "radio",
        CheckboxesComponent => "checkboxlist",
        DateInputComponent => "date",
        EmailComponent => "email",
        TelComponent => "tel",
        TextareaComponent => "textarea",
        BooleanComponent => "boolean",
        SliderComponent => "slider",
        FileUploadComponent => "file-upload",
        GuidanceChecklistComponent => "guidance-checklist",
        _ => "text"
    };

    private static object? GetDisplayValue(
        InputComponent input,
        string fieldType,
        Dictionary<string, object?> savedValues)
    {
        var raw = savedValues.TryGetValue(input.FieldKey, out var value) ? value : null;
        if (raw == null)
        {
            return null;
        }

        if (fieldType == "checkboxlist" || fieldType == "checkboxes")
        {
            var rawString = raw switch
            {
                string stringValue => stringValue,
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
                _ => null
            };

            if (rawString != null)
            {
                raw = string.Join(
                    ", ",
                    rawString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (fieldType == "file-upload")
        {
            // A freshly-uploaded file survives as its original CLR type for the rest of the
            // current request; a previously-uploaded one reloads from persistence as a boxed
            // JsonElement (no custom converter on FieldValues) — display the original filename
            // either way, never the reference object itself.
            return raw switch
            {
                WorkflowFileReference reference => reference.OriginalFileName,
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object
                    && jsonElement.TryGetProperty(nameof(WorkflowFileReference.OriginalFileName), out var nameProperty)
                    => nameProperty.GetString(),
                _ => null
            };
        }

        var prefix = input switch
        {
            TextInputComponent text => text.Prefix,
            NumberInputComponent number => number.Prefix,
            DecimalInputComponent decimalInput => decimalInput.Prefix,
            _ => null
        };

        return !string.IsNullOrEmpty(prefix)
            ? $"{prefix}{raw}"
            : raw;
    }

    /// <summary>
    /// Resolves <see cref="InputComponent.DefaultFrom"/> against the calculation display
    /// overlay — the same already-formatted scope stat-groups and summary-lists read from, so
    /// a "£20"-style gbp format applies here too if the named field declares one. Only ever
    /// called when there's no saved value yet (see <see cref="BuildInputPayload"/>'s value
    /// chain), so a visitor's own submitted choice always overrides this — it's a default, not
    /// a lock.
    /// </summary>
    private static string? ResolveDefaultFrom(InputComponent input, CalculationRenderContext? calc)
    {
        if (string.IsNullOrWhiteSpace(input.DefaultFrom) || calc is null)
        {
            return null;
        }

        return calc.DisplayValues.TryGetValue(input.DefaultFrom, out var value)
            ? value?.ToString()
            : null;
    }

    // ─── Gateway helpers ──────────────────────────────────────────────────────

    protected static WorkflowGatewayDefinition? FindGateway(WorkflowDefinitionFile definition, string nodeKey) =>
        GetGateways(definition).FirstOrDefault(g =>
            string.Equals(g.Key, nodeKey, StringComparison.Ordinal));

    protected WorkflowResponseEnvelope HandleSplitGatewayAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowTransitionFile arrivingTransition,
        WorkflowGatewayDefinition splitGateway,
        Dictionary<string, object?>? fieldValues,
        WorkflowAccessProfile accessProfile)
    {
        // Find all outgoing branches from the split gateway.
        // Split gateway transitions carry the action "split-auto" by convention or any action
        // — we follow ALL outgoing transitions from the gateway deterministically.
        var outgoing = GetOutgoingTransitions(definition, splitGateway.Key)
            .Where(transition => string.Equals(transition.Action, arrivingTransition.Action, StringComparison.Ordinal)
                || string.Equals(transition.Action, "split-auto", StringComparison.Ordinal))
            .OrderBy(transition => transition.ToState, StringComparer.Ordinal)
            .ToList();

        if (outgoing.Count == 0)
        {
            outgoing = GetOutgoingTransitions(definition, splitGateway.Key)
                .OrderBy(transition => transition.ToState, StringComparer.Ordinal)
                .ToList();
        }

        if (outgoing.Count == 0)
        {
            return ErrorEnvelope(
                $"Split gateway '{splitGateway.Key}' has no outgoing transitions.",
                "GATEWAY_NO_OUTGOING");
        }

        // Identify the cursor being advanced (if we are already in multi-cursor mode).
        var sourceCursor = instance.Cursors
            .FirstOrDefault(c => c.CurrentNodeKey == arrivingTransition.FromState && !c.IsAtGateway);
        var sourceCursorId = sourceCursor?.CursorId;

        // Remove the arriving cursor (or primary state in single-cursor mode) and fan out.
        var remainingCursors = sourceCursorId != null
            ? instance.Cursors.Where(c => c.CursorId != sourceCursorId).ToList()
            : new List<WorkflowCursor>();

        var newCursors = outgoing.Select(t =>
        {
            var targetGateway = FindGateway(definition, t.ToState);
            var targetQueueKey = FirstNonEmpty(
                string.Equals(targetGateway?.GatewayType, "Join", StringComparison.OrdinalIgnoreCase)
                    ? sourceCursor?.QueueKey
                    : targetGateway?.QueueKey,
                GetQueueKey(definition.States.FirstOrDefault(state => state.StateKey == t.ToState)),
                sourceCursor?.QueueKey,
                splitGateway.QueueKey);

            return new WorkflowCursor
            {
                CursorId = Guid.NewGuid().ToString(),
                QueueKey = targetQueueKey ?? string.Empty,
                CurrentNodeKey = t.ToState,
                IsAtGateway = targetGateway != null
            };
        }).ToList();

        var allCursors = remainingCursors.Concat(newCursors).ToArray();
        var primaryState = FirstActiveStageCursorKey(allCursors) ?? newCursors[0].CurrentNodeKey;
        var joinArrivals = new Dictionary<string, IReadOnlyList<string>>(instance.JoinArrivals);

        foreach (var joinGroup in newCursors
                     .Where(cursor => cursor.IsAtGateway)
                     .GroupBy(cursor => cursor.CurrentNodeKey, StringComparer.Ordinal))
        {
            var gateway = FindGateway(definition, joinGroup.Key);
            if (!string.Equals(gateway?.GatewayType, "Join", StringComparison.Ordinal))
                continue;

            var existingArrivals = joinArrivals.TryGetValue(joinGroup.Key, out var existing)
                ? existing.ToList()
                : new List<string>();

            foreach (var cursorId in joinGroup.Select(cursor => cursor.CursorId))
            {
                if (!existingArrivals.Contains(cursorId))
                    existingArrivals.Add(cursorId);
            }

            joinArrivals[joinGroup.Key] = existingArrivals;
        }

        var updated = instance with
        {
            CurrentState = primaryState,
            Cursors = allCursors,
            JoinArrivals = joinArrivals,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        foreach (var joinKey in newCursors
                     .Where(cursor => cursor.IsAtGateway)
                     .Select(cursor => cursor.CurrentNodeKey)
                     .Distinct(StringComparer.Ordinal))
        {
            var joinGateway = FindGateway(definition, joinKey);
            if (joinGateway is null)
            {
                continue;
            }

            if (TryReleaseJoinIfReady(updated, definition, joinGateway, accessProfile) is { } released)
            {
                return released;
            }
        }

        SaveInstance(updated);
        Logger.LogInformation(
            "Split gateway '{Gateway}': instance {Id} fanned out to {Count} cursors.",
            splitGateway.Key, instance.InstanceId, newCursors.Count);

        return BuildEnvelope(updated, definition, accessProfile, allowFallbackWhenHidden: true);
    }

    protected WorkflowResponseEnvelope HandleJoinGatewayAdvance(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowTransitionFile arrivingTransition,
        WorkflowGatewayDefinition joinGateway,
        Dictionary<string, object?>? fieldValues,
        WorkflowAccessProfile accessProfile)
    {
        var gatewayKey = joinGateway.Key;
        var requiredQueues = joinGateway.RequiredIncomingQueues ?? [];

        // Identify the arriving cursor.
        var arrivingCursor = instance.Cursors.Count > 0
            ? instance.Cursors.FirstOrDefault(c => c.CurrentNodeKey == arrivingTransition.FromState && !c.IsAtGateway)
            : new WorkflowCursor
            {
                CursorId = Guid.NewGuid().ToString(),
                QueueKey = FirstNonEmpty(
                               GetQueueKey(definition.States.FirstOrDefault(state => state.StateKey == arrivingTransition.FromState)),
                               joinGateway.QueueKey)
                           ?? string.Empty,
                CurrentNodeKey = arrivingTransition.FromState,
                IsAtGateway = false
            };

        var arrivingCursorId = arrivingCursor?.CursorId ?? Guid.NewGuid().ToString();
        var arrivingQueueKey = FirstNonEmpty(arrivingCursor?.QueueKey, joinGateway.QueueKey) ?? string.Empty;

        // Record arrival in join token bookkeeping.
        var existingArrivals = instance.JoinArrivals.TryGetValue(gatewayKey, out var existing)
            ? existing.ToList()
            : new List<string>();

        if (!existingArrivals.Contains(arrivingCursorId))
            existingArrivals.Add(arrivingCursorId);

        // Move the arriving cursor to the join gateway.
        var cursorsAfterArrival = instance.Cursors.Count > 0
            ? MoveCursor(instance.Cursors, arrivingCursor?.CursorId, gatewayKey, isAtGateway: true)
            : [new WorkflowCursor { CursorId = arrivingCursorId, QueueKey = arrivingQueueKey, CurrentNodeKey = gatewayKey, IsAtGateway = true }];

        var updatedArrivals = new Dictionary<string, IReadOnlyList<string>>(instance.JoinArrivals)
        {
            [gatewayKey] = existingArrivals
        };

        // Check if all required queues have a cursor at this gateway.
        var arrivedCursorIds = new HashSet<string>(existingArrivals, StringComparer.Ordinal);
        var arrivedQueues = cursorsAfterArrival
            .Where(c => c.IsAtGateway && string.Equals(c.CurrentNodeKey, gatewayKey, StringComparison.Ordinal)
                                      && arrivedCursorIds.Contains(c.CursorId))
            .Select(c => c.QueueKey)
            .ToHashSet(StringComparer.Ordinal);

        var allRequiredArrived = requiredQueues.Count == 0 || requiredQueues.All(queue => arrivedQueues.Contains(queue));

        if (!allRequiredArrived)
        {
            // Waiting — record arrival but do not release.
            var waitingInstance = instance with
            {
                CurrentState = FirstActiveStageCursorKey(cursorsAfterArrival) ?? gatewayKey,
                Cursors = cursorsAfterArrival,
                JoinArrivals = updatedArrivals,
                StateVersion = instance.StateVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                FieldValues = Merge(instance.FieldValues, fieldValues)
            };

            SaveInstance(waitingInstance);
            Logger.LogInformation(
                "Join gateway '{Gateway}': instance {Id} waiting ({Arrived}/{Required} queues).",
                gatewayKey, instance.InstanceId, arrivedQueues.Count, requiredQueues.Count);

            return BuildJoinWaitingEnvelope(waitingInstance, definition, joinGateway);
        }

        var arrivedInstance = instance with
        {
            CurrentState = FirstActiveStageCursorKey(cursorsAfterArrival) ?? gatewayKey,
            Cursors = cursorsAfterArrival,
            JoinArrivals = updatedArrivals,
            StateVersion = instance.StateVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldValues = Merge(instance.FieldValues, fieldValues)
        };

        return TryReleaseJoinIfReady(arrivedInstance, definition, joinGateway, accessProfile)
               ?? BuildJoinWaitingEnvelope(arrivedInstance, definition, joinGateway);
    }

    protected WorkflowResponseEnvelope BuildJoinWaitingEnvelope(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowGatewayDefinition joinGateway)
    {
        var waitingContent = joinGateway.WaitingContent
                             ?? "Please wait while other parts of this workflow are completed.";
        var pollMs = joinGateway.WaitingPollIntervalMs > 0 ? joinGateway.WaitingPollIntervalMs : 3000;
        var expectedSeconds = joinGateway.WaitingExpectedSeconds > 0 ? joinGateway.WaitingExpectedSeconds : 30;
        var allowDefer = joinGateway.WaitingDeferMessage is not null || joinGateway.WaitingAllowDefer;

        var waitingArrivals = instance.JoinArrivals.TryGetValue(joinGateway.Key, out var arr) ? arr : [];
        var requiredQueues = joinGateway.RequiredIncomingQueues ?? [];
        var pendingQueues = requiredQueues
            .Where(queue => instance.Cursors.All(c =>
                !(c.IsAtGateway
                  && string.Equals(c.CurrentNodeKey, joinGateway.Key, StringComparison.Ordinal)
                  && string.Equals(c.QueueKey, queue, StringComparison.Ordinal))))
            .ToArray();

        var statusContent = pendingQueues.Length > 0
            ? $"{waitingContent} Waiting for: {string.Join(", ", pendingQueues)}."
            : waitingContent;

        var render = new StepContent
        {
            StepType = "status-timeline",
            StateDisplayName = joinGateway.DisplayName,
            Components =
            [
                new PrismComponentRenderPayload
                {
                    Type = "waiting",
                    Content = statusContent,
                    ExpectedWaitSeconds = expectedSeconds,
                    PollIntervalMs = pollMs,
                    AllowDefer = allowDefer,
                    DeferMessage = joinGateway.WaitingDeferMessage
                }
            ],
            AvailableActions = Array.Empty<WorkflowAction>()
        };

        return new WorkflowResponseEnvelope
        {
            InstanceId = instance.InstanceId,
            ResponseState = "defer",
            StateVersion = instance.StateVersion,
            CorrelationId = instance.InstanceId,
            ServerTimeUtc = DateTimeOffset.UtcNow,
            PollAfterMs = pollMs,
            Render = render,
            InstancePolicy = definition.InstancePolicy
        };
    }

    private WorkflowResponseEnvelope? TryReleaseJoinIfReady(
        WorkflowInstanceState instance,
        WorkflowDefinitionFile definition,
        WorkflowGatewayDefinition joinGateway,
        WorkflowAccessProfile accessProfile)
    {
        var gatewayKey = joinGateway.Key;
        var requiredQueues = joinGateway.RequiredIncomingQueues ?? [];
        var arrivedCursorIds = instance.JoinArrivals.TryGetValue(gatewayKey, out var arrivals)
            ? new HashSet<string>(arrivals, StringComparer.Ordinal)
            : [];
        var arrivedQueues = instance.Cursors
            .Where(cursor => cursor.IsAtGateway
                && string.Equals(cursor.CurrentNodeKey, gatewayKey, StringComparison.Ordinal)
                && arrivedCursorIds.Contains(cursor.CursorId))
            .Select(cursor => cursor.QueueKey)
            .ToHashSet(StringComparer.Ordinal);

        if (requiredQueues.Count > 0 && !requiredQueues.All(queue => arrivedQueues.Contains(queue)))
        {
            return null;
        }

        var outgoing = GetOutgoingTransitions(definition, gatewayKey)
            .OrderBy(transition => transition.ToState, StringComparer.Ordinal)
            .ToList();

        if (outgoing.Count == 0)
        {
            return ErrorEnvelope(
                $"Join gateway '{gatewayKey}' has no outgoing transitions.",
                "GATEWAY_NO_OUTGOING");
        }

        var cursorsWithoutJoin = instance.Cursors
            .Where(cursor => !(cursor.IsAtGateway && string.Equals(cursor.CurrentNodeKey, gatewayKey, StringComparison.Ordinal)))
            .ToList();

        var releaseCursors = outgoing.Select(transition =>
        {
            var targetGateway = FindGateway(definition, transition.ToState);
            return new WorkflowCursor
            {
                CursorId = Guid.NewGuid().ToString(),
                QueueKey = FirstNonEmpty(
                               targetGateway?.QueueKey,
                               GetQueueKey(definition.States.FirstOrDefault(state => state.StateKey == transition.ToState)),
                               joinGateway.QueueKey)
                           ?? string.Empty,
                CurrentNodeKey = transition.ToState,
                IsAtGateway = targetGateway != null
            };
        }).ToList();

        var releasedCursors = cursorsWithoutJoin.Concat(releaseCursors).ToArray();
        var cleanedArrivals = new Dictionary<string, IReadOnlyList<string>>(instance.JoinArrivals);
        cleanedArrivals.Remove(gatewayKey);

        var releasedInstance = instance with
        {
            CurrentState = FirstActiveStageCursorKey(releasedCursors) ?? outgoing[0].ToState,
            Cursors = releasedCursors,
            JoinArrivals = cleanedArrivals
        };

        SaveInstance(releasedInstance);
        return BuildEnvelope(releasedInstance, definition, accessProfile, allowFallbackWhenHidden: true);
    }

    private static IReadOnlyList<WorkflowCursor> MoveCursor(
        IReadOnlyList<WorkflowCursor> cursors,
        string? cursorId,
        string newNodeKey,
        bool isAtGateway)
    {
        if (cursorId == null)
            return cursors;

        return cursors
            .Select(c => c.CursorId == cursorId
                ? c with { CurrentNodeKey = newNodeKey, IsAtGateway = isAtGateway }
                : c)
            .ToArray();
    }

    private static string? FirstActiveStageCursorKey(IReadOnlyList<WorkflowCursor> cursors) =>
        cursors.FirstOrDefault(c => !c.IsAtGateway)?.CurrentNodeKey;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    // ─── end Gateway helpers ──────────────────────────────────────────────────

    private static bool IsTerminalInstance(WorkflowInstanceState instance, WorkflowDefinitionFile definition)
    {
        var currentState = definition.States.FirstOrDefault(s => s.StateKey == instance.CurrentState);
        return currentState != null && currentState.Components.InferStepType() == "confirmation";
    }

    private WorkflowInstanceState? FindLatestInstance(string tenantId, string userId, string workflowKey) =>
        _instanceStore.GetAll()
            .Where(instance =>
                string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal)
                && string.Equals(instance.UserId, userId, StringComparison.Ordinal)
                && string.Equals(instance.WorkflowKey, workflowKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(instance => instance.UpdatedAt)
            .ThenByDescending(instance => instance.CreatedAt)
            .FirstOrDefault();

    private static Dictionary<string, object?> Merge(
        Dictionary<string, object?> existing,
        Dictionary<string, object?>? incoming)
    {
        if (incoming == null || incoming.Count == 0)
        {
            return existing;
        }

        var merged = new Dictionary<string, object?>(existing);
        foreach (var kvp in incoming)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static string ActionLabel(string key) => key switch
    {
        "submit" => "Submit",
        "save-draft" => "Save Draft",
        "start-another" => "Start Another",
        "approve" => "Approve",
        "request-changes" => "Request Changes",
        "continue" => "Continue",
        _ => key
    };

    private static string ActionStyle(string key) => key switch
    {
        "submit" or "approve" => "primary",
        "reject" or "cancel" => "destructive",
        _ => "secondary"
    };
}
