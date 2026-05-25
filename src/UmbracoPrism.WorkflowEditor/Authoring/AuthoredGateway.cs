using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Non-executable gateway definition preserved for future multi-lane runtime slices.
/// </summary>
public record AuthoredGateway
{
    [JsonPropertyName("key")]
    public string GatewayKey { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public GatewayKind Kind { get; init; } = GatewayKind.Split;

    [JsonPropertyName("laneKey")]
    public string LaneKey { get; init; } = string.Empty;

    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];
}
