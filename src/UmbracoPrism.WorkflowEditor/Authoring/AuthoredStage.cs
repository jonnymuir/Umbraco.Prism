using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// A single stage in the authored workflow. Maps to one runtime state on projection.
/// </summary>
public record AuthoredStage
{
    private StageKind _kind = StageKind.Question;
    private string? _unknownKindToken;
    private string? _queueKey;

    [JsonPropertyName("key")]
    public string StageKey { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonIgnore]
    public StageKind Kind
    {
        get => _kind;
        init => _kind = value;
    }

    [JsonPropertyName("type")]
    public string TypeRaw
    {
        get => _kind.ToString();
        init => ApplyKindToken(value);
    }

    [JsonIgnore]
    public string? UnknownKindToken => _unknownKindToken;

    [JsonPropertyName("actor")]
    public string? Actor { get; init; }

    [JsonPropertyName("queueKey")]
    public string QueueKey
    {
        get => _queueKey ?? string.Empty;
        init => _queueKey = value;
    }

    [JsonPropertyName("actions")]
    public IReadOnlyList<AuthoredAction> Actions { get; init; } = [];

    [JsonPropertyName("components")]
    public IReadOnlyList<PrismComponent> Components { get; init; } = [];

    [JsonPropertyName("roleGates")]
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    [JsonPropertyName("routes")]
    public IReadOnlyList<AuthoredRoute> Routes { get; init; } = [];

    [JsonPropertyName("editorComment")]
    public string? EditorComment { get; init; }

    private void ApplyKindToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (Enum.TryParse<StageKind>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(StageKind), parsed))
        {
            _kind = parsed;
            _unknownKindToken = null;
            return;
        }

        _unknownKindToken = value;
    }
}
