using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Gateway definition: a first-class named routing or convergence point in the blueprint graph.
/// </summary>
public record AuthoredGateway
{
    private string? _queueKey;
    private string? _source;
    private IReadOnlyList<string> _requiredIncomingQueues = [];

    [JsonPropertyName("key")]
    public string GatewayKey { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public GatewayKind Kind { get; init; } = GatewayKind.Split;

    [JsonPropertyName("queueKey")]
    public string QueueKey
    {
        get => _queueKey ?? string.Empty;
        init => _queueKey = value;
    }

    [JsonPropertyName("routes")]
    public IReadOnlyList<AuthoredRoute> Routes { get; init; } = [];

    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    [JsonPropertyName("waitingInfo")]
    public WaitingMetadata? WaitingInfo { get; init; }

    [JsonPropertyName("requiredIncomingQueues")]
    public IReadOnlyList<string> RequiredIncomingQueues
    {
        get => _requiredIncomingQueues;
        init => _requiredIncomingQueues = value;
    }

    [JsonIgnore]
    public string? Source
    {
        get => _source;
        init => _source = value;
    }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacySource
    {
        init => _source = value;
    }

}
