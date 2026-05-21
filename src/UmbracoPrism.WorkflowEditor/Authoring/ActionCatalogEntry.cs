using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Design-time descriptor for an action the workflow editor can offer to authors.
/// Reuses the authored parameter schema contract from issue #55 so catalog metadata and workflow
/// validation speak the same language.
/// </summary>
public record ActionCatalogEntry
{
    /// <summary>Stable action type key stored in authored workflow JSON.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>User-facing label shown in action pickers.</summary>
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    /// <summary>Short explanation of what the action does.</summary>
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    /// <summary>Where the action is valid, for example <c>stage.onEntry</c> or <c>transition</c>.</summary>
    [JsonPropertyName("appliesTo")]
    public IReadOnlyList<string> AppliesTo { get; init; } = [];

    /// <summary>Parameter schema used for editor rendering and authored-workflow validation.</summary>
    [JsonPropertyName("paramsSchema")]
    public required AuthoredParameterSchema ParamsSchema { get; init; }

    /// <summary>Resolved widget hints keyed by parameter path.</summary>
    [JsonPropertyName("parameterWidgets")]
    public IReadOnlyDictionary<string, string> ParameterWidgets { get; init; } = new Dictionary<string, string>();

    /// <summary>Starter values applied when an author first inserts the action.</summary>
    [JsonPropertyName("defaultParams")]
    public JsonObject DefaultParams { get; init; } = [];

    /// <summary>Design-time availability status.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = ActionCatalogStatuses.Available;

    /// <summary>Optional note describing the current runtime implementation status.</summary>
    [JsonPropertyName("runtimeImplementation")]
    public string? RuntimeImplementation { get; init; }
}
