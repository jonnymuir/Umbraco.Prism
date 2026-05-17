using System.Text.Json.Serialization;

namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// Intent hint used by the projector to determine the component shell for a stage.
/// The projector emits components whose presence satisfies the existing
/// <see cref="UmbracoPrism.Shared.Extensions.PrismComponentExtensions.InferStepType"/> rules.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StageKind
{
    /// <summary>Default interactive input stage — emits a FieldsetComponent. Infers "question".</summary>
    Question,

    /// <summary>Summary review stage — emits a SummaryListComponent. Infers "check-answers".</summary>
    CheckAnswers,

    /// <summary>Terminal success stage — emits a PanelComponent. Infers "confirmation".</summary>
    Confirmation,

    /// <summary>Task dashboard stage — emits a TaskListComponent. Infers "task-list".</summary>
    TaskList,

    /// <summary>Polling/waiting stage — emits a WaitingComponent. Infers "status-timeline".</summary>
    Waiting,

    /// <summary>Alias for <see cref="Waiting"/>. Emits a WaitingComponent. Infers "status-timeline".</summary>
    StatusTimeline
}
