namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Represents a runtime workflow instance tracking current state and execution metadata.
/// </summary>
public class WorkflowInstance
{
    /// <summary>
    /// Gets or sets the unique instance identifier.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database primary key of the workflow definition.
    /// </summary>
    public int DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the workflow definition key this instance is running.
    /// </summary>
    public string DefinitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state key.
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state version counter for optimistic concurrency control.
    /// Incremented on every state transition.
    /// </summary>
    public int StateVersion { get; set; }

    /// <summary>
    /// Gets or sets the instance status.
    /// Valid values: Active, Complete, Cancelled, Error.
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Gets or sets the correlation identifier for tracking related workflow instances.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the user ID who initiated the workflow instance.
    /// </summary>
    public string InitiatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state payload as JSON (nullable).
    /// </summary>
    public string? StateJson { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
