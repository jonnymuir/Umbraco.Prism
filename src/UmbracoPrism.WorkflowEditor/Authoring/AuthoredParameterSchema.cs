using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Reusable parameter schema definition for authored actions.
/// A workflow may carry these definitions so the editor and validation pipeline can agree on
/// the expected shape of <see cref="AuthoredAction.Parameters"/>.
/// </summary>
public record AuthoredParameterSchema
{
    /// <summary>Stable schema key referenced by <see cref="AuthoredAction.ParameterSchemaKey"/>.</summary>
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
    public IReadOnlyList<AuthoredParameterDefinition> Properties { get; init; } = [];

    /// <summary>Keys that must be present in the authored parameter object.</summary>
    [JsonPropertyName("required")]
    public IReadOnlyList<string> Required { get; init; } = [];
}
