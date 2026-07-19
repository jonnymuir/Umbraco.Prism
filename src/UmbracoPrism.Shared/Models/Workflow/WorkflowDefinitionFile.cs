using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Extensions;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Shared.Models.Workflow;

/// <summary>
/// Persisted workflow definition contract shared by authoring, seed files and runtime loading.
/// </summary>
public record WorkflowDefinitionFile
{
    /// <summary>
    /// Validates that every state route targets a gateway, never another state directly, that
    /// every gateway has a non-empty <c>key</c> (a keyless gateway can never be a valid route
    /// target — the engine resolves targets by key, so it would silently be unreachable), and
    /// that every route's <c>target</c> actually resolves to an existing gateway (for routes
    /// from a state) or state/gateway (for routes from a gateway) — a target that matches
    /// nothing is a dangling reference the engine can't route, and would only surface at
    /// runtime as an opaque "access denied" once a real user reached it.
    /// Gateway routes may target either states or gateways.
    /// Returns one diagnostic per violation; empty list means the workflow is valid.
    /// </summary>
    public IReadOnlyList<WorkflowDiagnostic> ValidateGatewayRouting()
    {
        var stateKeys = States
            .Where(s => !string.IsNullOrWhiteSpace(s.StateKey))
            .Select(s => s.StateKey)
            .ToHashSet(StringComparer.Ordinal);
        var gatewayKeys = (Gateways ?? [])
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var diagnostics = new List<WorkflowDiagnostic>();
        var gateways = Gateways ?? [];

        var gatewayIndex = 0;
        foreach (var gateway in gateways)
        {
            if (string.IsNullOrWhiteSpace(gateway.Key))
            {
                diagnostics.Add(new WorkflowDiagnostic(
                    "GATEWAY_MISSING_KEY",
                    $"gateways[{gatewayIndex}].key",
                    $"Gateway '{gateway.DisplayName}' has no key. The engine resolves route targets " +
                    "by key, so a keyless gateway can never be reached — give it a unique key."));
            }

            if (string.IsNullOrWhiteSpace(gateway.GatewayType))
            {
                // Not a runtime break — Advance() treats anything that isn't exactly "Split" as a
                // Join, so routing through a blank-typed gateway still works for the common single
                // in/single out case. But it IS an authoring-clarity gap (a reader can't tell fan
                // out from pass-through at a glance), so warn rather than stay silent.
                diagnostics.Add(new WorkflowDiagnostic(
                    "GATEWAY_MISSING_TYPE",
                    $"gateways[{gatewayIndex}].gatewayType",
                    $"Gateway '{gateway.Key}' has no gatewayType. It still routes correctly (anything " +
                    "other than \"Split\" behaves as a Join), but set it explicitly — \"Split\" for a " +
                    "fan-out, \"Join\" for a merge or plain pass-through — so the shape is clear from " +
                    "the definition alone.",
                    WorkflowDiagnosticSeverity.Warning));
            }

            if (string.IsNullOrWhiteSpace(gateway.QueueKey))
            {
                // Also not a runtime break for the common case — but the editor canvas visually
                // groups stages and gateways into lanes by queue, so a blank queue here renders the
                // gateway in its own separate lane even when every stage it connects shares one
                // queue, reading as "this got put in a different queue" even though nothing at
                // runtime actually treats it that way.
                diagnostics.Add(new WorkflowDiagnostic(
                    "GATEWAY_MISSING_QUEUE",
                    $"gateways[{gatewayIndex}].queueKey",
                    $"Gateway '{gateway.Key}' has no queueKey. Set it to match the queue of the " +
                    "stage(s) that route into it — otherwise the canvas renders it in its own lane, " +
                    "visually separate from a workflow that's actually all in one queue.",
                    WorkflowDiagnosticSeverity.Warning));
            }

            gatewayIndex++;
        }

        foreach (var state in States)
        {
            var routeIndex = 0;
            foreach (var route in state.Routes ?? [])
            {
                if (string.IsNullOrWhiteSpace(route.Target))
                {
                    // Warning, not Error: the visual editor's "add a route" affordance deliberately
                    // supports saving with a route not yet pointed anywhere, mid-edit. But an author
                    // (human or agent) finishing a change should see this before considering the
                    // job done — an empty target left in a "final" save is unreachable at runtime.
                    diagnostics.Add(new WorkflowDiagnostic(
                        "ROUTE_TARGET_EMPTY",
                        $"states.{state.StateKey}.routes[{routeIndex}]",
                        $"State '{state.StateKey}' route '{route.Id}' has no target — it doesn't go " +
                        "anywhere yet. Fine mid-edit; if this workflow is meant to be complete, wire it " +
                        "to a gateway before finishing.",
                        WorkflowDiagnosticSeverity.Warning));
                }
                else if (stateKeys.Contains(route.Target))
                {
                    diagnostics.Add(new WorkflowDiagnostic(
                        "GATEWAY_ROUTE_TARGETS_STATE",
                        $"states.{state.StateKey}.routes[{routeIndex}]",
                        $"State '{state.StateKey}' route '{route.Id}' targets state '{route.Target}' directly. " +
                        "Routes from states must always target a gateway."));
                }
                else if (!gatewayKeys.Contains(route.Target))
                {
                    diagnostics.Add(new WorkflowDiagnostic(
                        "ROUTE_TARGET_NOT_FOUND",
                        $"states.{state.StateKey}.routes[{routeIndex}]",
                        $"State '{state.StateKey}' route '{route.Id}' targets '{route.Target}', which is not " +
                        "any gateway's key in this workflow. Routes from states must target an existing gateway."));
                }

                if (string.IsNullOrWhiteSpace(route.Trigger))
                {
                    // Warning, not Error: the engine now defaults a blank trigger to "continue" at
                    // render time, so this no longer breaks the workflow — but a generic
                    // "Continue" button is rarely what an author actually wants on a human-facing
                    // stage, so it's worth flagging rather than passing silently.
                    diagnostics.Add(new WorkflowDiagnostic(
                        "ROUTE_TRIGGER_EMPTY",
                        $"states.{state.StateKey}.routes[{routeIndex}]",
                        $"State '{state.StateKey}' route '{route.Id}' has no trigger — it will render as a " +
                        "generic \"Continue\" button. Give it a specific trigger (e.g. \"continue\", \"submit\") " +
                        "and label if you want more intentional wording.",
                        WorkflowDiagnosticSeverity.Warning));
                }

                routeIndex++;
            }
        }

        gatewayIndex = 0;
        foreach (var gateway in gateways)
        {
            // A gateway with zero outgoing routes is a dead end: WorkflowRuntimeEngine's own
            // BuildJoinWaitingEnvelope hard-fails at runtime with GATEWAY_NO_OUTGOING the moment an
            // instance actually reaches it. Reproduced live — an agent-authored gateway saved
            // cleanly with an empty routes array (nothing above checks for *zero* routes, only that
            // each existing route's own target is valid), and the very first real submission that
            // reached it broke with that runtime error. Catch it at design time instead.
            if ((gateway.Routes ?? []).Count == 0)
            {
                diagnostics.Add(new WorkflowDiagnostic(
                    "GATEWAY_NO_OUTGOING_ROUTES",
                    $"gateways[{gatewayIndex}].routes",
                    $"Gateway '{gateway.Key}' has no outgoing routes — any instance that reaches it " +
                    "will hard-fail at runtime (GATEWAY_NO_OUTGOING). Add at least one route to a " +
                    "state or another gateway."));
            }

            var routeIndex = 0;
            foreach (var route in gateway.Routes ?? [])
            {
                if (string.IsNullOrWhiteSpace(route.Target))
                {
                    diagnostics.Add(new WorkflowDiagnostic(
                        "ROUTE_TARGET_EMPTY",
                        $"gateways[{gatewayIndex}].routes[{routeIndex}]",
                        $"Gateway '{gateway.Key}' route '{route.Id}' has no target — it doesn't go " +
                        "anywhere yet. Fine mid-edit; if this workflow is meant to be complete, wire it " +
                        "to a state or gateway before finishing.",
                        WorkflowDiagnosticSeverity.Warning));
                }
                else if (!stateKeys.Contains(route.Target) && !gatewayKeys.Contains(route.Target))
                {
                    diagnostics.Add(new WorkflowDiagnostic(
                        "ROUTE_TARGET_NOT_FOUND",
                        $"gateways[{gatewayIndex}].routes[{routeIndex}]",
                        $"Gateway '{gateway.Key}' route '{route.Id}' targets '{route.Target}', which is not " +
                        "any state or gateway key in this workflow."));
                }

                routeIndex++;
            }

            gatewayIndex++;
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that every state and gateway can eventually reach a terminal state (one with no
    /// outgoing routes) via *some* path — not that every path does, so a deliberate self-loop (e.g.
    /// money-modeller's <c>recalculate</c> route back to <c>model</c>) is fine as long as another
    /// route out of the same state still leads somewhere. Reproduced live: an agent-authored
    /// "request more info" gateway that only ever routed within the requesting queue, with no path
    /// back to a state where the other queue's actor could actually supply what was requested —
    /// <see cref="ValidateGatewayRouting"/> passed (every gateway had outgoing routes, every target
    /// resolved) but any real instance that took that branch could never complete. This check
    /// doesn't understand *why* a path is a dead end — that's a service-design judgement call it
    /// can't make — only that one exists structurally.
    /// Returns one diagnostic per state or gateway that can never reach a terminal state; empty
    /// list means every node can eventually complete.
    /// </summary>
    public IReadOnlyList<WorkflowDiagnostic> ValidateReachability()
    {
        var gateways = Gateways ?? [];
        var stateKeys = States
            .Where(s => !string.IsNullOrWhiteSpace(s.StateKey))
            .Select(s => s.StateKey)
            .ToHashSet(StringComparer.Ordinal);
        var gatewayKeys = gateways
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        // Predecessor map built only from routes that resolve to a real node — a dangling target
        // is already reported by ValidateGatewayRouting, so it's silently skipped here rather than
        // double-reported as an unreachable dead end too.
        var predecessors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void AddEdge(string from, string to)
        {
            if (!predecessors.TryGetValue(to, out var list))
            {
                list = [];
                predecessors[to] = list;
            }

            list.Add(from);
        }

        foreach (var state in States)
        {
            if (string.IsNullOrWhiteSpace(state.StateKey))
            {
                continue;
            }

            foreach (var route in state.Routes ?? [])
            {
                if (gatewayKeys.Contains(route.Target))
                {
                    AddEdge(state.StateKey, route.Target);
                }
            }
        }

        foreach (var gateway in gateways)
        {
            if (string.IsNullOrWhiteSpace(gateway.Key))
            {
                continue;
            }

            foreach (var route in gateway.Routes ?? [])
            {
                if (stateKeys.Contains(route.Target) || gatewayKeys.Contains(route.Target))
                {
                    AddEdge(gateway.Key, route.Target);
                }
            }
        }

        var terminalStates = States
            .Where(s => !string.IsNullOrWhiteSpace(s.StateKey) && (s.Routes ?? []).Count == 0)
            .Select(s => s.StateKey)
            .ToList();

        var reachable = new HashSet<string>(terminalStates, StringComparer.Ordinal);
        var queue = new Queue<string>(terminalStates);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!predecessors.TryGetValue(current, out var preds))
            {
                continue;
            }

            foreach (var predecessor in preds)
            {
                if (reachable.Add(predecessor))
                {
                    queue.Enqueue(predecessor);
                }
            }
        }

        var diagnostics = new List<WorkflowDiagnostic>();

        var gatewayIndex = 0;
        foreach (var gateway in gateways)
        {
            if (!string.IsNullOrWhiteSpace(gateway.Key) && !reachable.Contains(gateway.Key))
            {
                diagnostics.Add(new WorkflowDiagnostic(
                    "GATEWAY_UNREACHABLE_TERMINAL",
                    $"gateways[{gatewayIndex}]",
                    $"Gateway '{gateway.Key}' can never reach a completed state — every path leaving " +
                    "it eventually loops back without an exit. If this is a deliberate wait/retry " +
                    "loop, add a route somewhere in the loop that leads onward to a state with no " +
                    "outgoing routes."));
            }

            gatewayIndex++;
        }

        foreach (var state in States)
        {
            if (!string.IsNullOrWhiteSpace(state.StateKey) &&
                (state.Routes ?? []).Count > 0 &&
                !reachable.Contains(state.StateKey))
            {
                diagnostics.Add(new WorkflowDiagnostic(
                    "STATE_UNREACHABLE_TERMINAL",
                    $"states.{state.StateKey}",
                    $"State '{state.StateKey}' can never reach a completed state — every route out of " +
                    "it eventually loops back without an exit."));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates that every <see cref="StatGroupComponent"/> item and <see cref="ChartComponent"/>
    /// binds to a field or series that actually exists — either a calculated field/series, or (for
    /// stat-group only) an input component's own <c>fieldKey</c> captured earlier in the workflow.
    /// Catches the easy authoring mistake of adding a display component whose binding was never
    /// wired to the <c>calculations</c> block (or the block itself was never added), which would
    /// otherwise render silently blank with no error anywhere.
    /// Returns one diagnostic per dangling binding; empty list means every binding resolves.
    /// </summary>
    public IReadOnlyList<WorkflowDiagnostic> ValidateDataDisplayBindings()
    {
        var calculatedFieldNames = Calculations?.Fields.Keys.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var calculatedSeriesNames = Calculations?.Series?.Keys.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var inputFieldKeys = States
            .SelectMany(s => s.Components.FlattenWithPaths(""))
            .Select(c => c.Component)
            .OfType<InputComponent>()
            .Select(c => c.FieldKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
        var stateKeys = States.Select(s => s.StateKey).ToHashSet(StringComparer.Ordinal);

        var diagnostics = new List<WorkflowDiagnostic>();

        foreach (var state in States)
        {
            foreach (var (component, path) in state.Components.FlattenWithPaths($"states.{state.StateKey}.components"))
            {
                switch (component)
                {
                    case StatGroupComponent statGroup:
                        if (statGroup.Items.Count == 0)
                        {
                            diagnostics.Add(new WorkflowDiagnostic(
                                "DATA_DISPLAY_NO_ITEMS",
                                $"{path}.items",
                                $"stat-group '{statGroup.Title}' has no items — it will render nothing. " +
                                "Add at least one item bound to a captured input or calculations.fields entry."));
                        }

                        var itemIndex = 0;
                        foreach (var item in statGroup.Items)
                        {
                            if (string.IsNullOrWhiteSpace(item.FieldKey))
                            {
                                // Distinct from DATA_DISPLAY_UNKNOWN_FIELD below: this isn't a typo pointing
                                // at the wrong name, it's not pointing anywhere at all — a real regression
                                // seen in practice (an agent wired the calculation but left the display
                                // component's binding blank), and one the old "only check non-empty keys"
                                // logic silently let through.
                                diagnostics.Add(new WorkflowDiagnostic(
                                    "DATA_DISPLAY_MISSING_FIELD",
                                    $"{path}.items[{itemIndex}].fieldKey",
                                    $"stat-group item '{item.Label}' has no fieldKey — it can never bind to " +
                                    "anything and will always render its empty-value placeholder. Set it to a " +
                                    "captured input's fieldKey or a calculations.fields entry."));
                            }
                            else if (!calculatedFieldNames.Contains(item.FieldKey) &&
                                !inputFieldKeys.Contains(item.FieldKey))
                            {
                                diagnostics.Add(new WorkflowDiagnostic(
                                    "DATA_DISPLAY_UNKNOWN_FIELD",
                                    $"{path}.items[{itemIndex}].fieldKey",
                                    $"stat-group item '{item.Label}' binds to field '{item.FieldKey}', which is " +
                                    "neither a captured input field nor a calculations.fields entry. Either add " +
                                    $"'{item.FieldKey}' to the workflow's calculations block, or fix the fieldKey."));
                            }

                            itemIndex++;
                        }

                        break;

                    case SummaryListComponent summaryList:
                        if (summaryList.Children.Count == 0)
                        {
                            diagnostics.Add(new WorkflowDiagnostic(
                                "DATA_DISPLAY_NO_ITEMS",
                                $"{path}.children",
                                $"summary-list '{summaryList.Title}' has no children — it will render nothing. " +
                                "Add at least one child bound to a captured input or calculations.fields entry."));
                        }

                        if (!string.IsNullOrWhiteSpace(summaryList.ChangeStateKey) &&
                            !stateKeys.Contains(summaryList.ChangeStateKey))
                        {
                            diagnostics.Add(new WorkflowDiagnostic(
                                "DATA_DISPLAY_UNKNOWN_CHANGE_STATE",
                                $"{path}.changeStateKey",
                                $"summary-list '{summaryList.Title}' changeStateKey '{summaryList.ChangeStateKey}' " +
                                "is not a state in this workflow — its 'Change' link would navigate nowhere. Fix " +
                                "the state key, or remove changeStateKey if there's nothing to change."));
                        }

                        var childIndex = 0;
                        foreach (var child in summaryList.Children.OfType<InputComponent>())
                        {
                            if (string.IsNullOrWhiteSpace(child.FieldKey))
                            {
                                diagnostics.Add(new WorkflowDiagnostic(
                                    "DATA_DISPLAY_MISSING_FIELD",
                                    $"{path}.children[{childIndex}].fieldKey",
                                    $"summary-list child '{child.Label}' has no fieldKey — it can never bind to " +
                                    "anything and will always render its empty-value placeholder. Set it to a " +
                                    "captured input's fieldKey or a calculations.fields entry."));
                            }
                            else if (!calculatedFieldNames.Contains(child.FieldKey) &&
                                !inputFieldKeys.Contains(child.FieldKey))
                            {
                                diagnostics.Add(new WorkflowDiagnostic(
                                    "DATA_DISPLAY_UNKNOWN_FIELD",
                                    $"{path}.children[{childIndex}].fieldKey",
                                    $"summary-list child '{child.Label}' binds to field '{child.FieldKey}', which " +
                                    "is neither a captured input field nor a calculations.fields entry. Either " +
                                    $"add '{child.FieldKey}' to the workflow's calculations block, or fix the " +
                                    "fieldKey."));
                            }

                            // A row's own ChangeStateKey (for summary lists spanning multiple earlier
                            // stages) needs the same dangling-target check as the component-level one.
                            if (!string.IsNullOrWhiteSpace(child.ChangeStateKey) &&
                                !stateKeys.Contains(child.ChangeStateKey))
                            {
                                diagnostics.Add(new WorkflowDiagnostic(
                                    "DATA_DISPLAY_UNKNOWN_CHANGE_STATE",
                                    $"{path}.children[{childIndex}].changeStateKey",
                                    $"summary-list child '{child.Label}' changeStateKey '{child.ChangeStateKey}' " +
                                    "is not a state in this workflow — its 'Change' link would navigate nowhere. " +
                                    "Fix the state key, or remove changeStateKey to fall back to the summary-list's " +
                                    "own changeStateKey."));
                            }

                            childIndex++;
                        }

                        break;

                    case ChartComponent chart:
                        if (string.IsNullOrWhiteSpace(chart.Series))
                        {
                            diagnostics.Add(new WorkflowDiagnostic(
                                "DATA_DISPLAY_MISSING_FIELD",
                                $"{path}.series",
                                $"chart '{chart.Title}' has no series set — it can never bind to anything and " +
                                "will always render empty. Set it to a calculations.series entry."));
                        }
                        else if (!calculatedSeriesNames.Contains(chart.Series))
                        {
                            diagnostics.Add(new WorkflowDiagnostic(
                                "DATA_DISPLAY_UNKNOWN_FIELD",
                                $"{path}.series",
                                $"chart '{chart.Title}' binds to series '{chart.Series}', which is not a " +
                                $"calculations.series entry. Either add '{chart.Series}' to the workflow's " +
                                "calculations block, or fix the series name."));
                        }

                        break;
                }
            }
        }

        if (Calculations is not null)
        {
            foreach (var (name, field) in Calculations.Fields)
            {
                if (string.Equals(field.Source, "service", StringComparison.OrdinalIgnoreCase) &&
                    inputFieldKeys.Contains(name))
                {
                    diagnostics.Add(new WorkflowDiagnostic(
                        "CALC_FIELD_SHADOWS_INPUT",
                        $"calculations.fields.{name}",
                        $"'{name}' is declared source: \"service\" in calculations, but a component in this " +
                        $"workflow already captures user input under fieldKey '{name}' — that value is " +
                        "automatically in the calculation scope already. `source: \"service\"` is for a value " +
                        "an external system supplies (e.g. a lookup a host resolves), never for the user's own " +
                        "submitted input. Remove this calculations entry, or use a different field name."));
                }
            }
        }

        return diagnostics;
    }


    private IReadOnlyList<WorkflowQueueDefinition>? _queues;
    private IReadOnlyList<WorkflowTransitionFile>? _transitions;

    public string DefinitionKey { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public int Version { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SchemaVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? AuthoredWorkflowId { get; init; }

    public string InitialState { get; init; } = "";

    public string InstancePolicy { get; init; } = "single";

    public IReadOnlyList<StepDefinition> States { get; init; } = Array.Empty<StepDefinition>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowQueueDefinition>? Queues
    {
        get => _queues;
        init => _queues = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowGatewayDefinition>? Gateways { get; init; }

    /// <summary>
    /// Declarative calculations for this workflow: tables, computed fields and series
    /// evaluated by <c>CalculationEvaluator</c> against instance field values plus
    /// host-supplied service inputs.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Calculations.WorkflowCalculationSet? Calculations { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowHandoffDefinition>? Handoffs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// Editor-owned canvas layout hints: manually arranged node positions
    /// keyed by prefixed node id (<c>stage:&lt;stateKey&gt;</c> /
    /// <c>gateway:&lt;key&gt;</c>). The runtime never reads this — it exists
    /// so authored arrangements survive the save/load round-trip.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowLayoutDefinition? Layout { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowDefinitionMetadata? Metadata { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowTransitionFile>? Transitions
    {
        get => _transitions;
        init => _transitions = value;
    }

    [JsonIgnore]
    public IReadOnlyList<WorkflowTransitionFile>? LegacyTransitions
    {
        init => _transitions = value;
    }
}

public record StepDefinition
{
    private string? _queueKey;
    private IReadOnlyList<WorkflowRouteDefinition>? _routes;

    public string StateKey { get; init; } = "";

    public string DisplayName { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StageType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Actor { get; init; }

    public string QueueKey
    {
        get => _queueKey ?? string.Empty;
        init => _queueKey = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RoleGates { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }

    public IReadOnlyList<PrismComponent> Components { get; init; } = Array.Empty<PrismComponent>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowRouteDefinition>? Routes
    {
        get => _routes;
        init => _routes = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowStateMetadata? Metadata { get; init; }

    /// <summary>Curated icon-set key (see the client's graph/node-icons.ts). Falls back to a stage-kind default when unset.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; init; }

}

public record WorkflowTransitionFile
{
    public string FromState { get; init; } = "";

    public string ToState { get; init; } = "";

    public string Action { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Style { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiresRole { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowConditionDefinition>? Conditions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowTransitionMetadata? Metadata { get; init; }
}

public record WorkflowQueueDefinition
{
    private string? _key;

    public string Key
    {
        get => _key ?? string.Empty;
        init => _key = value;
    }

    public string DisplayName { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Actor { get; init; }

    [JsonIgnore]
    public string? QueueName
    {
        get => string.IsNullOrWhiteSpace(_key) ? null : _key;
        init => _key = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RoleGates { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

public record WorkflowGatewayDefinition
{
    private string? _queueKey;

    public string Key { get; init; } = "";

    public string DisplayName { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    public string GatewayType { get; init; } = "";

    public string QueueKey
    {
        get => _queueKey ?? string.Empty;
        init => _queueKey = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Actor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RoleGates { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowRouteDefinition>? Routes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WaitingContent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WaitingExpectedSeconds { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WaitingPollIntervalMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WaitingAllowDefer { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WaitingDeferMessage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RequiredIncomingQueues { get; init; }

    [JsonIgnore]
    public string? QueueName
    {
        get => string.IsNullOrWhiteSpace(_queueKey) ? null : _queueKey;
        init => _queueKey = value;
    }

    [JsonIgnore]
    public string? Source { get; init; }

    [JsonPropertyName("queueName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyQueueName
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _queueKey = value;
            }
        }
    }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacySource
    {
        init => Source = value;
    }

    /// <summary>Curated icon-set key (see the client's graph/node-icons.ts). Falls back to a Split/Join default when unset.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; init; }
}

public record WorkflowRouteDefinition
{
    public string Id { get; init; } = "";

    public string Target { get; init; } = "";

    public string Trigger { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Style { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiresRole { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowConditionDefinition>? Conditions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }
}

public record WorkflowDefinitionMetadata
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? AuthoredWorkflowId { get; init; }

    public string? Description { get; init; }

    public string? SchemaVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowGatewayDefinition>? Gateways { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowHandoffDefinition>? Handoffs { get; init; }
}

public record WorkflowHandoffDefinition
{
    public string Id { get; init; } = "";

    public string FromState { get; init; } = "";

    public string ToState { get; init; } = "";

    public string Label { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActorChange { get; init; }
}

public record WorkflowStateMetadata
{
    private string? _queueKey;

    public string? Description { get; init; }

    public string? StageType { get; init; }

    public string? Actor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QueueKey
    {
        get => _queueKey;
        init => _queueKey = value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RoleGates { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }

    [JsonIgnore]
    public string? QueueName
    {
        get => string.IsNullOrWhiteSpace(_queueKey) ? null : _queueKey;
        init => _queueKey = value;
    }

    [JsonPropertyName("queueName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyQueueName
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _queueKey = value;
            }
        }
    }
}

public record WorkflowTransitionMetadata
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowConditionDefinition>? Conditions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }
}

public record WorkflowActionDefinition
{
    public string Type { get; init; } = "";

    public string Timing { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("parameterSchemaKey")]
    public string? ParameterSchemaKey { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("params")]
    public JsonObject Parameters { get; init; } = [];
}

public record WorkflowConditionDefinition
{
    public string Kind { get; init; } = "";

    public string Expression { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

/// <summary>
/// Editor canvas layout hints. Positions are whole flow pixels; queue
/// membership stays authoritative on the states/gateways themselves.
/// </summary>
public record WorkflowLayoutDefinition
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, WorkflowNodePosition>? Nodes { get; init; }
}

public record WorkflowNodePosition
{
    public double X { get; init; }

    public double Y { get; init; }
}
