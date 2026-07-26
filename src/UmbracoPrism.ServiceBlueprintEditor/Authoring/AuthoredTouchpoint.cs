using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// A single touchpoint in the authored blueprint. Maps to one runtime touchpoint on projection.
/// </summary>
public record AuthoredTouchpoint
{
    private TouchpointKind _kind = TouchpointKind.Question;
    private string? _unknownKindToken;
    private string? _queueKey;

    [JsonPropertyName("key")]
    public string TouchpointKey { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonIgnore]
    public TouchpointKind Kind
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

        if (Enum.TryParse<TouchpointKind>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(TouchpointKind), parsed))
        {
            _kind = parsed;
            _unknownKindToken = null;
            return;
        }

        _unknownKindToken = value;
    }
}
