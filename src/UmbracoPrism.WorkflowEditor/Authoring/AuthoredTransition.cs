using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A directed edge in the authored workflow graph.
/// Projects 1:1 to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowTransitionFile"/>.
/// </summary>
public record AuthoredTransition
{
    private string _fromStage = string.Empty;
    private string _toStage = string.Empty;
    private string _action = string.Empty;

    /// <summary>The <see cref="AuthoredStage.StageKey"/> this transition originates from.</summary>
    [JsonPropertyName("source")]
    public string FromStage
    {
        get => _fromStage;
        init => _fromStage = value;
    }

    [JsonPropertyName("fromStage")]
    public string? LegacyFromStage
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _fromStage = value;
        }
    }

    /// <summary>The <see cref="AuthoredStage.StageKey"/> this transition goes to.</summary>
    [JsonPropertyName("target")]
    public string ToStage
    {
        get => _toStage;
        init => _toStage = value;
    }

    [JsonPropertyName("toStage")]
    public string? LegacyToStage
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _toStage = value;
        }
    }

    /// <summary>Action label that triggers this transition (e.g. "submit", "continue").</summary>
    [JsonPropertyName("trigger")]
    public string Action
    {
        get => _action;
        init => _action = value;
    }

    [JsonPropertyName("action")]
    public string? LegacyAction
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _action = value;
        }
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
