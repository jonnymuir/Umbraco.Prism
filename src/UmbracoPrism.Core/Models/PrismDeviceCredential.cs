namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a registered biometric device credential in the Prism device registry.
/// </summary>
public class PrismDeviceCredential
{
    /// <summary>
    /// Gets or sets the unique identifier for the device credential record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the client-generated device UUID, stored as a string.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier this credential is scoped to.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Entra Object ID of the user who registered this credential.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-provided friendly name for the device. May be null.
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the BiometricToken JWT used for exchange validation.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of consecutive failed exchange attempts for rate-limiting.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime until which exchange attempts are locked out. Null means not locked.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential was registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential was last successfully used. Null if never used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential expires. Default is 30 days from registration.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC datetime when this credential was revoked. Null means active.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the device platform. Expected values: 'ios' or 'android'. May be null.
    /// </summary>
    public string? Platform { get; set; }
}
