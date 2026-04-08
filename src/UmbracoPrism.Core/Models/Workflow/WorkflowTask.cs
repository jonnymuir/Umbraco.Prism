namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Represents a queueable work item for reviewer/approver/ops roles.
/// </summary>
public class WorkflowTask
{
    /// <summary>
    /// Gets or sets the unique task identifier.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this task belongs to.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task key/type.
    /// </summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task status.
    /// Valid values: Pending, Complete, Cancelled.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Gets or sets the list of roles assigned to this task.
    /// </summary>
    public List<string> AssignedToRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the UTC timestamp when the task is due. Null if no deadline.
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was completed. Null if not complete.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
