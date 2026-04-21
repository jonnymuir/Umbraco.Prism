namespace UmbracoPrism.Shared.Models.Workflow;

public class WorkflowInstanceListEnvelope
{
    public IReadOnlyList<WorkflowInstanceSummary> Instances { get; init; } = Array.Empty<WorkflowInstanceSummary>();
}

public class WorkflowInstanceSummary
{
    public string InstanceId { get; init; } = string.Empty;
    public string WorkflowKey { get; init; } = string.Empty;
    public string WorkflowDisplayName { get; init; } = string.Empty;
    public string CurrentStateKey { get; init; } = string.Empty;
    public string CurrentStateDisplayName { get; init; } = string.Empty;
    /// <summary>Step type for this instance (question, check-answers, confirmation, status-timeline, task-list).</summary>
    public string StepType { get; init; } = "question";
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public bool CanContinue { get; init; }
    public bool IsCompleted { get; init; }
    public string? WorkflowPageUrl { get; init; }
    public string InstancePolicy { get; init; } = "single";
}
