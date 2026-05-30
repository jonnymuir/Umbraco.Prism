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
    private StageKind _kind = StageKind.Question;
    private string? _legacyKindRaw;
    private bool _hasLegacyWaitingPayload;

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
    [JsonIgnore]
    public StageKind Kind
    {
        get => _kind;
        init => _kind = value;
    }

    /// <summary>
    /// JSON-bound stage type token. Accepts any string so the validator can flag retired values
    /// (PROJ140: <c>Waiting</c>, <c>StatusTimeline</c>) at the JSON boundary; unknown tokens default
    /// to <see cref="StageKind.Question"/> at the runtime level but are preserved on
    /// <see cref="LegacyKindRaw"/> for diagnostic emission.
    /// </summary>
    [JsonPropertyName("type")]
    public string TypeRaw
    {
        get => _kind.ToString();
        init => ApplyKindToken(value);
    }

    /// <summary>Legacy alias for <see cref="Kind"/> retained for proposal payload compatibility.</summary>
    [JsonPropertyName("kind")]
    public string? LegacyKindLiteral
    {
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                ApplyKindToken(value);
        }
    }

    /// <summary>Raw stage-type token captured when JSON supplied a value that does not map to <see cref="StageKind"/>.</summary>
    [JsonIgnore]
    public string? LegacyKindRaw => _legacyKindRaw;

    private void ApplyKindToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (Enum.TryParse<StageKind>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(StageKind), parsed))
        {
            _kind = parsed;
            _legacyKindRaw = null;
        }
        else
        {
            _legacyKindRaw = value;
            _kind = StageKind.Question;
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

    /// <summary>
    /// Legacy waiting payload retained only to detect old authored documents at the JSON boundary.
    /// Waiting metadata belongs on join gateways; presence here triggers PROJ140.
    /// </summary>
    [JsonPropertyName("waiting")]
    public System.Text.Json.Nodes.JsonNode? LegacyWaitingPayload
    {
        init
        {
            if (value is not null)
                _hasLegacyWaitingPayload = true;
        }
    }

    /// <summary>True when the authored document carried stage-level waiting metadata that the validator should reject.</summary>
    [JsonIgnore]
    public bool HasLegacyWaitingPayload => _hasLegacyWaitingPayload;

    /// <summary>Editor-only comment, stripped during projection.</summary>
    [JsonPropertyName("editorComment")]
    public string? EditorComment { get; init; }
}
