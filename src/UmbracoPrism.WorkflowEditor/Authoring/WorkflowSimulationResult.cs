namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Result of simulating a path through an authored workflow.
/// </summary>
public record WorkflowSimulationResult
{
    public required string InitialStageKey { get; init; }

    public required string CurrentStageKey { get; init; }

    public required string StopReason { get; init; }

    public bool Completed { get; init; }

    public IReadOnlyList<WorkflowSimulationStep> Steps { get; init; } = [];

    public IReadOnlyList<WorkflowSimulationTransitionOption> AvailableTransitions { get; init; } = [];
}

/// <summary>
/// One executed hop in a simulated path.
/// </summary>
public record WorkflowSimulationStep
{
    public required string FromStageKey { get; init; }

    public required string Action { get; init; }

    public required string ToStageKey { get; init; }

    public string? Condition { get; init; }

    public string? RequiresRole { get; init; }
}

/// <summary>
/// A transition the simulation could take next from the current stage.
/// </summary>
public record WorkflowSimulationTransitionOption
{
    public required string Action { get; init; }

    public required string ToStageKey { get; init; }

    public string? Condition { get; init; }

    public string? RequiresRole { get; init; }
}
