using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowEvents table.
/// Append-only audit log; no updates allowed.
/// </summary>
[TableName("prismWorkflowEvents")]
[PrimaryKey("EventId", AutoIncrement = false)]
[ExplicitColumns]
public class PrismWorkflowEventSchema
{
    /// <summary>
    /// Gets or sets the unique event identifier (GUID).
    /// </summary>
    [Column("EventId")]
    [Length(64)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this event belongs to.
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
    /// Gets or sets the event type.
    /// Valid values: InstanceCreated, StateTransition, FieldGroupSubmitted, TaskCreated, TaskCompleted, ActionTriggered, Error.
    /// </summary>
    [Column("EventType")]
    [Length(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who triggered the event.
    /// </summary>
    [Column("ActorId")]
    [Length(450)]
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source state key (for transitions). Null if not applicable.
    /// </summary>
    [Column("StateFrom")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? StateFrom { get; set; }

    /// <summary>
    /// Gets or sets the destination state key (for transitions). Null if not applicable.
    /// </summary>
    [Column("StateTo")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? StateTo { get; set; }

    /// <summary>
    /// Gets or sets the event payload as JSON (submitted values, action metadata, error details).
    /// </summary>
    [Column("PayloadJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the UTC timestamp when the event occurred.
    /// </summary>
    [Column("TimestampUtc")]
    [Constraint(Default = "getutcdate()")]
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    [Column("CorrelationId")]
    [Length(64)]
    public string CorrelationId { get; set; } = string.Empty;
}
