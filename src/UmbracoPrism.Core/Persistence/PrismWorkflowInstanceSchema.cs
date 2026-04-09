using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowInstances table.
/// </summary>
[TableName("prismWorkflowInstances")]
[PrimaryKey("InstanceId", AutoIncrement = false)]
[ExplicitColumns]
public class PrismWorkflowInstanceSchema
{
    /// <summary>
    /// Gets or sets the unique instance identifier (GUID).
    /// </summary>
    [Column("InstanceId")]
    [Length(64)]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow key this instance is running.
    /// </summary>
    [Column("WorkflowKey")]
    [Length(255)]
    public string WorkflowKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pinned workflow version.
    /// </summary>
    [Column("WorkflowVersion")]
    [Length(50)]
    public string WorkflowVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who initiated the workflow instance.
    /// </summary>
    [Column("UserId")]
    [Length(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state key.
    /// </summary>
    [Column("CurrentStateKey")]
    [Length(255)]
    public string CurrentStateKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state version counter for optimistic concurrency control.
    /// Incremented on every state transition.
    /// </summary>
    [Column("StateVersion")]
    [Constraint(Default = "0")]
    public int StateVersion { get; set; }

    /// <summary>
    /// Gets or sets the instance status: Active, Waiting, Completed, Cancelled, Faulted.
    /// </summary>
    [Column("Status")]
    [Length(50)]

    public string Status { get; set; } = "Active";

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance was created.
    /// </summary>
    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance was last updated.
    /// </summary>
    [Column("UpdatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance reached a terminal state. Null if not complete.
    /// </summary>
    [Column("CompletedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the final outcome key for completed instances (e.g., "approved", "rejected").
    /// </summary>
    [Column("OutcomeKey")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OutcomeKey { get; set; }

    /// <summary>
    /// Gets or sets additional instance metadata as JSON.
    /// </summary>
    [Column("MetadataJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? MetadataJson { get; set; }
}
