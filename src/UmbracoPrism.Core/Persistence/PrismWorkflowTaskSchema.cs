using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowTasks table.
/// </summary>
[TableName("prismWorkflowTasks")]
[PrimaryKey("TaskId", AutoIncrement = false)]
[ExplicitColumns]
public class PrismWorkflowTaskSchema
{
    /// <summary>
    /// Gets or sets the unique task identifier (GUID).
    /// </summary>
    [Column("TaskId")]
    [Length(64)]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this task belongs to.
    /// </summary>
    [Column("InstanceId")]
    [Length(64)]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task type/key (e.g., "review", "approve", "assign").
    /// </summary>
    [Column("TaskType")]
    [Length(255)]
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role or user ID assigned to this task.
    /// Role-based: "Approver", "Reviewer"; User-based: specific user GUID.
    /// </summary>
    [Column("AssignedTo")]
    [Length(450)]
    public string AssignedTo { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the assignment is role-based (true) or user-based (false).
    /// </summary>
    [Column("IsRoleAssignment")]
    [Constraint(Default = "1")]
    public bool IsRoleAssignment { get; set; } = true;

    /// <summary>
    /// Gets or sets the task status: Pending, InProgress, Completed, Cancelled.
    /// </summary>
    [Column("Status")]
    [Length(50)]

    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was created.
    /// </summary>
    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task is due. Null if no deadline.
    /// </summary>
    [Column("DueAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was started. Null if not started.
    /// </summary>
    [Column("StartedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was completed. Null if not complete.
    /// </summary>
    [Column("CompletedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID who completed the task.
    /// </summary>
    [Column("CompletedBy")]
    [Length(450)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? CompletedBy { get; set; }

    /// <summary>
    /// Gets or sets the task outcome (e.g., "approved", "rejected", "changes-requested").
    /// </summary>
    [Column("OutcomeKey")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OutcomeKey { get; set; }

    /// <summary>
    /// Gets or sets additional task metadata as JSON.
    /// </summary>
    [Column("MetadataJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? MetadataJson { get; set; }
}
