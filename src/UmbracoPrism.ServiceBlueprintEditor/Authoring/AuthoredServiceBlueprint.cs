using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Editor-native representation of a blueprint — the human and agent authoring source of truth.
/// Never loaded directly by the Prism runtime; compiled to <see cref="UmbracoPrism.Shared.Models.ServiceDesign.ServiceBlueprint"/>
/// by <see cref="IServiceBlueprintProjector"/>.
/// </summary>
public record AuthoredServiceBlueprint
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

    [JsonPropertyName("initialTouchpointKey")]
    public required string InitialTouchpointKey { get; init; }

    [JsonPropertyName("requestPolicy")]
    public string RequestPolicy { get; init; } = "single";

    [JsonPropertyName("touchpoints")]
    public IReadOnlyList<AuthoredTouchpoint> Touchpoints { get; init; } = [];

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

}
