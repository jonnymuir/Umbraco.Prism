using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Gateway definition: a first-class named routing/convergence point in the workflow graph.
/// Split gateways fan one cursor out into many lane-owned cursors.
/// Join gateways collect cursors from required incoming lanes before releasing the next step.
/// </summary>
public record AuthoredGateway
{
    [JsonPropertyName("key")]
    public string GatewayKey { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Optional description shown to authors in the editor inspector.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public GatewayKind Kind { get; init; } = GatewayKind.Split;

    [JsonPropertyName("laneKey")]
    public string LaneKey { get; init; } = string.Empty;

    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    /// <summary>
    /// Waiting copy shown to the owner lane while a join gateway awaits other lanes.
    /// Required for join gateways; ignored on split gateways.
    /// </summary>
    [JsonPropertyName("waitingInfo")]
    public WaitingMetadata? WaitingInfo { get; init; }

    /// <summary>
    /// Lane keys whose cursors must all arrive before this join gateway releases.
    /// Required for join gateways; ignored on split gateways.
    /// </summary>
    [JsonPropertyName("requiredIncomingLanes")]
    public IReadOnlyList<string> RequiredIncomingLanes { get; init; } = [];
}
