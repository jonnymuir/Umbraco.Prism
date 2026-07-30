using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Wayfinder.Umbraco.Persistence;

/// <summary>
/// Database schema for the prismCmsServiceRequest table — durable (survives an app-pool
/// recycle), TTL-bound (expires with the visitor's session) storage for CMS service request
/// state, backing <see cref="Wayfinder.Engine.Abstractions.IServiceRequestStore"/>.
/// </summary>
[TableName("prismCmsServiceRequest")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismCmsServiceRequestSchema
{
    /// <summary>Gets or sets the unique identifier for the request record.</summary>
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Gets or sets the engine-assigned request id — the store's lookup key.</summary>
    [Column("InstanceId")]
    [Length(64)]
    [Index(IndexTypes.UniqueNonClustered)]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the service blueprint key this request is running against.</summary>
    [Column("BlueprintKey")]
    [Length(200)]
    public string BlueprintKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant this request belongs to.</summary>
    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning user id — the authenticated Prism Member's stable claim, or an
    /// anonymous visitor correlation id for unauthenticated journeys.
    /// </summary>
    [Column("UserId")]
    [Length(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the full serialized <c>ServiceRequest</c> JSON.</summary>
    [Column("StateJson")]
    public string StateJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC datetime this row expires. Refreshed on every read/write to a
    /// sliding window; rows past this are treated as not-found and swept up.
    /// </summary>
    [Column("ExpiresUtc")]
    public DateTime ExpiresUtc { get; set; }

    /// <summary>Gets or sets the UTC datetime this row was last saved.</summary>
    [Column("UpdatedUtc")]
    [Constraint(Default = "getutcdate()")]
    public DateTime UpdatedUtc { get; set; }
}
