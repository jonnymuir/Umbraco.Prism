namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// A directed edge in the authored workflow graph.
/// Projects 1:1 to <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowTransitionFile"/>.
/// </summary>
public record AuthoredTransition
{
    /// <summary>The <see cref="AuthoredStage.StageKey"/> this transition originates from.</summary>
    public required string FromStage { get; init; }

    /// <summary>The <see cref="AuthoredStage.StageKey"/> this transition goes to.</summary>
    public required string ToStage { get; init; }

    /// <summary>Action name that triggers this transition (e.g. "submit", "continue").</summary>
    public required string Action { get; init; }

    /// <summary>Optional authored condition expression (stripped at projection; carried for agent tooling).</summary>
    public string? Condition { get; init; }

    /// <summary>Optional role that must be present for this transition to be valid.</summary>
    public string? RequiresRole { get; init; }
}
