using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Named queue definition used to group authored stages and gateways under shared assignment data.
/// </summary>
public record AuthoredQueue
{
    private string? _key;

    [JsonPropertyName("key")]
    public string Key
    {
        get => _key ?? string.Empty;
        init => _key = value;
    }

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    [JsonIgnore]
    public string? QueueName
    {
        get => _key;
        init => _key = value;
    }

    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    [JsonPropertyName("tags")]
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}

