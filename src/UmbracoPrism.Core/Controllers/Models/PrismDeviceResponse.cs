namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// A biometric device credential as the backoffice lists it. The token hash and the encrypted
/// refresh token are never exposed.
/// </summary>
public class PrismDeviceResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }

    /// <summary>The identity provider's stable user identifier, not an email address.</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
