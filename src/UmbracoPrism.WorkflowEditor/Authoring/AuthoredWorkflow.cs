using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Editor-native representation of a workflow — the human and agent authoring source of truth.
/// Never loaded directly by the Prism runtime; compiled to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile"/>
/// by <see cref="IWorkflowProjector"/>.
/// </summary>
public record AuthoredWorkflow
{
    private IReadOnlyList<AuthoredQueue> _queues = [];

    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("definitionKey")]
    public required string DefinitionKey { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("initialStageKey")]
    public required string InitialStageKey { get; init; }

    [JsonPropertyName("instancePolicy")]
    public string InstancePolicy { get; init; } = "single";

    [JsonPropertyName("stages")]
    public IReadOnlyList<AuthoredStage> Stages { get; init; } = [];

    [JsonPropertyName("queues")]
    public IReadOnlyList<AuthoredQueue> Queues
    {
        get => _queues;
        init => _queues = value;
    }

    [JsonPropertyName("gateways")]
    public IReadOnlyList<AuthoredGateway> Gateways { get; init; } = [];

    [JsonPropertyName("handoffs")]
    public IReadOnlyList<AuthoredHandoff> Handoffs { get; init; } = [];

    [JsonPropertyName("parameterSchemas")]
    public IReadOnlyList<AuthoredParameterSchema> ParameterSchemas { get; init; } = [];

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("authorNote")]
    public string? AuthorNote { get; init; }

    [JsonIgnore]
    public IReadOnlyList<AuthoredQueue> Lanes
    {
        get => _queues;
        init => _queues = value;
    }

    [JsonPropertyName("lanes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AuthoredLane>? LegacyLanes
    {
        init
        {
            if (_queues.Count == 0 && value is not null)
            {
                _queues = value.ToArray();
            }
        }
    }
}
