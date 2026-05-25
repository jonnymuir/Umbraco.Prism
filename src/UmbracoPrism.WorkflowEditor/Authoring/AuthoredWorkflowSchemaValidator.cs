using System.Text.Json;
using System.Text.Json.Nodes;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Structural validator for the authored workflow schema.
/// Keeps authoring-only concerns out of the projector while still returning the same diagnostic shape.
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

        if (string.IsNullOrWhiteSpace(authored.DefinitionKey))
            diagnostics.Add(Error("PROJ100", "DefinitionKey is required.", null));

        if (string.IsNullOrWhiteSpace(authored.DisplayName))
            diagnostics.Add(Error("PROJ101", "DisplayName is required.", null));

        if (string.IsNullOrWhiteSpace(authored.InitialStageKey))
            diagnostics.Add(Error("PROJ102", "InitialStageKey is required.", null));

        if (authored.Stages.Count == 0)
            diagnostics.Add(Error("PROJ103", "At least one stage is required.", null));

        var schemaByKey = BuildSchemaMap(authored.ParameterSchemas, diagnostics);
        var lanesByKey = BuildLaneMap(authored.Lanes, diagnostics);
        var gatewayKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stage in authored.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.DisplayName))
            {
                diagnostics.Add(Error("PROJ104",
                    $"Stage '{stage.StageKey}' must define a title.", stage.StageKey));
            }

            if ((stage.Kind == StageKind.Waiting || stage.Kind == StageKind.StatusTimeline) && stage.Waiting is null)
            {
                diagnostics.Add(Error("PROJ105",
                    $"Stage '{stage.StageKey}' of type '{stage.Kind}' must define waiting metadata.", stage.StageKey));
            }

            if (!string.IsNullOrWhiteSpace(stage.LaneKey))
            {
                if (!lanesByKey.TryGetValue(stage.LaneKey, out var lane))
                {
                    diagnostics.Add(Error("PROJ129",
                        $"Stage '{stage.StageKey}' references unknown lane '{stage.LaneKey}'.", stage.StageKey));
                }
                else
                {
                    ValidateAssignmentCompatibility(stage.StageKey, "Stage", stage.Actor, stage.RoleGates, lane, diagnostics);
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

        foreach (var gateway in authored.Gateways)
        {
            if (string.IsNullOrWhiteSpace(gateway.GatewayKey))
            {
                diagnostics.Add(Error("PROJ130", "Gateway key is required.", null));
                continue;
            }

            if (!gatewayKeys.Add(gateway.GatewayKey))
            {
                diagnostics.Add(Error("PROJ131", $"Duplicate gateway key '{gateway.GatewayKey}'.", gateway.GatewayKey));
            }

            if (string.IsNullOrWhiteSpace(gateway.DisplayName))
            {
                diagnostics.Add(Error("PROJ132",
                    $"Gateway '{gateway.GatewayKey}' must define a title.", gateway.GatewayKey));
            }

            if (string.IsNullOrWhiteSpace(gateway.LaneKey))
            {
                diagnostics.Add(Error("PROJ133",
                    $"Gateway '{gateway.GatewayKey}' must reference a lane.", gateway.GatewayKey));
                continue;
            }

            if (!lanesByKey.TryGetValue(gateway.LaneKey, out var lane))
            {
                diagnostics.Add(Error("PROJ134",
                    $"Gateway '{gateway.GatewayKey}' references unknown lane '{gateway.LaneKey}'.", gateway.GatewayKey));
                continue;
            }

            ValidateAssignmentCompatibility(gateway.GatewayKey, "Gateway", gateway.Actor, gateway.RoleGates, lane, diagnostics);

            if (gateway.Kind == GatewayKind.Join)
            {
                if (gateway.WaitingInfo is null)
                {
                    diagnostics.Add(Error("PROJ137",
                        $"Join gateway '{gateway.GatewayKey}' must define waitingInfo.", gateway.GatewayKey));
                }

                if (gateway.RequiredIncomingLanes.Count == 0)
                {
                    diagnostics.Add(Error("PROJ138",
                        $"Join gateway '{gateway.GatewayKey}' must define at least one requiredIncomingLane.", gateway.GatewayKey));
                }
                else
                {
                    foreach (var requiredLane in gateway.RequiredIncomingLanes)
                    {
                        if (!lanesByKey.ContainsKey(requiredLane))
                        {
                            diagnostics.Add(Error("PROJ139",
                                $"Join gateway '{gateway.GatewayKey}' requiredIncomingLane '{requiredLane}' references an unknown lane.",
                                gateway.GatewayKey));
                        }
                    }
                }
            }
        }

        foreach (var transition in authored.Transitions)
        {
            if (string.IsNullOrWhiteSpace(transition.FromStage))
                diagnostics.Add(Error("PROJ106", "Transition source is required.", null));

            if (string.IsNullOrWhiteSpace(transition.ToStage))
                diagnostics.Add(Error("PROJ107", "Transition target is required.", null));

            if (string.IsNullOrWhiteSpace(transition.Action))
                diagnostics.Add(Error("PROJ108", "Transition trigger is required.", transition.FromStage));

            foreach (var condition in transition.Conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.Expression))
                {
                    diagnostics.Add(Error("PROJ109",
                        $"Transition '{transition.FromStage}' → '{transition.ToStage}' contains a condition with no expression.",
                        transition.FromStage));
                }
            }

            ValidateActions(
                transition.Actions,
                transition.FromStage,
                "transition",
                new HashSet<ActionTiming> { ActionTiming.OnTransition },
                schemaByKey,
                catalogByType,
                diagnostics);
        }
    }

    private static Dictionary<string, AuthoredLane> BuildLaneMap(
        IReadOnlyList<AuthoredLane> lanes,
        List<ProjectionDiagnostic> diagnostics)
    {
        var map = new Dictionary<string, AuthoredLane>(StringComparer.Ordinal);

        foreach (var lane in lanes)
        {
            if (string.IsNullOrWhiteSpace(lane.Key))
            {
                diagnostics.Add(Error("PROJ127", "Lane key is required.", null));
                continue;
            }

            if (!map.TryAdd(lane.Key, lane))
            {
                diagnostics.Add(Error("PROJ128", $"Duplicate lane key '{lane.Key}'.", lane.Key));
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
                .Select(p => p.Key)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var requiredKey in schema.Required)
            {
                if (!definedKeys.Contains(requiredKey))
                {
                    diagnostics.Add(Error("PROJ112",
                        $"Parameter schema '{schema.Key}' marks '{requiredKey}' as required but does not define it.", null));
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
                    $"Parameter schema '{schemaKey}' contains a property with no key.", null));
                continue;
            }

            if (!seen.Add(definition.Key))
            {
                diagnostics.Add(Error("PROJ114",
                    $"Parameter schema '{schemaKey}' contains duplicate property key '{definition.Key}'.", null));
            }

            if (definition.ValueKind == ParameterValueKind.Array && definition.Items is null)
            {
                diagnostics.Add(Error("PROJ115",
                    $"Parameter '{definition.Key}' in schema '{schemaKey}' is an array but does not define item metadata.", null));
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
                    $"A {ownerKind} action in '{ownerKey ?? "workflow"}' must define a type.", ownerKey));
                continue;
            }

            if (!allowedTimings.Contains(action.Timing))
            {
                diagnostics.Add(Error("PROJ117",
                    $"Action '{action.Type}' on {ownerKind} '{ownerKey}' has invalid timing '{action.Timing}'.", ownerKey));
            }

            var appliesTo = GetAppliesTo(ownerKind, action.Timing);

            if (catalogByType.TryGetValue(action.Type, out var catalogEntry) &&
                catalogEntry.AppliesTo.Count > 0 &&
                !catalogEntry.AppliesTo.Contains(appliesTo, StringComparer.Ordinal))
            {
                diagnostics.Add(Error("PROJ126",
                    $"Action '{action.Type}' cannot run at '{appliesTo}'.", ownerKey));
            }

            var schema = ResolveSchema(action, ownerKey, schemaByKey, catalogByType, diagnostics);
            if (schema is null)
                continue;

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
                if (catalogByType.TryGetValue(action.Type, out var catalogEntry) &&
                    string.Equals(catalogEntry.ParamsSchema.Key, action.ParameterSchemaKey, StringComparison.Ordinal))
                {
                    schema = catalogEntry.ParamsSchema;
                }
                else
                {
                    diagnostics.Add(Error("PROJ118",
                        $"Action '{action.Type}' references unknown parameter schema '{action.ParameterSchemaKey}'.", ownerKey));
                    return null;
                }
            }

            if (schema.AppliesTo.Count > 0 && !schema.AppliesTo.Contains(action.Type, StringComparer.Ordinal))
            {
                diagnostics.Add(Error("PROJ119",
                    $"Parameter schema '{schema.Key}' cannot be used with action type '{action.Type}'.", ownerKey));
            }

            return schema;
        }

        if (catalogByType.TryGetValue(action.Type, out var catalogEntryByType))
            return catalogEntryByType.ParamsSchema;

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
        AuthoredLane lane,
        List<ProjectionDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(actor)
            && !string.IsNullOrWhiteSpace(lane.Actor)
            && !string.Equals(actor, lane.Actor, StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "PROJ135",
                $"{ownerKind} '{ownerKey}' actor '{actor}' does not match lane '{lane.Key}' actor '{lane.Actor}'.",
                ownerKey));
        }

        if (roleGates.Count > 0
            && lane.RoleGates.Count > 0
            && !roleGates.OrderBy(role => role, StringComparer.Ordinal)
                .SequenceEqual(lane.RoleGates.OrderBy(role => role, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "PROJ136",
                $"{ownerKind} '{ownerKey}' role gates do not match lane '{lane.Key}'.",
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
        var propertyMap = schema.Properties.ToDictionary(p => p.Key, StringComparer.Ordinal);

        foreach (var requiredKey in schema.Required)
        {
            if (!parameters.TryGetPropertyValue(requiredKey, out var requiredNode) || requiredNode is null)
            {
                diagnostics.Add(Error("PROJ120",
                    $"Action '{actionType}' is missing required parameter '{requiredKey}'.", ownerKey));
            }
        }

        foreach (var (key, node) in parameters)
        {
            if (!propertyMap.TryGetValue(key, out var definition))
            {
                if (!schema.AllowAdditionalProperties)
                {
                    diagnostics.Add(Error("PROJ121",
                        $"Action '{actionType}' includes unsupported parameter '{key}'.", ownerKey));
                }

                continue;
            }

            if (node is null)
            {
                if (definition.ValueKind != ParameterValueKind.Null)
                {
                    diagnostics.Add(Error("PROJ122",
                        $"Action '{actionType}' parameter '{key}' cannot be null.", ownerKey));
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

        if (definition.AllowedValues.Count > 0 &&
            actualKind == JsonValueKind.String &&
            node.GetValue<string?>() is { } stringValue &&
            !definition.AllowedValues.Contains(stringValue, StringComparer.Ordinal))
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
                        $"Action '{actionType}' parameter '{definition.Key}' cannot contain null array items.", ownerKey));
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
