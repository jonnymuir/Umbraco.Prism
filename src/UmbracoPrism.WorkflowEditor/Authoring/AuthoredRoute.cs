using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A single outgoing edge owned by an <see cref="AuthoredGateway"/>.
/// The gateway carries the <em>source</em> stage; each route carries the trigger,
/// optional condition, optional role gate, optional on-transition actions, and the target node
/// (another stage or another gateway).
/// </summary>
/// <remarks>
/// Gateways own all authored routing in the workflow. A stage cannot transition to another stage
/// directly — it always routes through a gateway whose <c>Source</c> is that stage.
/// </remarks>
public record AuthoredRoute
{
    /// <summary>
    /// Stable identifier for this route within its parent gateway.
    /// Used by patch operations (<c>update-route</c>, <c>delete-route</c>) so renames or
    /// reorders do not invalidate references in the editor's undo/redo log.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Stage key or gateway key this route arrives at.</summary>
    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    /// <summary>Action label that triggers this route (e.g. "submit", "continue").</summary>
    [JsonPropertyName("trigger")]
    public string Trigger { get; init; } = string.Empty;

    /// <summary>Optional structured condition that gates whether this route is available.</summary>
    [JsonPropertyName("condition")]
    public AuthoredCondition? Condition { get; init; }

    /// <summary>Optional role that must be present for this route to be taken at runtime.</summary>
    [JsonPropertyName("requiresRole")]
    public string? RequiresRole { get; init; }

    /// <summary>Typed actions that run as part of taking this route.</summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<AuthoredAction> Actions { get; init; } = [];
}
