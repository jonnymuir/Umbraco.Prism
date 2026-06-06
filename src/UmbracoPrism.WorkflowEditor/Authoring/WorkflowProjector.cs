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

    public ProjectionResult Project(AuthoredWorkflow authored)
    {
        var diagnostics = new List<ProjectionDiagnostic>();

        Validate(authored, diagnostics);

        var queuesByKey = GetQueues(authored)
            .ToDictionary(queue => queue.Key, StringComparer.Ordinal);

        var states = authored.Stages
            .OrderBy(stage => stage.StageKey, StringComparer.Ordinal)
            .Select(stage => EmitStage(authored, stage, diagnostics, queuesByKey))
            .ToArray();

        var gateways = authored.Gateways
            .OrderBy(gateway => gateway.GatewayKey, StringComparer.Ordinal)
            .Select(gateway => EmitGateway(gateway, queuesByKey))
            .ToArray();

        var queues = GetQueues(authored)
            .OrderBy(queue => queue.Key, StringComparer.Ordinal)
            .Select(queue => new WorkflowQueueDefinition
            {
                Key = queue.Key,
                DisplayName = queue.DisplayName,
                Description = queue.Description,
                Actor = queue.Actor,
                RoleGates = queue.RoleGates.Count == 0 ? null : queue.RoleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
                Tags = queue.Tags.Count == 0
                    ? null
                    : queue.Tags.OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal)
            })
            .ToArray();

        var handoffs = authored.Handoffs.Count == 0
            ? null
            : authored.Handoffs
                .OrderBy(handoff => handoff.Id, StringComparer.Ordinal)
                .Select(handoff => new WorkflowHandoffDefinition
                {
                    Id = handoff.Id,
                    FromState = handoff.FromStage,
                    ToState = handoff.ToStage,
                    Label = handoff.Label,
                    ActorChange = handoff.ActorChange
                })
                .ToArray();

        var tags = authored.Metadata.Count == 0
            ? null
            : authored.Metadata
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        var legacyLanes = queues.Length == 0
            ? null
            : queues.Select(queue => new WorkflowLaneDefinition
            {
                Key = queue.Key,
                DisplayName = queue.DisplayName,
                Description = queue.Description,
                Actor = queue.Actor,
                RoleGates = queue.RoleGates,
                Tags = queue.Tags
            }).ToArray();

        var file = new WorkflowDefinitionFile
        {
            DefinitionKey = authored.DefinitionKey,
            DisplayName = authored.DisplayName,
            Version = authored.Version,
            Description = authored.Description,
            SchemaVersion = authored.SchemaVersion,
            AuthoredWorkflowId = authored.Id,
            InitialState = authored.InitialStageKey,
            InstancePolicy = authored.InstancePolicy,
            States = states,
            Transitions = EmitLegacyTransitions(states, gateways),
            Queues = queues.Length == 0 ? null : queues,
            Gateways = gateways.Length == 0 ? null : gateways,
            Handoffs = handoffs,
            Tags = tags,
            Metadata = new WorkflowDefinitionMetadata
            {
                AuthoredWorkflowId = authored.Id,
                Description = authored.Description,
                SchemaVersion = authored.SchemaVersion,
                Lanes = legacyLanes,
                Gateways = gateways.Length == 0 ? null : gateways,
                Tags = tags,
                Handoffs = handoffs
            }
        };

        return new ProjectionResult
        {
            File = file,
            Checksum = ComputeCanonicalChecksum(file),
            Diagnostics = diagnostics
        };
    }

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
                    $"Duplicate StageKey '{stage.StageKey}'. All stage keys must be unique.",
                    stage.StageKey));
            }
        }

        if (!string.IsNullOrWhiteSpace(authored.InitialStageKey)
            && !stageKeys.Contains(authored.InitialStageKey))
        {
            diagnostics.Add(Error("PROJ003",
                $"InitialStageKey '{authored.InitialStageKey}' does not reference any defined stage.",
                null));
        }
    }

    private static StepDefinition EmitStage(
        AuthoredWorkflow authored,
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics,
        IReadOnlyDictionary<string, AuthoredQueue> queuesByKey)
    {
        var assignment = ResolveAssignment(stage.QueueKey, stage.Actor, stage.RoleGates, queuesByKey);
        var actions = EmitActions(stage.Actions);
        var routes = GetStageRoutes(authored, stage)
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ThenBy(route => route.Id, StringComparer.Ordinal)
            .Select(EmitRoute)
            .ToArray();

        return new StepDefinition
        {
            StateKey = stage.StageKey,
            DisplayName = stage.DisplayName,
            Description = stage.Description,
            StageType = stage.Kind.ToString(),
            Actor = assignment.Actor,
            QueueKey = stage.QueueKey,
            RoleGates = assignment.RoleGates,
            Actions = actions,
            Components = EmitComponents(authored, stage, diagnostics),
            Routes = routes.Length == 0 ? null : routes,
            Metadata = new WorkflowStateMetadata
            {
                Description = stage.Description,
                StageType = stage.Kind.ToString(),
                Actor = assignment.Actor,
                QueueKey = stage.QueueKey,
                RoleGates = assignment.RoleGates,
                Actions = actions
            }
        };
    }

    private static WorkflowGatewayDefinition EmitGateway(
        AuthoredGateway gateway,
        IReadOnlyDictionary<string, AuthoredQueue> queuesByKey)
    {
        var assignment = ResolveAssignment(gateway.QueueKey, gateway.Actor, gateway.RoleGates, queuesByKey);
        var routes = gateway.Routes
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ThenBy(route => route.Id, StringComparer.Ordinal)
            .Select(EmitRoute)
            .ToArray();

        return new WorkflowGatewayDefinition
        {
            Key = gateway.GatewayKey,
            DisplayName = gateway.DisplayName,
            Description = gateway.Description,
            GatewayType = gateway.Kind.ToString(),
            QueueKey = gateway.QueueKey,
            Actor = assignment.Actor,
            RoleGates = assignment.RoleGates,
            Routes = routes.Length == 0 ? null : routes,
            WaitingContent = gateway.WaitingInfo?.Content,
            WaitingExpectedSeconds = gateway.WaitingInfo?.ExpectedWaitSeconds ?? 0,
            WaitingPollIntervalMs = gateway.WaitingInfo?.PollIntervalMs ?? 0,
            WaitingAllowDefer = gateway.WaitingInfo?.AllowDefer ?? true,
            WaitingDeferMessage = gateway.WaitingInfo?.DeferMessage,
            RequiredIncomingQueues = gateway.RequiredIncomingQueues.Count == 0
                ? null
                : gateway.RequiredIncomingQueues.OrderBy(queue => queue, StringComparer.Ordinal).ToArray()
        };
    }

    private static IReadOnlyList<AuthoredQueue> GetQueues(AuthoredWorkflow authored) =>
        authored.Queues.Count > 0 ? authored.Queues : authored.Lanes;

    private static IReadOnlyList<AuthoredRoute> GetStageRoutes(AuthoredWorkflow authored, AuthoredStage stage)
    {
        if (stage.Routes.Count > 0)
        {
            return stage.Routes;
        }

        var legacyRoutes = authored.Gateways
            .Where(gateway => string.Equals(gateway.Source, stage.StageKey, StringComparison.Ordinal))
            .SelectMany(gateway => gateway.Routes
                .Select(route => route.Trigger)
                .Where(trigger => !string.IsNullOrWhiteSpace(trigger))
                .Distinct(StringComparer.Ordinal)
                .Select(trigger => new AuthoredRoute
                {
                    Id = $"{stage.StageKey}--{trigger}--{gateway.GatewayKey}",
                    Target = gateway.GatewayKey,
                    Trigger = trigger
                }))
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ToArray();

        return legacyRoutes;
    }

    private static IReadOnlyList<WorkflowTransitionFile> EmitLegacyTransitions(
        IReadOnlyList<StepDefinition> states,
        IReadOnlyList<WorkflowGatewayDefinition> gateways)
    {
        var transitions = new List<WorkflowTransitionFile>();

        transitions.AddRange(states
            .Where(state => state.Routes is { Count: > 0 })
            .SelectMany(state => state.Routes!.Select(route => new WorkflowTransitionFile
            {
                FromState = state.StateKey,
                ToState = route.Target,
                Action = route.Trigger,
                RequiresRole = route.RequiresRole,
                Conditions = route.Conditions,
                Actions = route.Actions
            })));

        transitions.AddRange(gateways
            .Where(gateway => gateway.Routes is { Count: > 0 })
            .SelectMany(gateway => gateway.Routes!.Select(route => new WorkflowTransitionFile
            {
                FromState = gateway.Key,
                ToState = route.Target,
                Action = route.Trigger,
                RequiresRole = route.RequiresRole,
                Conditions = route.Conditions,
                Actions = route.Actions
            })));

        return transitions
            .OrderBy(transition => transition.FromState, StringComparer.Ordinal)
            .ThenBy(transition => transition.ToState, StringComparer.Ordinal)
            .ThenBy(transition => transition.Action, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PrismComponent> EmitComponents(
        AuthoredWorkflow authored,
        AuthoredStage stage,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (stage.Components.Count > 0)
        {
            return stage.Components;
        }

        return stage.Kind switch
        {
            StageKind.Question => [new FieldsetComponent()],
            StageKind.CheckAnswers => EmitDefaultCheckAnswersSummary(authored),
            StageKind.Confirmation => EmitDefaultConfirmation(stage),
            StageKind.TaskList => [new TaskListComponent()],
            _ => EmitUnknownKind(stage, diagnostics)
        };
    }

    private static IReadOnlyList<PrismComponent> EmitDefaultCheckAnswersSummary(AuthoredWorkflow authored)
    {
        var inputs = authored.Stages
            .Where(stage => stage.Kind == StageKind.Question)
            .OrderBy(stage => stage.StageKey, StringComparer.Ordinal)
            .SelectMany(stage => HarvestInputs(stage.Components))
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
                    {
                        yield return nested;
                    }

                    break;
                case AccordionComponent accordion:
                    foreach (var section in accordion.Sections)
                    {
                        foreach (var nested in HarvestInputs(section.Children))
                        {
                            yield return nested;
                        }
                    }

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

    private static WorkflowRouteDefinition EmitRoute(AuthoredRoute route) =>
        new()
        {
            Id = route.Id,
            Target = route.Target,
            Trigger = route.Trigger,
            RequiresRole = route.RequiresRole,
            Conditions = route.Condition is null
                ? null
                : [new WorkflowConditionDefinition
                {
                    Kind = route.Condition.Kind,
                    Expression = route.Condition.Expression,
                    Description = route.Condition.Description
                }],
            Actions = EmitActions(route.Actions)
        };

    private static (string? Actor, string[]? RoleGates) ResolveAssignment(
        string? queueKey,
        string? actor,
        IReadOnlyList<string> roleGates,
        IReadOnlyDictionary<string, AuthoredQueue> queuesByKey)
    {
        if (!string.IsNullOrWhiteSpace(queueKey)
            && queuesByKey.TryGetValue(queueKey, out var queue))
        {
            var effectiveActor = !string.IsNullOrWhiteSpace(actor) ? actor : queue.Actor;
            var effectiveRoleGates = roleGates.Count > 0
                ? roleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray()
                : queue.RoleGates.Count > 0
                    ? queue.RoleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray()
                    : null;

            return (effectiveActor, effectiveRoleGates);
        }

        return (
            actor,
            roleGates.Count == 0 ? null : roleGates.OrderBy(role => role, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<WorkflowActionDefinition>? EmitActions(IReadOnlyList<AuthoredAction> actions) =>
        actions.Count == 0
            ? null
            : actions.Select(action => new WorkflowActionDefinition
            {
                Type = action.Type,
                Timing = action.Timing.ToString(),
                ParameterSchemaKey = action.ParameterSchemaKey,
                Summary = action.Summary,
                Parameters = CloneParameters(action.Parameters)
            }).ToArray();

    private static JsonObject CloneParameters(JsonObject parameters)
    {
        var clone = new JsonObject();
        foreach (var kvp in parameters)
        {
            clone[kvp.Key] = kvp.Value?.DeepClone();
        }

        return clone;
    }

    private static ProjectionDiagnostic Error(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, StageKey = stageKey };

    private static ProjectionDiagnostic Warning(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Warning, Code = code, Message = message, StageKey = stageKey };
}
