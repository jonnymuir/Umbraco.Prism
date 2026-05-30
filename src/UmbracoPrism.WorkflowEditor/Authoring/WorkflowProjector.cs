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

        var normalisedTransitions = authored.Transitions
            .OrderBy(t => t.Source, StringComparer.Ordinal)
            .ThenBy(t => t.Target, StringComparer.Ordinal)
            .ThenBy(t => t.Trigger, StringComparer.Ordinal)
            .ToList();

        // 3. Emit
        var states = normalisedStages
            .Select(s => EmitStage(authored, s, diagnostics, lanesByKey))
            .ToList();

        var transitions = normalisedTransitions
            .Select(EmitTransition)
            .ToList();

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

        // Transitions may target either stages or gateways — both are valid graph nodes.
        var gatewayKeys = new HashSet<string>(
            authored.Gateways.Select(g => g.GatewayKey), StringComparer.Ordinal);

        var validNodeKeys = new HashSet<string>(stageKeys, StringComparer.Ordinal);
        validNodeKeys.UnionWith(gatewayKeys);

        foreach (var transition in authored.Transitions)
        {
            if (!string.IsNullOrWhiteSpace(transition.Source) && !validNodeKeys.Contains(transition.Source))
            {
                diagnostics.Add(Warning("PROJ004",
                    $"Transition source '{transition.Source}' does not reference a defined stage or gateway.",
                    transition.Source));
            }

            if (!string.IsNullOrWhiteSpace(transition.Target) && !validNodeKeys.Contains(transition.Target))
            {
                diagnostics.Add(Warning("PROJ004",
                    $"Transition target '{transition.Target}' does not reference a defined stage or gateway.",
                    transition.Target));
            }
        }
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
        return stage.Kind switch
        {
            StageKind.Question => EmitQuestionComponents(stage),
            StageKind.CheckAnswers => EmitCheckAnswersComponents(authored),
            StageKind.Confirmation => EmitConfirmationComponents(stage),
            StageKind.TaskList => EmitTaskListComponents(),
            _ => EmitUnknownKind(stage, diagnostics)
        };
    }

    private static IReadOnlyList<PrismComponent> EmitQuestionComponents(AuthoredStage stage)
    {
        var normalisedFields = stage.Fields
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .ToList();

        var children = normalisedFields
            .Select(f => (PrismComponent)MapFieldToInputComponent(f))
            .ToList();

        return [new FieldsetComponent { Children = children }];
    }

    private static IReadOnlyList<PrismComponent> EmitCheckAnswersComponents(AuthoredWorkflow authored)
    {
        // Gather all fields from question stages in canonical (sorted) order so the output is stable.
        var questionFields = authored.Stages
            .Where(s => s.Kind == StageKind.Question)
            .OrderBy(s => s.StageKey, StringComparer.Ordinal)
            .SelectMany(s => s.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
            .Select(f => (PrismComponent)MapFieldToInputComponent(f))
            .ToList();

        return [new SummaryListComponent { Children = questionFields }];
    }

    private static IReadOnlyList<PrismComponent> EmitConfirmationComponents(AuthoredStage stage)
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

    private static IReadOnlyList<PrismComponent> EmitTaskListComponents()
        => [new TaskListComponent()];

    private static IReadOnlyList<PrismComponent> EmitUnknownKind(
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        diagnostics.Add(Warning("PROJ005",
            $"Unknown StageKind value for stage '{stage.StageKey}'. Defaulting to question shell.",
            stage.StageKey));

        return [new FieldsetComponent()];
    }

    private static WorkflowTransitionFile EmitTransition(AuthoredTransition t) =>
        new()
        {
            FromState = t.Source,
            ToState = t.Target,
            Action = t.Trigger,
            RequiresRole = t.RequiresRole,
            Metadata = EmitTransitionMetadata(t)
        };

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

    private static WorkflowTransitionMetadata? EmitTransitionMetadata(AuthoredTransition transition)
    {
        var actions = EmitActions(transition.Actions);
        var conditions = transition.Conditions.Count == 0
            ? null
            : transition.Conditions.Select(c => new WorkflowConditionDefinition
            {
                Kind = c.Kind,
                Expression = c.Expression,
                Description = c.Description
            }).ToArray();

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

    // ─── Field-to-component mapping ───────────────────────────────────────────

    private static InputComponent MapFieldToInputComponent(AuthoredField field) => field.Type switch
    {
        FieldType.Textarea => new TextareaComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Email => new EmailComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Pattern = field.ValidationPattern
        },
        FieldType.Number => new NumberInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Decimal => new DecimalInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Date => new DateInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Boolean => new BooleanComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint
        },
        FieldType.Select => new SelectComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Options = field.Options
        },
        FieldType.Radios => new RadiosComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Options = field.Options
        },
        FieldType.Checkboxes => new CheckboxesComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Options = field.Options
        },
        _ => new TextInputComponent
        {
            FieldKey = field.Key, Label = field.Label,
            Required = field.Required, Hint = field.Hint,
            Pattern = field.ValidationPattern
        }
    };

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectionDiagnostic Error(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, StageKey = stageKey };

    private static ProjectionDiagnostic Warning(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Warning, Code = code, Message = message, StageKey = stageKey };
}
