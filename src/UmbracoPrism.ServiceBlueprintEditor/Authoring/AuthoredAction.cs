using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// A typed unit of blueprint behaviour. Actions stay declarative in authored JSON and are resolved
/// by type key at runtime through a handler registry.
/// </summary>
public record AuthoredAction
{
    /// <summary>Stable action type key (for example <c>forms.submit</c> or <c>case.assign</c>).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>When this action executes relative to the touchpoint or transition that owns it.</summary>
    [JsonPropertyName("timing")]
    public ActionTiming Timing { get; init; }

    /// <summary>Concrete parameter values for this authored action instance.</summary>
    [JsonPropertyName("params")]
    public JsonObject Parameters { get; init; } = [];

    /// <summary>Optional reference to a reusable parameter schema declared on the parent blueprint.</summary>
    [JsonPropertyName("parameterSchemaKey")]
    public string? ParameterSchemaKey { get; init; }

    /// <summary>Optional editor-facing summary for this specific configured action.</summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }
}
