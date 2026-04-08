namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Represents an append-only audit event in the workflow timeline.
/// Events are never updated or deleted.
/// </summary>
public class WorkflowEvent
{
    /// <summary>
    /// Gets or sets the unique event identifier.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this event belongs to.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event type.
    /// Valid values: Created, Advanced, Cancelled, Error, TaskCreated, TaskCompleted.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who triggered the event.
    /// </summary>
    public string ActorUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source state key (for transitions). Null if not applicable.
    /// </summary>
    public string? FromState { get; set; }

    /// <summary>
    /// Gets or sets the destination state key (for transitions). Null if not applicable.
    /// </summary>
    public string? ToState { get; set; }

    /// <summary>
    /// Gets or sets the event payload as JSON (nullable).
    /// </summary>
    public string? EventJson { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; set; }
}
