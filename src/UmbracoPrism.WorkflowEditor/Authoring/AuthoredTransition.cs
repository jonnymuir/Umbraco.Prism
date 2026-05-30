using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A directed edge in the authored workflow graph.
/// Projects 1:1 to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowTransitionFile"/>.
/// </summary>
public record AuthoredTransition
{
    private string _source = string.Empty;
    private string _target = string.Empty;
    private string _trigger = string.Empty;

    /// <summary>The stage or gateway key this transition originates from.</summary>
    [JsonPropertyName("source")]
    public string Source
    {
        get => _source;
        init => _source = value;
    }

    [JsonPropertyName("fromStage")]
    public string? LegacyFromStage
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _source = value;
        }
    }

    /// <summary>Compatibility shim for older C# callers that still refer to stage-based names.</summary>
    [JsonIgnore]
    [Obsolete("Use Source/Target/Trigger. Removed in next major.", error: false)]
    public string FromStage
    {
        get => _source;
        init => _source = value;
    }

    /// <summary>The stage or gateway key this transition points at.</summary>
    [JsonPropertyName("target")]
    public string Target
    {
        get => _target;
        init => _target = value;
    }

    [JsonPropertyName("toStage")]
    public string? LegacyToStage
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _target = value;
        }
    }

    /// <summary>Compatibility shim for older C# callers that still refer to stage-based names.</summary>
    [JsonIgnore]
    [Obsolete("Use Source/Target/Trigger. Removed in next major.", error: false)]
    public string ToStage
    {
        get => _target;
        init => _target = value;
    }

    /// <summary>Action label that triggers this transition (e.g. "submit", "continue").</summary>
    [JsonPropertyName("trigger")]
    public string Trigger
    {
        get => _trigger;
        init => _trigger = value;
    }

    [JsonPropertyName("action")]
    public string? LegacyAction
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _trigger = value;
        }
    }

    /// <summary>Compatibility shim for older C# callers that still refer to stage-based names.</summary>
    [JsonIgnore]
    [Obsolete("Use Source/Target/Trigger. Removed in next major.", error: false)]
    public string Action
    {
        get => _trigger;
        init => _trigger = value;
    }

    /// <summary>Optional structured conditions that gate whether the transition is available.</summary>
    [JsonPropertyName("conditions")]
    public IReadOnlyList<AuthoredCondition> Conditions { get; init; } = [];

    [JsonPropertyName("condition")]
    public string? LegacyCondition
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                Conditions =
                [
                    new AuthoredCondition
                    {
                        Expression = value
                    }
                ];
        }
    }

    /// <summary>Typed actions that run as part of taking the transition.</summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<AuthoredAction> Actions { get; init; } = [];

    /// <summary>Optional role that must be present for this transition to be valid at runtime.</summary>
    [JsonPropertyName("requiresRole")]
    public string? RequiresRole { get; init; }
}
