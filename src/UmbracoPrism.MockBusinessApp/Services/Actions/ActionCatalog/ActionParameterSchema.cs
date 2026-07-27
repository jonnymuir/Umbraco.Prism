using System.Text.Json.Serialization;

namespace UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;

/// <summary>
/// Reusable parameter schema definition for a catalog action.
/// Describes the expected shape of an action's <c>params</c> object so the editor and
/// validation pipeline agree on the same contract.
/// </summary>
public record ActionParameterSchema
{
    /// <summary>Stable schema key referenced by an action's <c>parameterSchemaKey</c>.</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Optional display title for authoring UIs.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Optional description explaining what this schema configures.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Action types that can legally reference this schema.</summary>
    [JsonPropertyName("appliesTo")]
    public IReadOnlyList<string> AppliesTo { get; init; } = [];

    /// <summary>Root value kind. V1 primarily expects <see cref="ParameterValueKind.Object"/>.</summary>
    [JsonPropertyName("valueKind")]
    public ParameterValueKind ValueKind { get; init; } = ParameterValueKind.Object;

    /// <summary>Whether unknown properties are allowed in the configured parameter object.</summary>
    [JsonPropertyName("allowAdditionalProperties")]
    public bool AllowAdditionalProperties { get; init; } = true;

    /// <summary>Property definitions for object-shaped parameters.</summary>
    [JsonPropertyName("properties")]
    public IReadOnlyList<ActionParameterDefinition> Properties { get; init; } = [];

    /// <summary>Keys that must be present in the authored parameter object.</summary>
    [JsonPropertyName("required")]
    public IReadOnlyList<string> Required { get; init; } = [];
}
