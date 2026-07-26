using System.Text.Json.Serialization;

namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Intent hint used by the projector to determine the component shell for a touchpoint.
/// The projector emits components whose presence satisfies the existing
/// <see cref="UmbracoPrism.Shared.Extensions.PrismComponentExtensions.InferStepType"/> rules.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TouchpointKind
{
    /// <summary>Default interactive input touchpoint — emits a FieldsetComponent. Infers "question".</summary>
    Question,

    /// <summary>Summary review touchpoint — emits a SummaryListComponent. Infers "check-answers".</summary>
    CheckAnswers,

    /// <summary>Terminal success touchpoint — emits a PanelComponent. Infers "confirmation".</summary>
    Confirmation,

    /// <summary>Task dashboard touchpoint — emits a TaskListComponent. Infers "task-list".</summary>
    TaskList
}
