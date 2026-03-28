namespace UmbracoPrism.Core.Services;

/// <summary>
/// Claims extracted from a validated BiometricToken JWT.
/// </summary>
public class BiometricTokenClaims
{
    /// <summary>Unique device identifier for the registered device.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Tenant identifier the token was issued for.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Entra user object ID of the registering user.</summary>
    public string UserOid { get; set; } = string.Empty;

    /// <summary>UTC time the token was issued.</summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>UTC time the token expires.</summary>
    public DateTime ExpiresAt { get; set; }
}
