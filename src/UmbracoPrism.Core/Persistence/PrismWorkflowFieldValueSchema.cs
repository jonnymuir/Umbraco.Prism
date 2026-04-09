using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismFieldGroupSubmissions table.
/// </summary>
[TableName("prismFieldGroupSubmissions")]
[PrimaryKey("SubmissionId", AutoIncrement = false)]
[ExplicitColumns]
public class PrismWorkflowFieldGroupSubmissionSchema
{
    /// <summary>
    /// Gets or sets the unique submission identifier (GUID).
    /// </summary>
    [Column("SubmissionId")]
    [Length(64)]
    public string SubmissionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this submission belongs to.
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
    /// Gets or sets the field group key this submission corresponds to.
    /// </summary>
    [Column("FieldGroupKey")]
    [Length(255)]
    public string FieldGroupKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field group version pinned at submission time.
    /// </summary>
    [Column("FieldGroupVersion")]
    [Length(50)]
    public string FieldGroupVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the submitted field values as JSON.
    /// </summary>
    [Column("ValuesJson")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    public string ValuesJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the user ID who submitted this field group.
    /// </summary>
    [Column("SubmittedBy")]
    [Length(450)]
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the submission occurred.
    /// </summary>
    [Column("SubmittedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this is the current/latest submission for the field group.
    /// </summary>
    [Column("IsCurrent")]
    [Constraint(Default = "1")]
    public bool IsCurrent { get; set; } = true;
}
