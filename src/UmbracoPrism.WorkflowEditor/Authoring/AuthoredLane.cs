using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Named lane definition used to group authored stages and gateways under shared assignment data.
/// </summary>
public record AuthoredLane
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    [JsonPropertyName("queueName")]
    public string? QueueName { get; init; }

    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];
}
