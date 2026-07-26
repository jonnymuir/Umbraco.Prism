namespace UmbracoPrism.Shared.Models.ServiceDesign;

public class ServiceRequestListEnvelope
{
    public IReadOnlyList<ServiceRequestSummary> Instances { get; init; } = Array.Empty<ServiceRequestSummary>();
}

public class ServiceRequestSummary
{
    public string InstanceId { get; init; } = string.Empty;
    public string BlueprintKey { get; init; } = string.Empty;
    public string BlueprintDisplayName { get; init; } = string.Empty;
    public string CurrentStateKey { get; init; } = string.Empty;
    public string CurrentStateDisplayName { get; init; } = string.Empty;
    /// <summary>Step type for this instance (question, check-answers, confirmation, status-timeline, task-list).</summary>
    public string StepType { get; init; } = "question";
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public bool CanContinue { get; init; }
    public bool IsCompleted { get; init; }
    public string? ServiceRequestPageUrl { get; init; }
    public string RequestPolicy { get; init; } = "single";
}
