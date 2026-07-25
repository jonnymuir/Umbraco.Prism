using System.Text.Json;
using System.Text.Json.Nodes;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Structural validator for the authored workflow schema.
/// </summary>
public static class AuthoredWorkflowSchemaValidator
{
    public static void Validate(
        AuthoredWorkflow authored,
        List<ProjectionDiagnostic> diagnostics,
        IActionCatalogProvider? actionCatalogProvider = null)
    {
        actionCatalogProvider ??= new BuiltInActionCatalogProvider();
        var catalogByType = actionCatalogProvider.GetEntries()
            .ToDictionary(entry => entry.Type, StringComparer.Ordinal);
        var stageKeys = authored.Stages
            .Where(stage => !string.IsNullOrWhiteSpace(stage.StageKey))
            .Select(stage => stage.StageKey)
            .ToHashSet(StringComparer.Ordinal);
        var gatewayKeys = authored.Gateways
            .Where(gateway => !string.IsNullOrWhiteSpace(gateway.GatewayKey))
            .Select(gateway => gateway.GatewayKey)
            .ToHashSet(StringComparer.Ordinal);
        var schemaByKey = BuildSchemaMap(authored.ParameterSchemas, diagnostics);
        var queuesByKey = BuildQueueMap(authored.Queues, diagnostics);

        if (string.IsNullOrWhiteSpace(authored.DefinitionKey))
        {
            diagnostics.Add(Error("PROJ100", "DefinitionKey is required.", null));
        }

        if (string.IsNullOrWhiteSpace(authored.DisplayName))
        {
            diagnostics.Add(Error("PROJ101", "DisplayName is required.", null));
        }

        if (string.IsNullOrWhiteSpace(authored.InitialStageKey))
        {
            diagnostics.Add(Error("PROJ102", "InitialStageKey is required.", null));
        }

        if (authored.Stages.Count == 0)
        {
            diagnostics.Add(Error("PROJ103", "At least one stage is required.", null));
        }

        foreach (var stage in authored.Stages)
        {
            ValidateStage(stage, gatewayKeys, queuesByKey, schemaByKey, catalogByType, diagnostics);
        }

        var validGatewayTargets = new HashSet<string>(stageKeys, StringComparer.Ordinal);
        validGatewayTargets.UnionWith(gatewayKeys);

        foreach (var gateway in authored.Gateways)
        {
            ValidateGateway(gateway, validGatewayTargets, queuesByKey, schemaByKey, catalogByType, diagnostics);
        }
    }

    private static void ValidateStage(
        AuthoredStage stage,
        IReadOnlySet<string> gatewayKeys,
        IReadOnlyDictionary<string, AuthoredQueue> queuesByKey,
        IReadOnlyDictionary<string, AuthoredParameterSchema> schemaByKey,
        IReadOnlyDictionary<string, ActionCatalogEntry> catalogByType,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(stage.DisplayName))
        {
            diagnostics.Add(Error("PROJ104",
                $"Stage '{stage.StageKey}' must define a title.",
                stage.StageKey));
        }

        if (!string.IsNullOrWhiteSpace(stage.UnknownKindToken))
        {
            diagnostics.Add(Error("PROJ005",
                $"Unknown stage kind '{stage.UnknownKindToken}'. Allowed kinds: " +
                $"{string.Join(", ", WorkflowDefinitionFile.KnownStageKinds)}.",
                stage.StageKey));
        }

        if (string.IsNullOrWhiteSpace(stage.QueueKey))
        {
            if (queuesByKey.Count > 0)
            {
                diagnostics.Add(Error("PROJ153",
                    $"Stage '{stage.StageKey}' must reference a queue.",
                    stage.StageKey));
            }
        }
        else if (!queuesByKey.TryGetValue(stage.QueueKey, out var queue))
        {
            diagnostics.Add(Error("PROJ129",
                $"Stage '{stage.StageKey}' references unknown queue '{stage.QueueKey}'.",
                stage.StageKey));
        }
        else
        {
            ValidateAssignmentCompatibility(stage.StageKey, "Stage", stage.Actor, stage.RoleGates, queue, diagnostics);
        }

