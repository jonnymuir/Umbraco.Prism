namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// A reusable field definition within the authored workflow.
/// On projection, each <see cref="AuthoredField"/> is mapped to a concrete
/// <see cref="UmbracoPrism.Shared.Models.Workflow.Components.InputComponent"/> subtype.
/// </summary>
public record AuthoredField
{
    /// <summary>Stable identifier used to reference this field from stage views.</summary>
    public required string Key { get; init; }

    /// <summary>User-facing label rendered next to the input.</summary>
    public required string Label { get; init; }

    /// <summary>Component type to emit on projection.</summary>
    public FieldType Type { get; init; } = FieldType.Text;

    /// <summary>Whether the field is mandatory.</summary>
    public bool Required { get; init; }

    /// <summary>Optional hint text shown beneath the label.</summary>
    public string? Hint { get; init; }

    /// <summary>Optional HTML5 pattern (regex) for client-side validation.</summary>
    public string? ValidationPattern { get; init; }

    /// <summary>Available options for Select, Radios, and Checkboxes fields.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];
}
