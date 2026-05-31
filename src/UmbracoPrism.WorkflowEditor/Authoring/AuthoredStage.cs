using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A single stage in the authored workflow. Maps to one runtime state on projection.
/// The <see cref="Kind"/> property drives component shell selection; shell type is ultimately
/// confirmed by <see cref="UmbracoPrism.Shared.Extensions.PrismComponentExtensions.InferStepType"/>.
/// </summary>
public record AuthoredStage
{
    private StageKind _kind = StageKind.Question;
    private string? _unknownKindToken;

    /// <summary>Stable key unique within this workflow. Maps to <c>StepDefinition.StateKey</c> on projection.</summary>
    [JsonPropertyName("key")]
    public string StageKey { get; init; } = string.Empty;

    /// <summary>User-facing title rendered by the editor and projected runtime state.</summary>
    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

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
    /// JSON-bound stage type token. Unknown tokens are captured on
    /// <see cref="UnknownKindToken"/> so the validator can fail loudly (PROJ005)
    /// instead of silently rewriting the kind.
    /// </summary>
    [JsonPropertyName("type")]
    public string TypeRaw
    {
        get => _kind.ToString();
        init => ApplyKindToken(value);
    }

    /// <summary>
    /// Raw stage-type token captured when JSON supplied a value that does not map to <see cref="StageKind"/>.
    /// Surfaces in PROJ005 diagnostics; null on well-formed inputs.
    /// </summary>
    [JsonIgnore]
    public string? UnknownKindToken => _unknownKindToken;

    private void ApplyKindToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (Enum.TryParse<StageKind>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(StageKind), parsed))
        {
            _kind = parsed;
            _unknownKindToken = null;
        }
        else
        {
            _unknownKindToken = value;
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
    /// Components shown by this stage. Authored as a polymorphic tree using the
    /// shared <see cref="PrismComponent"/> hierarchy (fieldset, body, inset-text,
    /// summary-list, panel, every input kind, etc.). The projector hands this tree
    /// straight through to the runtime; nothing wraps it implicitly.
    /// </summary>
    [JsonPropertyName("components")]
    public IReadOnlyList<PrismComponent> Components { get; init; } = [];

    /// <summary>Roles that may enter this stage. Empty means any authenticated principal.</summary>
    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    /// <summary>Editor-only comment, stripped during projection.</summary>
    [JsonPropertyName("editorComment")]
    public string? EditorComment { get; init; }
}
