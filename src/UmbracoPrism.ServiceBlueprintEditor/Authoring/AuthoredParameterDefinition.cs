using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Describes one parameter in a reusable authored parameter schema.
/// </summary>
public record AuthoredParameterDefinition
{
    /// <summary>Parameter key inside the owning schema.</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Optional display title for editor rendering.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Optional description/help text.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Expected JSON value kind for the parameter.</summary>
    [JsonPropertyName("valueKind")]
    public ParameterValueKind ValueKind { get; init; } = ParameterValueKind.String;

    /// <summary>Optional format hint such as <c>email</c>, <c>date</c>, or <c>duration</c>.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    /// <summary>Optional authoring control hint such as <c>text</c>, <c>select</c>, or <c>textarea</c>.</summary>
    [JsonPropertyName("editor")]
    public string? Editor { get; init; }

    /// <summary>Optional allowed values for enum-like parameters.</summary>
    [JsonPropertyName("allowedValues")]
    public IReadOnlyList<string> AllowedValues { get; init; } = [];

    /// <summary>Optional default value suggested by the schema.</summary>
    [JsonPropertyName("defaultValue")]
    public JsonNode? DefaultValue { get; init; }

    /// <summary>Nested object properties when <see cref="ValueKind"/> is <see cref="ParameterValueKind.Object"/>.</summary>
    [JsonPropertyName("properties")]
    public IReadOnlyList<AuthoredParameterDefinition> Properties { get; init; } = [];

    /// <summary>Array item schema when <see cref="ValueKind"/> is <see cref="ParameterValueKind.Array"/>.</summary>
    [JsonPropertyName("items")]
    public AuthoredParameterDefinition? Items { get; init; }
}
