using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismDeviceCredentials table.
/// </summary>
[TableName("prismDeviceCredentials")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismDeviceCredentialSchema
{
    /// <summary>
    /// Gets or sets the unique identifier for the device credential record.
    /// </summary>
    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the client-generated device UUID, stored as a string.
    /// </summary>
    [Column("DeviceId")]
    [Length(64)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier this credential is scoped to.
    /// </summary>
    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Entra Object ID of the user who registered this credential.
    /// </summary>
    [Column("UserId")]
    [Length(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-provided friendly name for the device. May be null.
    /// </summary>
    [Column("DeviceName")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? DeviceName { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the BiometricToken JWT used for exchange validation.
    /// </summary>
    [Column("TokenHash")]
    [Length(512)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of consecutive failed exchange attempts for rate-limiting.
    /// </summary>
    [Column("FailedAttempts")]
    [Constraint(Default = "0")]
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime until which exchange attempts are locked out. Null means not locked.
    /// </summary>
    [Column("LockedUntil")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential was registered.
    /// </summary>
    [Column("RegisteredAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential was last successfully used.
    /// </summary>
    [Column("LastUsedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential expires.
    /// </summary>
    [Column("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential was revoked. Null means active.
    /// </summary>
    [Column("RevokedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the device platform ('ios' or 'android'). May be null.
    /// </summary>
    [Column("Platform")]
    [Length(50)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Platform { get; set; }
}
