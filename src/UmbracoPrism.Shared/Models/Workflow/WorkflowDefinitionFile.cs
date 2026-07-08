using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Shared.Models.Workflow;

/// <summary>
/// Persisted workflow definition contract shared by authoring, seed files and runtime loading.
/// </summary>
public record WorkflowDefinitionFile
{
    /// <summary>
    /// Validates that every state route targets a gateway, never another state directly.
    /// Gateway routes may target either states or gateways.
    /// Returns one error message per violation; empty list means the workflow is valid.
    /// </summary>
    public IReadOnlyList<string> ValidateGatewayRouting()
    {
        var gatewayKeys = (Gateways ?? [])
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var stateKeys = States
            .Where(s => !string.IsNullOrWhiteSpace(s.StateKey))
            .Select(s => s.StateKey)
            .ToHashSet(StringComparer.Ordinal);

        var errors = new List<string>();

        foreach (var state in States)
        {
            foreach (var route in state.Routes ?? [])
            {
                if (string.IsNullOrWhiteSpace(route.Target))
                {
                    continue;
                }

                if (stateKeys.Contains(route.Target))
                {
                    errors.Add(
                        $"State '{state.StateKey}' route '{route.Id}' targets state '{route.Target}' directly. " +
                        "Routes from states must always target a gateway.");
                }
            }
        }

        return errors;
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowHandoffDefinition>? Handoffs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

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
