namespace UmbracoPrism.Core.Services;

/// <summary>
/// Configurable options for BiometricToken issuance and validation.
/// Bind from appsettings.json under "Prism:Biometric".
/// </summary>
public class PrismBiometricOptions
{
    /// <summary>
    /// Configuration section path used to bind biometric options.
    /// </summary>
    public const string SectionName = "Prism:Biometric";

    /// <summary>
    /// HMAC-SHA256 signing key for BiometricToken JWTs.
    /// Must be at least 32 characters. In production, inject via environment variable
    /// or Azure Key Vault reference rather than embedding in appsettings.json.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Number of days before an issued BiometricToken expires (default: 30).</summary>
    public int TokenLifetimeDays { get; set; } = 30;
}
