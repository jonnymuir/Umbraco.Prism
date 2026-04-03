using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismNotificationSubscriptions table.
/// Each row represents a user's subscription to a notification genre within a tenant.
/// </summary>
[TableName("prismNotificationSubscriptions")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismNotificationSubscriptionSchema
{
    /// <summary>Gets or sets the unique identifier for the subscription record.</summary>
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Gets or sets the Entra Object ID of the subscribing user.</summary>
    [Column("UserId")]
    [Length(255)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant this subscription belongs to.</summary>
    [Column("TenantId")]
    [Length(255)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the notification genre (e.g. "news", "alerts").</summary>
    [Column("Genre")]
    [Length(100)]
    public string Genre { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC datetime when this subscription was created.</summary>
    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }
}