        var routeIds = new HashSet<string>(StringComparer.Ordinal);
        var triggerTargets = new HashSet<(string Trigger, string Target)>();
        for (var index = 0; index < stage.Routes.Count; index++)
        {
            var route = stage.Routes[index];

            if (string.IsNullOrWhiteSpace(route.Id))
            {
                diagnostics.Add(Error("PROJ154",
                    $"Route #{index} on stage '{stage.StageKey}' must define an id.",
                    stage.StageKey));
            }
            else if (!routeIds.Add(route.Id))
            {
                diagnostics.Add(Error("PROJ158",
                    $"Stage '{stage.StageKey}' has duplicate route id '{route.Id}'.",
                    stage.StageKey));
            }

            if (string.IsNullOrWhiteSpace(route.Trigger))
            {
                diagnostics.Add(Error("PROJ155",
                    $"Route '{route.Id}' on stage '{stage.StageKey}' must define a trigger.",
                    stage.StageKey));
            }

            if (string.IsNullOrWhiteSpace(route.Target))
            {
                diagnostics.Add(Error("PROJ156",
                    $"Route '{route.Id}' on stage '{stage.StageKey}' must define a target.",
                    stage.StageKey));
            }
            else if (!gatewayKeys.Contains(route.Target))
            {
                diagnostics.Add(Error("PROJ157",
                    $"Stage '{stage.StageKey}' route '{route.Id}' must target a gateway, but '{route.Target}' is not a known gateway.",
                    stage.StageKey));
            }

            if (!string.IsNullOrWhiteSpace(route.Trigger)
                && !string.IsNullOrWhiteSpace(route.Target)
                && !triggerTargets.Add((route.Trigger, route.Target)))
            {
                diagnostics.Add(Error("PROJ159",
                    $"Stage '{stage.StageKey}' has duplicate route '{route.Trigger}' → '{route.Target}'.",
                    stage.StageKey));
            }
        }

