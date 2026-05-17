using System.Text.Json.Serialization;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// The input type of an authored field. Maps to a concrete
/// <see cref="UmbracoPrism.Shared.Models.Workflow.Components.InputComponent"/> subtype on projection.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FieldType
{
    Text,
    Number,
    Decimal,
    Email,
    Date,
    Textarea,
    Boolean,
    Select,
    Radios,
    Checkboxes
}
