using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// A single outgoing edge owned by an <see cref="AuthoredGateway"/>.
/// The gateway carries the <em>source</em> touchpoint; each route carries the trigger,
/// optional condition, optional role gate, optional on-transition actions, and the target node
/// (another touchpoint or another gateway).
/// </summary>
/// <remarks>
/// Gateways own all authored routing in the blueprint. A touchpoint cannot transition to another touchpoint
/// directly — it always routes through a gateway whose <c>Source</c> is that touchpoint.
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

    /// <summary>Touchpoint key or gateway key this route arrives at.</summary>
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