        ValidateActions(
            stage.Actions,
            stage.StageKey,
            "stage",
            new HashSet<ActionTiming> { ActionTiming.OnEntry, ActionTiming.OnExit },
            schemaByKey,
            catalogByType,
            diagnostics);
    }

    private static void ValidateGateway(
        AuthoredGateway gateway,
        IReadOnlySet<string> validTargets,
        IReadOnlyDictionary<string, AuthoredQueue> queuesByKey,
        IReadOnlyDictionary<string, AuthoredParameterSchema> schemaByKey,
        IReadOnlyDictionary<string, ActionCatalogEntry> catalogByType,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(gateway.GatewayKey))
        {
            diagnostics.Add(Error("PROJ130", "Gateway key is required.", null));
            return;
        }

        if (string.IsNullOrWhiteSpace(gateway.DisplayName))
        {
            diagnostics.Add(Error("PROJ132",
                $"Gateway '{gateway.GatewayKey}' must define a title.",
                gateway.GatewayKey));
        }

        if (string.IsNullOrWhiteSpace(gateway.QueueKey))
        {
            if (queuesByKey.Count > 0)
            {
                diagnostics.Add(Error("PROJ133",
                    $"Gateway '{gateway.GatewayKey}' must reference a queue.",
                    gateway.GatewayKey));
            }
        }
        else if (!queuesByKey.TryGetValue(gateway.QueueKey, out var queue))
        {
            diagnostics.Add(Error("PROJ134",
                $"Gateway '{gateway.GatewayKey}' references unknown queue '{gateway.QueueKey}'.",
                gateway.GatewayKey));
        }
        else
        {
            ValidateAssignmentCompatibility(gateway.GatewayKey, "Gateway", gateway.Actor, gateway.RoleGates, queue, diagnostics);
        }

        if (gateway.Routes.Count == 0)
        {
            diagnostics.Add(Error("PROJ144",
                $"Gateway '{gateway.GatewayKey}' must define at least one route.",
                gateway.GatewayKey));
        }

        var routeIds = new HashSet<string>(StringComparer.Ordinal);
        var triggerTargets = new HashSet<(string Trigger, string Target)>();
        for (var routeIndex = 0; routeIndex < gateway.Routes.Count; routeIndex++)
        {
            var route = gateway.Routes[routeIndex];

            if (string.IsNullOrWhiteSpace(route.Id))
            {
                diagnostics.Add(Error("PROJ145",
                    $"Route #{routeIndex} on gateway '{gateway.GatewayKey}' must define an id.",
                    gateway.GatewayKey));
            }
            else if (!routeIds.Add(route.Id))
            {
                diagnostics.Add(Error("PROJ146",
                    $"Gateway '{gateway.GatewayKey}' has duplicate route id '{route.Id}'.",
                    gateway.GatewayKey));
            }

            if (string.IsNullOrWhiteSpace(route.Trigger))
            {
                diagnostics.Add(Error("PROJ147",
                    $"Route '{route.Id}' on gateway '{gateway.GatewayKey}' must define a trigger.",
                    gateway.GatewayKey));
            }

            if (string.IsNullOrWhiteSpace(route.Target))
            {
                diagnostics.Add(Error("PROJ149",
                    $"Route '{route.Id}' on gateway '{gateway.GatewayKey}' must define a target.",
                    gateway.GatewayKey));
            }
            else if (!validTargets.Contains(route.Target))
            {
                diagnostics.Add(Error("PROJ150",
                    $"Route '{route.Id}' on gateway '{gateway.GatewayKey}' targets '{route.Target}', which is not a known state or gateway.",
                    gateway.GatewayKey));
            }

            if (!string.IsNullOrWhiteSpace(route.Trigger)
                && !string.IsNullOrWhiteSpace(route.Target)
                && !triggerTargets.Add((route.Trigger, route.Target)))
            {
                diagnostics.Add(Error("PROJ148",
                    $"Gateway '{gateway.GatewayKey}' has duplicate route '{route.Trigger}' → '{route.Target}'.",
                    gateway.GatewayKey));
            }

            if (route.Condition is { Expression: var expr } && string.IsNullOrWhiteSpace(expr))
            {
                diagnostics.Add(Error("PROJ151",
                    $"Route '{route.Id}' on gateway '{gateway.GatewayKey}' has a condition with no expression.",
                    gateway.GatewayKey));
            }

            ValidateActions(
                route.Actions,
                gateway.GatewayKey,
                "route",
                new HashSet<ActionTiming> { ActionTiming.OnTransition },
                schemaByKey,
                catalogByType,
                diagnostics);
        }

        if (gateway.Kind != GatewayKind.Join)
        {
            return;
        }

        if (gateway.WaitingInfo is null)
        {
            diagnostics.Add(Error("PROJ137",
                $"Join gateway '{gateway.GatewayKey}' must define waitingInfo.",
                gateway.GatewayKey));
        }

        if (gateway.RequiredIncomingQueues.Count == 0)
        {
            diagnostics.Add(Error("PROJ138",
                $"Join gateway '{gateway.GatewayKey}' must define at least one requiredIncomingQueue.",
                gateway.GatewayKey));
            return;
        }

        foreach (var requiredQueue in gateway.RequiredIncomingQueues)
        {
            if (!queuesByKey.ContainsKey(requiredQueue))
            {
                diagnostics.Add(Error("PROJ139",
                    $"Join gateway '{gateway.GatewayKey}' requiredIncomingQueue '{requiredQueue}' references an unknown queue.",
                    gateway.GatewayKey));
            }
        }
    }

    private static Dictionary<string, AuthoredQueue> BuildQueueMap(
        IReadOnlyList<AuthoredQueue> queues,
        List<ProjectionDiagnostic> diagnostics)
    {
        var map = new Dictionary<string, AuthoredQueue>(StringComparer.Ordinal);

        foreach (var queue in queues)
        {
            if (string.IsNullOrWhiteSpace(queue.Key))
            {
                diagnostics.Add(Error("PROJ127", "Queue key is required.", null));
                continue;
            }

            if (!map.TryAdd(queue.Key, queue))
            {
                diagnostics.Add(Error("PROJ128", $"Duplicate queue key '{queue.Key}'.", queue.Key));
            }
        }

        return map;
    }

    private static Dictionary<string, AuthoredParameterSchema> BuildSchemaMap(
        IReadOnlyList<AuthoredParameterSchema> schemas,
        List<ProjectionDiagnostic> diagnostics)
    {
        var map = new Dictionary<string, AuthoredParameterSchema>(StringComparer.Ordinal);

        foreach (var schema in schemas)
        {
            if (string.IsNullOrWhiteSpace(schema.Key))
            {
                diagnostics.Add(Error("PROJ110", "Parameter schema key is required.", null));
                continue;
            }

            if (!map.TryAdd(schema.Key, schema))
            {
                diagnostics.Add(Error("PROJ111", $"Duplicate parameter schema key '{schema.Key}'.", null));
                continue;
            }

            ValidateParameterDefinitions(schema.Key, schema.Properties, diagnostics);

            var definedKeys = schema.Properties
                .Select(property => property.Key)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var requiredKey in schema.Required)
            {
                if (!definedKeys.Contains(requiredKey))
                {
                    diagnostics.Add(Error("PROJ112",
                        $"Parameter schema '{schema.Key}' marks '{requiredKey}' as required but does not define it.",
                        null));
                }
            }
        }

        return map;
    }

    private static void ValidateParameterDefinitions(
        string schemaKey,
        IReadOnlyList<AuthoredParameterDefinition> definitions,
        List<ProjectionDiagnostic> diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Key))
            {
                diagnostics.Add(Error("PROJ113",
                    $"Parameter schema '{schemaKey}' contains a property with no key.",
                    null));
                continue;
            }

            if (!seen.Add(definition.Key))
            {
                diagnostics.Add(Error("PROJ114",
                    $"Parameter schema '{schemaKey}' contains duplicate property key '{definition.Key}'.",
                    null));
            }

            if (definition.ValueKind == ParameterValueKind.Array && definition.Items is null)
            {
                diagnostics.Add(Error("PROJ115",
                    $"Parameter '{definition.Key}' in schema '{schemaKey}' is an array but does not define item metadata.",
                    null));
            }

            if (definition.ValueKind == ParameterValueKind.Object)
            {
                ValidateParameterDefinitions(schemaKey, definition.Properties, diagnostics);
            }

            if (definition.Items is not null)
            {
                ValidateParameterDefinitions(schemaKey, [definition.Items], diagnostics);
            }
        }
    }

    private static void ValidateActions(
        IReadOnlyList<AuthoredAction> actions,
        string? ownerKey,
        string ownerKind,
        IReadOnlySet<ActionTiming> allowedTimings,
        IReadOnlyDictionary<string, AuthoredParameterSchema> schemaByKey,
        IReadOnlyDictionary<string, ActionCatalogEntry> catalogByType,
        List<ProjectionDiagnostic> diagnostics)
    {
        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action.Type))
            {
                diagnostics.Add(Error("PROJ116",
                    $"A {ownerKind} action in '{ownerKey ?? "workflow"}' must define a type.",
                    ownerKey));
                continue;
            }

            if (!allowedTimings.Contains(action.Timing))
            {
                diagnostics.Add(Error("PROJ117",
                    $"Action '{action.Type}' on {ownerKind} '{ownerKey}' has invalid timing '{action.Timing}'.",
                    ownerKey));
            }

            var appliesTo = GetAppliesTo(ownerKind, action.Timing);

            if (catalogByType.TryGetValue(action.Type, out var catalogEntry)
                && catalogEntry.AppliesTo.Count > 0
                && !catalogEntry.AppliesTo.Contains(appliesTo, StringComparer.Ordinal))
            {
                diagnostics.Add(Error("PROJ126",
                    $"Action '{action.Type}' cannot run at '{appliesTo}'.",
                    ownerKey));
            }

            var schema = ResolveSchema(action, ownerKey, schemaByKey, catalogByType, diagnostics);
            if (schema is null)
            {
                continue;
            }

            ValidateParameterObject(action.Parameters, schema, action.Type, ownerKey, diagnostics);
        }
    }

    private static AuthoredParameterSchema? ResolveSchema(
        AuthoredAction action,
        string? ownerKey,
        IReadOnlyDictionary<string, AuthoredParameterSchema> schemaByKey,
        IReadOnlyDictionary<string, ActionCatalogEntry> catalogByType,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(action.ParameterSchemaKey))
        {
            if (!schemaByKey.TryGetValue(action.ParameterSchemaKey, out var schema))
            {
                if (catalogByType.TryGetValue(action.Type, out var catalogEntry)
                    && string.Equals(catalogEntry.ParamsSchema.Key, action.ParameterSchemaKey, StringComparison.Ordinal))
                {
                    schema = catalogEntry.ParamsSchema;
                }
                else
                {
                    diagnostics.Add(Error("PROJ118",
                        $"Action '{action.Type}' references unknown parameter schema '{action.ParameterSchemaKey}'.",
                        ownerKey));
                    return null;
                }
            }

            if (schema.AppliesTo.Count > 0 && !schema.AppliesTo.Contains(action.Type, StringComparer.Ordinal))
            {
                diagnostics.Add(Error("PROJ119",
                    $"Parameter schema '{schema.Key}' cannot be used with action type '{action.Type}'.",
                    ownerKey));
            }

            return schema;
        }

        if (catalogByType.TryGetValue(action.Type, out var catalogEntryByType))
        {
            return catalogEntryByType.ParamsSchema;
        }

        var matches = schemaByKey.Values
            .Where(schema => schema.AppliesTo.Contains(action.Type, StringComparer.Ordinal))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static string GetAppliesTo(string ownerKind, ActionTiming timing) => (ownerKind, timing) switch
    {
        ("stage", ActionTiming.OnEntry) => ActionCatalogScopes.StageOnEntry,
        ("stage", ActionTiming.OnExit) => ActionCatalogScopes.StageOnExit,
        _ => ActionCatalogScopes.Transition
    };

    private static void ValidateAssignmentCompatibility(
        string ownerKey,
        string ownerKind,
        string? actor,
        IReadOnlyList<string> roleGates,
        AuthoredQueue queue,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(actor)
            && !string.IsNullOrWhiteSpace(queue.Actor)
            && !string.Equals(actor, queue.Actor, StringComparison.Ordinal))
        {
            diagnostics.Add(Error("PROJ135",
                $"{ownerKind} '{ownerKey}' actor '{actor}' does not match queue '{queue.Key}' actor '{queue.Actor}'.",
                ownerKey));
        }

        if (roleGates.Count > 0
            && queue.RoleGates.Count > 0
            && !roleGates.OrderBy(role => role, StringComparer.Ordinal)
                .SequenceEqual(queue.RoleGates.OrderBy(role => role, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            diagnostics.Add(Error("PROJ136",
                $"{ownerKind} '{ownerKey}' role gates do not match queue '{queue.Key}'.",
                ownerKey));
        }
    }

    private static void ValidateParameterObject(
        JsonObject? parameters,
        AuthoredParameterSchema schema,
        string actionType,
        string? ownerKey,
        List<ProjectionDiagnostic> diagnostics)
    {
        parameters ??= [];
        var propertyMap = schema.Properties.ToDictionary(property => property.Key, StringComparer.Ordinal);

        foreach (var requiredKey in schema.Required)
        {
            if (!parameters.TryGetPropertyValue(requiredKey, out var requiredNode) || requiredNode is null)
            {
                diagnostics.Add(Error("PROJ120",
                    $"Action '{actionType}' is missing required parameter '{requiredKey}'.",
                    ownerKey));
            }
        }

        foreach (var (key, node) in parameters)
        {
            if (!propertyMap.TryGetValue(key, out var definition))
            {
                if (!schema.AllowAdditionalProperties)
                {
                    diagnostics.Add(Error("PROJ121",
                        $"Action '{actionType}' includes unsupported parameter '{key}'.",
                        ownerKey));
                }

                continue;
            }

            if (node is null)
            {
                if (definition.ValueKind != ParameterValueKind.Null)
                {
                    diagnostics.Add(Error("PROJ122",
                        $"Action '{actionType}' parameter '{key}' cannot be null.",
                        ownerKey));
                }

                continue;
            }

            ValidateParameterValue(node, definition, actionType, ownerKey, diagnostics);
        }
    }

    private static void ValidateParameterValue(
        JsonNode node,
        AuthoredParameterDefinition definition,
        string actionType,
        string? ownerKey,
        List<ProjectionDiagnostic> diagnostics)
    {
        var actualKind = GetValueKind(node);

        if (!Matches(definition.ValueKind, actualKind))
        {
            diagnostics.Add(Error("PROJ123",
                $"Action '{actionType}' parameter '{definition.Key}' must be '{definition.ValueKind}' but was '{actualKind}'.",
                ownerKey));
            return;
        }

        if (definition.AllowedValues.Count > 0
            && actualKind == JsonValueKind.String
            && node.GetValue<string?>() is { } stringValue
            && !definition.AllowedValues.Contains(stringValue, StringComparer.Ordinal))
        {
            diagnostics.Add(Error("PROJ124",
                $"Action '{actionType}' parameter '{definition.Key}' must be one of: {string.Join(", ", definition.AllowedValues)}.",
                ownerKey));
        }

        if (definition.ValueKind == ParameterValueKind.Object && node is JsonObject childObject)
        {
            var childSchema = new AuthoredParameterSchema
            {
                Key = definition.Key,
                Properties = definition.Properties,
                AllowAdditionalProperties = true,
                Required = []
            };

            ValidateParameterObject(childObject, childSchema, actionType, ownerKey, diagnostics);
        }

        if (definition.ValueKind == ParameterValueKind.Array && node is JsonArray array && definition.Items is not null)
        {
            foreach (var item in array)
            {
                if (item is null)
                {
                    diagnostics.Add(Error("PROJ125",
                        $"Action '{actionType}' parameter '{definition.Key}' cannot contain null array items.",
                        ownerKey));
                    continue;
                }

                ValidateParameterValue(item, definition.Items, actionType, ownerKey, diagnostics);
            }
        }
    }

    private static JsonValueKind GetValueKind(JsonNode node)
    {
        var element = JsonSerializer.SerializeToElement(node);
        return element.ValueKind;
    }

    private static bool Matches(ParameterValueKind expected, JsonValueKind actual) => expected switch
    {
        ParameterValueKind.String => actual == JsonValueKind.String,
        ParameterValueKind.Number => actual == JsonValueKind.Number,
        ParameterValueKind.Integer => actual == JsonValueKind.Number,
        ParameterValueKind.Boolean => actual == JsonValueKind.True || actual == JsonValueKind.False,
        ParameterValueKind.Object => actual == JsonValueKind.Object,
        ParameterValueKind.Array => actual == JsonValueKind.Array,
        ParameterValueKind.Null => actual == JsonValueKind.Null,
        _ => false
    };

    private static ProjectionDiagnostic Error(string code, string message, string? stageKey) =>
        new() { Severity = DiagnosticSeverity.Error, Code = code, Message = message, StageKey = stageKey };
}
