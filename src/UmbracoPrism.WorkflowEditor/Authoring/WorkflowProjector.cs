using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Deterministic compiler from <see cref="AuthoredWorkflow"/> to <see cref="WorkflowDefinitionFile"/>.
///
/// Pipeline stages:
///   1. Validate   — structural correctness checks; errors block emission.
///   2. Normalise  — sort stages, transitions and fields into canonical order.
///   3. Emit       — build StepDefinitions and WorkflowTransitionFiles from authored graph.
///   4. Checksum   — SHA-256 of canonical UTF-8 JSON (no BOM).
///
/// Determinism contract: identical <see cref="AuthoredWorkflow"/> input → byte-identical output on
/// every invocation, every platform, every .NET version (within the same major). Locked by
/// <c>WorkflowProjectorDeterminismTests</c>.
/// </summary>
public sealed class WorkflowProjector : IWorkflowProjector
{
    private readonly IActionCatalogProvider _actionCatalogProvider;

    public WorkflowProjector()
        : this(new BuiltInActionCatalogProvider())
    {
    }

    public WorkflowProjector(IActionCatalogProvider actionCatalogProvider)
    {
        _actionCatalogProvider = actionCatalogProvider;
    }

    /// <summary>
    /// Canonical serialization options used for both the checksum computation and external consumers
    /// that need to reproduce the projected JSON. Exposed as a public static so tests can verify
    /// byte-identical output without duplicating option configuration.
    /// </summary>
    public static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static byte[] SerializeCanonical(WorkflowDefinitionFile file) =>
        JsonSerializer.SerializeToUtf8Bytes(file, CanonicalOptions);

    public static string ComputeCanonicalChecksum(WorkflowDefinitionFile file)
    {
        var hash = SHA256.HashData(SerializeCanonical(file));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc/>
    public ProjectionResult Project(AuthoredWorkflow authored)
    {
        var diagnostics = new List<ProjectionDiagnostic>();

        // 1. Validate
        Validate(authored, diagnostics);

        // 2. Normalise — sort everything into canonical order before emitting
        var lanesByKey = authored.Lanes.ToDictionary(lane => lane.Key, StringComparer.Ordinal);

        var normalisedStages = authored.Stages
            .OrderBy(s => s.StageKey, StringComparer.Ordinal)
            .ToList();

        // 3. Emit
        var states = normalisedStages
            .Select(s => EmitStage(authored, s, diagnostics, lanesByKey))
            .ToList();

        var transitions = EmitTransitions(authored.Gateways);

        var file = new WorkflowDefinitionFile
        {
            DefinitionKey = authored.DefinitionKey,
            DisplayName = authored.DisplayName,
            Version = authored.Version,
            InitialState = authored.InitialStageKey,
            InstancePolicy = authored.InstancePolicy,
            States = states,
            Transitions = transitions,
            Metadata = EmitWorkflowMetadata(authored)
        };

        // 4. Checksum — SHA-256 of canonical UTF-8 JSON (no BOM)
        var checksum = ComputeCanonicalChecksum(file);

        return new ProjectionResult
        {
            File = file,
            Checksum = checksum,
            Diagnostics = diagnostics
        };
    }

    // ─── Stage 1: Validate ────────────────────────────────────────────────────

    private void Validate(AuthoredWorkflow authored, List<ProjectionDiagnostic> diagnostics)
    {
        AuthoredWorkflowSchemaValidator.Validate(authored, diagnostics, _actionCatalogProvider);

        var stageKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stage in authored.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageKey))
            {
                diagnostics.Add(Error("PROJ001", "A stage has an empty StageKey.", null));
                continue;
            }

