using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A directed edge in the authored workflow graph.
/// Projects 1:1 to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowTransitionFile"/>.
/// </summary>
public record AuthoredTransition
{
    /// <summary>The stage or gateway key this transition originates from.</summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    /// <summary>The stage or gateway key this transition points at.</summary>
    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    /// <summary>Action label that triggers this transition (e.g. "submit", "continue").</summary>
    [JsonPropertyName("trigger")]
    public string Trigger { get; init; } = string.Empty;

    /// <summary>Optional structured conditions that gate whether the transition is available.</summary>
    [JsonPropertyName("conditions")]
    public IReadOnlyList<AuthoredCondition> Conditions { get; init; } = [];

    /// <summary>Typed actions that run as part of taking the transition.</summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<AuthoredAction> Actions { get; init; } = [];

    /// <summary>Optional role that must be present for this transition to be valid at runtime.</summary>
    [JsonPropertyName("requiresRole")]
    public string? RequiresRole { get; init; }
}
