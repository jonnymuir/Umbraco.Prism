using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A single stage in the authored workflow. Maps to one runtime state on projection.
/// The <see cref="Kind"/> property drives component shell selection; shell type is ultimately
/// confirmed by <see cref="UmbracoPrism.Shared.Extensions.PrismComponentExtensions.InferStepType"/>.
/// </summary>
public record AuthoredStage
{
    private string _stageKey = string.Empty;
    private string _displayName = string.Empty;

    /// <summary>Stable key unique within this workflow. Maps to <c>StepDefinition.StateKey</c> on projection.</summary>
    [JsonPropertyName("key")]
    public string StageKey
    {
        get => _stageKey;
        init => _stageKey = value;
    }

    /// <summary>Legacy alias for <see cref="StageKey"/> retained for proposal payload compatibility.</summary>
    [JsonPropertyName("stageKey")]
    public string? LegacyStageKey
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _stageKey = value;
        }
    }

    /// <summary>User-facing title rendered by the editor and projected runtime state.</summary>
    [JsonPropertyName("title")]
    public string DisplayName
    {
        get => _displayName;
        init => _displayName = value;
    }

    /// <summary>Legacy alias for <see cref="DisplayName"/> retained for proposal payload compatibility.</summary>
    [JsonPropertyName("displayName")]
    public string? LegacyDisplayName
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                _displayName = value;
        }
    }

    /// <summary>Optional description to explain the purpose of the stage to authors.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Shell intent used by the projector to choose which components to emit.</summary>
    [JsonPropertyName("type")]
    public StageKind Kind { get; init; } = StageKind.Question;

    /// <summary>Legacy alias for <see cref="Kind"/> retained for proposal payload compatibility.</summary>
    [JsonPropertyName("kind")]
    public StageKind? LegacyKind
    {
        init
        {
            if (value is not null)
                Kind = value.Value;
        }
    }

    /// <summary>
    /// The role or persona responsible for acting on this stage (e.g. "applicant", "caseworker").
    /// Informational; not projected into the runtime.
    /// </summary>
    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    /// <summary>Optional named lane that owns this stage.</summary>
    [JsonPropertyName("laneKey")]
    public string? LaneKey { get; init; }

    /// <summary>Typed actions that run when the workflow enters or exits this stage.</summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<AuthoredAction> Actions { get; init; } = [];

    /// <summary>
    /// Fields collected or displayed in this stage.
    /// For <see cref="StageKind.Question"/> stages these become InputComponents inside a FieldsetComponent.
    /// For <see cref="StageKind.CheckAnswers"/> stages on the parent workflow they populate the SummaryListComponent.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<AuthoredField> Fields { get; init; } = [];

    /// <summary>Roles that may enter this stage. Empty means any authenticated principal.</summary>
    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    /// <summary>Waiting configuration, required for <see cref="StageKind.Waiting"/> and <see cref="StageKind.StatusTimeline"/> stages.</summary>
    [JsonPropertyName("waiting")]
    public WaitingMetadata? Waiting { get; init; }

    /// <summary>Editor-only comment, stripped during projection.</summary>
    [JsonPropertyName("editorComment")]
    public string? EditorComment { get; init; }
}
