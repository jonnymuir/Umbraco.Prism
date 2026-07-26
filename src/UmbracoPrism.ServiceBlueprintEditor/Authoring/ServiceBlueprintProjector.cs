using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Deterministic compiler from <see cref="AuthoredServiceBlueprint"/> to <see cref="ServiceBlueprint"/>.
/// </summary>
public sealed class ServiceBlueprintProjector : IServiceBlueprintProjector
{
    private readonly IActionCatalogProvider _actionCatalogProvider;

    public ServiceBlueprintProjector()
        : this(new BuiltInActionCatalogProvider())
    {
    }

    public ServiceBlueprintProjector(IActionCatalogProvider actionCatalogProvider)
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

    public static byte[] SerializeCanonical(ServiceBlueprint file) =>
        JsonSerializer.SerializeToUtf8Bytes(file, CanonicalOptions);

    public static string ComputeCanonicalChecksum(ServiceBlueprint file)
    {
        var hash = SHA256.HashData(SerializeCanonical(file));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public ProjectionResult Project(AuthoredServiceBlueprint authored)
    {
        var diagnostics = new List<ProjectionDiagnostic>();

        Validate(authored, diagnostics);

        var queuesByKey = authored.Queues
            .ToDictionary(queue => queue.Key, StringComparer.Ordinal);

        var touchpoints = authored.Touchpoints
            .OrderBy(touchpoint => touchpoint.TouchpointKey, StringComparer.Ordinal)
            .Select(touchpoint => EmitTouchpoint(authored, touchpoint, diagnostics, queuesByKey))
            .ToArray();

        var gateways = authored.Gateways
            .OrderBy(gateway => gateway.GatewayKey, StringComparer.Ordinal)
            .Select(gateway => EmitGateway(gateway, queuesByKey))
            .ToArray();

        var queues = authored.Queues
            .OrderBy(queue => queue.Key, StringComparer.Ordinal)
            .Select(queue => new QueueDefinition
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
                .Select(handoff => new HandoffDefinition
                {
                    Id = handoff.Id,
                    FromState = handoff.FromTouchpoint,
                    ToState = handoff.ToTouchpoint,
                    Label = handoff.Label,
                    ActorChange = handoff.ActorChange
                })
                .ToArray();

        var tags = authored.Metadata.Count == 0
            ? null
            : authored.Metadata
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        var file = new ServiceBlueprint
        {
            DefinitionKey = authored.DefinitionKey,
            DisplayName = authored.DisplayName,
            Version = authored.Version,
            Description = authored.Description,
            SchemaVersion = authored.SchemaVersion,
            AuthoredServiceBlueprintId = authored.Id,
            InitialTouchpoint = authored.InitialTouchpointKey,
            RequestPolicy = authored.RequestPolicy,
            Touchpoints = touchpoints,
            Transitions = EmitLegacyTransitions(touchpoints, gateways),
            Queues = queues.Length == 0 ? null : queues,
            Gateways = gateways.Length == 0 ? null : gateways,
            Handoffs = handoffs,
            Tags = tags,
            Metadata = new ServiceBlueprintMetadata
            {
                AuthoredServiceBlueprintId = authored.Id,
                Description = authored.Description,
                SchemaVersion = authored.SchemaVersion,
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

    private void Validate(AuthoredServiceBlueprint authored, List<ProjectionDiagnostic> diagnostics)
    {
        AuthoredServiceBlueprintSchemaValidator.Validate(authored, diagnostics, _actionCatalogProvider);

        var touchpointKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var touchpoint in authored.Touchpoints)
        {
            if (string.IsNullOrWhiteSpace(touchpoint.TouchpointKey))
            {
                diagnostics.Add(Error("PROJ001", "A touchpoint has an empty TouchpointKey.", null));
                continue;
            }

            if (!touchpointKeys.Add(touchpoint.TouchpointKey))
            {
                diagnostics.Add(Error("PROJ002",
                    $"Duplicate TouchpointKey '{touchpoint.TouchpointKey}'. All touchpoint keys must be unique.",
                    touchpoint.TouchpointKey));
            }
        }

        if (!string.IsNullOrWhiteSpace(authored.InitialTouchpointKey)
            && !touchpointKeys.Contains(authored.InitialTouchpointKey))
        {
            diagnostics.Add(Error("PROJ003",
                $"InitialTouchpointKey '{authored.InitialTouchpointKey}' does not reference any defined touchpoint.",
                null));
        }
    }

    private static StepDefinition EmitTouchpoint(
        AuthoredServiceBlueprint authored,
        AuthoredTouchpoint touchpoint,
        List<ProjectionDiagnostic> diagnostics,
        IReadOnlyDictionary<string, AuthoredQueue> queuesByKey)
    {
        var assignment = ResolveAssignment(touchpoint.QueueKey, touchpoint.Actor, touchpoint.RoleGates, queuesByKey);
        var actions = EmitActions(touchpoint.Actions);
        var routes = GetTouchpointRoutes(authored, touchpoint)
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ThenBy(route => route.Id, StringComparer.Ordinal)
            .Select(EmitRoute)
            .ToArray();

        return new StepDefinition
        {
            TouchpointKey = touchpoint.TouchpointKey,
            DisplayName = touchpoint.DisplayName,
            Description = touchpoint.Description,
            TouchpointType = touchpoint.Kind.ToString(),
            Actor = assignment.Actor,
            QueueKey = touchpoint.QueueKey,
            RoleGates = assignment.RoleGates,
            Actions = actions,
            Components = EmitComponents(authored, touchpoint, diagnostics),
            Routes = routes.Length == 0 ? null : routes,
            Metadata = new TouchpointMetadata
            {
                Description = touchpoint.Description,
                TouchpointType = touchpoint.Kind.ToString(),
                Actor = assignment.Actor,
                QueueKey = touchpoint.QueueKey,
                RoleGates = assignment.RoleGates,
                Actions = actions
            }
        };
    }

    private static ServiceBlueprintGatewayDefinition EmitGateway(
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

        return new ServiceBlueprintGatewayDefinition
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

    private static IReadOnlyList<AuthoredRoute> GetTouchpointRoutes(AuthoredServiceBlueprint authored, AuthoredTouchpoint touchpoint)
    {
        if (touchpoint.Routes.Count > 0)
        {
            return touchpoint.Routes;
        }

        var legacyRoutes = authored.Gateways
            .Where(gateway => string.Equals(gateway.Source, touchpoint.TouchpointKey, StringComparison.Ordinal))
            .SelectMany(gateway => gateway.Routes
                .Select(route => route.Trigger)
                .Where(trigger => !string.IsNullOrWhiteSpace(trigger))
                .Distinct(StringComparer.Ordinal)
                .Select(trigger => new AuthoredRoute
                {
                    Id = $"{touchpoint.TouchpointKey}--{trigger}--{gateway.GatewayKey}",
                    Target = gateway.GatewayKey,
                    Trigger = trigger
                }))
            .OrderBy(route => route.Trigger, StringComparer.Ordinal)
            .ThenBy(route => route.Target, StringComparer.Ordinal)
            .ToArray();

        return legacyRoutes;
    }

    private static IReadOnlyList<RouteFile> EmitLegacyTransitions(
        IReadOnlyList<StepDefinition> touchpoints,
        IReadOnlyList<ServiceBlueprintGatewayDefinition> gateways)
    {
        var transitions = new List<RouteFile>();

        transitions.AddRange(touchpoints
            .Where(touchpoint => touchpoint.Routes is { Count: > 0 })
            .SelectMany(touchpoint => touchpoint.Routes!.Select(route => new RouteFile
            {
                FromState = touchpoint.TouchpointKey,
                ToState = route.Target,
                Action = route.Trigger,
                RequiresRole = route.RequiresRole,
                Conditions = route.Conditions,
                Actions = route.Actions
            })));

        transitions.AddRange(gateways
            .Where(gateway => gateway.Routes is { Count: > 0 })
            .SelectMany(gateway => gateway.Routes!.Select(route => new RouteFile
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
        AuthoredServiceBlueprint authored,
        AuthoredTouchpoint touchpoint,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (touchpoint.Components.Count > 0)
        {
            return touchpoint.Components;
        }

        return touchpoint.Kind switch
        {
            TouchpointKind.Question => [new FieldsetComponent()],
            TouchpointKind.CheckAnswers => EmitDefaultCheckAnswersSummary(authored),
            TouchpointKind.Confirmation => EmitDefaultConfirmation(touchpoint),
            TouchpointKind.TaskList => [new TaskListComponent()],
            _ => EmitUnknownKind(touchpoint, diagnostics)
        };
    }

    private static IReadOnlyList<PrismComponent> EmitDefaultCheckAnswersSummary(AuthoredServiceBlueprint authored)
    {
        var inputs = authored.Touchpoints
            .Where(touchpoint => touchpoint.Kind == TouchpointKind.Question)
            .OrderBy(touchpoint => touchpoint.TouchpointKey, StringComparer.Ordinal)
            .SelectMany(touchpoint => HarvestInputs(touchpoint.Components))
            .ToList();

        return [new SummaryListComponent { Children = inputs }];
    }

    private static IReadOnlyList<PrismComponent> EmitDefaultConfirmation(AuthoredTouchpoint touchpoint)
    {
        var components = new List<PrismComponent>
        {
            new PanelComponent { Heading = touchpoint.DisplayName }
        };

        if (!string.IsNullOrWhiteSpace(touchpoint.Description))
        {
            components.Add(new BodyComponent { Content = touchpoint.Description });
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
        AuthoredTouchpoint touchpoint,
        List<ProjectionDiagnostic> diagnostics)
    {
        diagnostics.Add(Warning("PROJ005",
            $"Unknown TouchpointKind value for touchpoint '{touchpoint.TouchpointKey}'. Defaulting to question shell.",
            touchpoint.TouchpointKey));

        return [new FieldsetComponent()];
    }

    private static ServiceBlueprintRouteDefinition EmitRoute(AuthoredRoute route) =>
        new()
        {
            Id = route.Id,
            Target = route.Target,
            Trigger = route.Trigger,
            RequiresRole = route.RequiresRole,
            Conditions = route.Condition is null
                ? null
                : [new ConditionDefinition
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

    private static IReadOnlyList<ActionDefinition>? EmitActions(IReadOnlyList<AuthoredAction> actions) =>
        actions.Count == 0
            ? null
            : actions.Select(action => new ActionDefinition
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

    private static ProjectionDiagnostic Error(string code, string message, string? touchpointKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, TouchpointKey = touchpointKey };

    private static ProjectionDiagnostic Warning(string code, string message, string? touchpointKey) =>
        new() { Severity = DiagnosticSeverity.Warning, Code = code, Message = message, TouchpointKey = touchpointKey };
}
