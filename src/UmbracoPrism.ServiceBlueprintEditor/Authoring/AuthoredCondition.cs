using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// A simple authored guard that explains or constrains whether a transition can be taken.
/// The runtime can interpret <see cref="Expression"/> later without making the authored model code-like.
/// </summary>
public record AuthoredCondition
{
    /// <summary>Condition kind so richer evaluators can be introduced without changing the outer transition shape.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "expression";

    /// <summary>Declarative condition expression understood by the blueprint validator/runtime.</summary>
    [JsonPropertyName("expression")]
    public required string Expression { get; init; }

    /// <summary>Optional human-readable explanation shown in the editor.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
