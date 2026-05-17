namespace UmbracoPrism.WorkflowRuntime.Models;

/// <summary>
/// Runtime state for a workflow instance held in-memory by the host application.
/// </summary>
public record WorkflowInstanceState
{
    public string InstanceId { get; init; } = "";

    public string WorkflowKey { get; init; } = "";

    public string TenantId { get; init; } = "";

    public string UserId { get; init; } = "";

    public string CurrentState { get; init; } = "";

    public int StateVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public Dictionary<string, object?> FieldValues { get; init; } = new();
}