            if (!stageKeys.Add(stage.StageKey))
            {
                diagnostics.Add(Error("PROJ002",
                    $"Duplicate StageKey '{stage.StageKey}'. All stage keys must be unique.", stage.StageKey));
            }
        }

        if (!string.IsNullOrWhiteSpace(authored.InitialStageKey) &&
            !stageKeys.Contains(authored.InitialStageKey))
        {
            diagnostics.Add(Error("PROJ003",
                $"InitialStageKey '{authored.InitialStageKey}' does not reference any defined stage.", null));
        }

        // Routes target either stages or gateways — both are valid graph nodes. The detailed
        // per-route checks (target existence, unique triggers, etc) live in the schema validator;
        // here we just emit the basic stage-side diagnostics.
    }

    // ─── Stage 3: Emit ────────────────────────────────────────────────────────

    private static StepDefinition EmitStage(
        AuthoredWorkflow authored,
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics,
        IReadOnlyDictionary<string, AuthoredLane> lanesByKey)
    {
        var components = EmitComponents(authored, stage, diagnostics);

        return new StepDefinition
        {
            StateKey = stage.StageKey,
            DisplayName = stage.DisplayName,
            Components = components,
            Metadata = EmitStateMetadata(stage, lanesByKey)
        };
    }

    private static IReadOnlyList<PrismComponent> EmitComponents(
        AuthoredWorkflow authored,
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        // Authored components are the source of truth — pass through untouched so authors
        // express their stages as a real component tree (fieldset + legend, body, inset-text,
        // panel, summary-list, etc.). When a stage declares no components, fall back to a
        // sensible kind-based default so empty stages still render as the right shell.
        if (stage.Components.Count > 0)
            return stage.Components;

        return stage.Kind switch
        {
            StageKind.Question => [new FieldsetComponent()],
            StageKind.CheckAnswers => EmitDefaultCheckAnswersSummary(authored),
            StageKind.Confirmation => EmitDefaultConfirmation(stage),
            StageKind.TaskList => [new TaskListComponent()],
            _ => EmitUnknownKind(stage, diagnostics)
        };
    }

    /// <summary>
    /// Default summary list emitted only when a CheckAnswers stage declares no components
    /// of its own. Authors are expected to place their own <see cref="SummaryListComponent"/>
    /// (with the inputs they want summarised) on the stage; this fallback walks the workflow's
    /// question stages so legacy fixtures without authored components still get a shell that
    /// infers as "check-answers".
    /// </summary>
    private static IReadOnlyList<PrismComponent> EmitDefaultCheckAnswersSummary(AuthoredWorkflow authored)
    {
        var inputs = authored.Stages
            .Where(s => s.Kind == StageKind.Question)
            .OrderBy(s => s.StageKey, StringComparer.Ordinal)
            .SelectMany(s => HarvestInputs(s.Components))
            .ToList();

        return [new SummaryListComponent { Children = inputs }];
    }

    private static IReadOnlyList<PrismComponent> EmitDefaultConfirmation(AuthoredStage stage)
    {
        var components = new List<PrismComponent>
        {
            new PanelComponent { Heading = stage.DisplayName }
        };

        if (!string.IsNullOrWhiteSpace(stage.Description))
        {
            components.Add(new BodyComponent { Content = stage.Description });
        }

        return components;
    }

    private static IEnumerable<PrismComponent> HarvestInputs(IEnumerable<PrismComponent> components)
    {
        foreach (var component in components)
        {
            switch (component)
            {
                case InputComponent input:
                    yield return input;
                    break;
                case FieldsetComponent fieldset:
                    foreach (var nested in HarvestInputs(fieldset.Children))
                        yield return nested;
                    break;
                case AccordionComponent accordion:
                    foreach (var section in accordion.Sections)
                        foreach (var nested in HarvestInputs(section.Children))
                            yield return nested;
                    break;
            }
        }
    }

    private static IReadOnlyList<PrismComponent> EmitUnknownKind(
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        diagnostics.Add(Warning("PROJ005",
            $"Unknown StageKind value for stage '{stage.StageKey}'. Defaulting to question shell.",
            stage.StageKey));

        return [new FieldsetComponent()];
    }

    private static WorkflowTransitionFile EmitTransition(AuthoredGateway gateway, AuthoredRoute route) =>
        new()
        {
            FromState = gateway.Source,
            ToState = route.Target,
            Action = route.Trigger,
            RequiresRole = route.RequiresRole,
            Metadata = EmitTransitionMetadata(route)
        };

    /// <summary>
    /// Builds the runtime transition graph from the authored gateways.
    ///
    /// Gateway emission rules:
    ///   - Parallel-fork Split (≥2 routes that all share one trigger): emit the gateway key as a
    ///     real node so the engine's <c>HandleSplitGatewayAdvance</c> fans out. Shape:
    ///     <c>source → gatewayKey [trigger]</c> + one <c>gatewayKey → routeTarget [split-auto]</c> per route.
    ///   - Exclusive-choice Split (routes with distinct triggers) or single-route Split:
    ///     flatten to <c>source → routeTarget [trigger]</c>. Distinct triggers carry XOR
    ///     semantics — chaining them would silently convert XOR into a parallel fork.
    ///   - Join: emit each outgoing route as <c>gatewayKey → routeTarget [trigger]</c> so the
    ///     engine can release the join after all required incoming lanes have arrived.
    ///
    /// All transitions are sorted by (FromState, ToState, Action) for deterministic output.
    /// </summary>
    private static List<WorkflowTransitionFile> EmitTransitions(IReadOnlyList<AuthoredGateway> gateways)
    {
        var transitions = new List<WorkflowTransitionFile>();

        foreach (var gateway in gateways)
        {
            if (gateway.Routes.Count == 0)
                continue;

            if (gateway.Kind == GatewayKind.Join)
            {
                // Join: emit outgoing edges from the gateway key so the engine can release
                // once all required incoming lanes have arrived.
                foreach (var route in gateway.Routes)
                {
                    transitions.Add(new WorkflowTransitionFile
                    {
                        FromState = gateway.GatewayKey,
                        ToState = route.Target,
                        Action = route.Trigger,
                        RequiresRole = route.RequiresRole,
                        Metadata = EmitTransitionMetadata(route)
                    });
                }
                continue;
            }

            // Split (or any non-Join gateway with a Source).
            if (string.IsNullOrWhiteSpace(gateway.Source))
                continue;

            var distinctTriggers = gateway.Routes
                .Select(r => r.Trigger)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var isParallelFork = gateway.Routes.Count >= 2 && distinctTriggers.Count == 1;

            if (!isParallelFork)
            {
                // Exclusive choice (distinct triggers) or single-route wrapper:
                // flatten so each trigger lands on its own target stage.
                foreach (var route in gateway.Routes)
                {
                    transitions.Add(EmitTransition(gateway, route));
                }
                continue;
            }

            // Parallel fan-out: emit the entry edge into the gateway, then one auto-fan-out
            // edge per outgoing branch. The engine's HandleSplitGatewayAdvance follows every
            // outgoing edge from gateway.Source==gatewayKey when the user takes the entry trigger.
            transitions.Add(new WorkflowTransitionFile
            {
                FromState = gateway.Source,
                ToState = gateway.GatewayKey,
                Action = distinctTriggers[0]
            });

            foreach (var route in gateway.Routes)
            {
                transitions.Add(new WorkflowTransitionFile
                {
                    FromState = gateway.GatewayKey,
                    ToState = route.Target,
                    Action = "split-auto",
                    RequiresRole = route.RequiresRole,
                    Metadata = EmitTransitionMetadata(route)
                });
            }
        }

        return transitions
            .OrderBy(t => t.FromState, StringComparer.Ordinal)
            .ThenBy(t => t.ToState, StringComparer.Ordinal)
            .ThenBy(t => t.Action, StringComparer.Ordinal)
            .ToList();
    }

    private static WorkflowDefinitionMetadata? EmitWorkflowMetadata(AuthoredWorkflow authored)
    {
        var lanes = authored.Lanes.Count == 0
            ? null
            : authored.Lanes
                .OrderBy(lane => lane.Key, StringComparer.Ordinal)
                .Select(lane => new WorkflowLaneDefinition
                {
                    Key = lane.Key,
                    DisplayName = lane.DisplayName,
                    Actor = lane.Actor,
                    QueueName = lane.QueueName,
                    RoleGates = lane.RoleGates.Count == 0 ? null : lane.RoleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray()
                })
                .ToArray();

        var lanesByKey = authored.Lanes.ToDictionary(lane => lane.Key, StringComparer.Ordinal);

        var gateways = authored.Gateways.Count == 0
            ? null
            : authored.Gateways
                .OrderBy(gateway => gateway.GatewayKey, StringComparer.Ordinal)
                .Select(gateway =>
                {
                    var assignment = ResolveAssignment(gateway.LaneKey, gateway.Actor, gateway.RoleGates, lanesByKey);
                    var waitingInfo = gateway.WaitingInfo;
                    return new WorkflowGatewayDefinition
                    {
                        Key = gateway.GatewayKey,
                        DisplayName = gateway.DisplayName,
                        Description = gateway.Description,
                        GatewayType = gateway.Kind.ToString(),
                        LaneKey = gateway.LaneKey,
                        Actor = assignment.Actor,
                        RoleGates = assignment.RoleGates,
                        WaitingContent = waitingInfo?.Content,
                        WaitingExpectedSeconds = waitingInfo?.ExpectedWaitSeconds ?? 0,
                        WaitingPollIntervalMs = waitingInfo?.PollIntervalMs ?? 0,
                        WaitingAllowDefer = waitingInfo?.AllowDefer ?? true,
                        WaitingDeferMessage = waitingInfo?.DeferMessage,
                        RequiredIncomingLanes = gateway.RequiredIncomingLanes.Count == 0
                            ? null
                            : gateway.RequiredIncomingLanes.OrderBy(l => l, StringComparer.Ordinal).ToArray()
                    };
                })
                .ToArray();

        var handoffs = authored.Handoffs.Count == 0
            ? null
            : authored.Handoffs
                .OrderBy(h => h.Id, StringComparer.Ordinal)
                .Select(h => new WorkflowHandoffDefinition
                {
                    Id = h.Id,
                    FromState = h.FromStage,
                    ToState = h.ToStage,
                    Label = h.Label,
                    ActorChange = h.ActorChange
                })
                .ToArray();

        var tags = authored.Metadata.Count == 0
            ? null
            : authored.Metadata
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(authored.Description)
            && string.IsNullOrWhiteSpace(authored.SchemaVersion)
            && lanes is null
            && gateways is null
            && handoffs is null
            && tags is null)
        {
            return new WorkflowDefinitionMetadata
            {
                AuthoredWorkflowId = authored.Id
            };
        }

        return new WorkflowDefinitionMetadata
        {
            AuthoredWorkflowId = authored.Id,
            Description = authored.Description,
            SchemaVersion = authored.SchemaVersion,
            Lanes = lanes,
            Gateways = gateways,
            Tags = tags,
            Handoffs = handoffs
        };
    }

    private static WorkflowStateMetadata? EmitStateMetadata(
        AuthoredStage stage,
        IReadOnlyDictionary<string, AuthoredLane> lanesByKey)
    {
        var actions = EmitActions(stage.Actions);
        var assignment = ResolveAssignment(stage.LaneKey, stage.Actor, stage.RoleGates, lanesByKey);

        if (string.IsNullOrWhiteSpace(stage.Description)
            && string.IsNullOrWhiteSpace(assignment.Actor)
            && string.IsNullOrWhiteSpace(stage.LaneKey)
            && assignment.RoleGates is null
            && actions is null)
        {
            return new WorkflowStateMetadata
            {
                StageType = stage.Kind.ToString()
            };
        }

        return new WorkflowStateMetadata
        {
            Description = stage.Description,
            StageType = stage.Kind.ToString(),
            Actor = assignment.Actor,
            LaneKey = stage.LaneKey,
            RoleGates = assignment.RoleGates,
            Actions = actions
        };
    }

    private static (string? Actor, string[]? RoleGates) ResolveAssignment(
        string? laneKey,
        string? actor,
        IReadOnlyList<string> roleGates,
        IReadOnlyDictionary<string, AuthoredLane> lanesByKey)
    {
        if (!string.IsNullOrWhiteSpace(laneKey) && lanesByKey.TryGetValue(laneKey, out var lane))
        {
            var effectiveActor = !string.IsNullOrWhiteSpace(actor) ? actor : lane.Actor;
            var effectiveRoleGates = roleGates.Count > 0
                ? roleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray()
                : lane.RoleGates.Count > 0
                    ? lane.RoleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray()
                    : null;

            return (effectiveActor, effectiveRoleGates);
        }

        return (
            actor,
            roleGates.Count == 0 ? null : roleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray());
    }

    private static WorkflowTransitionMetadata? EmitTransitionMetadata(AuthoredRoute route)
    {
        var actions = EmitActions(route.Actions);
        var conditions = route.Condition is null
            ? null
            : new[]
            {
                new WorkflowConditionDefinition
                {
                    Kind = route.Condition.Kind,
                    Expression = route.Condition.Expression,
                    Description = route.Condition.Description
                }
            };

        return actions is null && conditions is null
            ? null
            : new WorkflowTransitionMetadata
            {
                Conditions = conditions,
                Actions = actions
            };
    }

    private static IReadOnlyList<WorkflowActionDefinition>? EmitActions(IReadOnlyList<AuthoredAction> actions) =>
        actions.Count == 0
            ? null
            : actions.Select(a => new WorkflowActionDefinition
            {
                Type = a.Type,
                Timing = a.Timing.ToString(),
                ParameterSchemaKey = a.ParameterSchemaKey,
                Summary = a.Summary,
                Parameters = CloneParameters(a.Parameters)
            }).ToArray();

    private static JsonObject CloneParameters(JsonObject parameters)
    {
        var clone = new JsonObject();
        foreach (var kvp in parameters)
            clone[kvp.Key] = kvp.Value?.DeepClone();

        return clone;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectionDiagnostic Error(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, StageKey = stageKey };

    private static ProjectionDiagnostic Warning(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Warning, Code = code, Message = message, StageKey = stageKey };
}
