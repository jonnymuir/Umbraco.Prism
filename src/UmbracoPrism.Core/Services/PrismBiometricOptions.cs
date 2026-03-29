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

    /// <summary>
    /// Base64-encoded 32-byte AES-256 key used to encrypt Entra refresh tokens at rest.
    /// Generate with: <c>Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))</c>.
    /// In production, inject via environment variable or Azure Key Vault reference.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Maximum consecutive failed exchange attempts per token hash before lockout (default: 3).</summary>
    public int MaxFailedAttempts { get; set; } = 3;

    /// <summary>Sliding window in minutes for counting failed attempts (default: 10).</summary>
    public int FailureWindowMinutes { get; set; } = 10;

    /// <summary>Maximum exchange requests per IP address per minute (default: 20).</summary>
    public int PerIpRequestsPerMinute { get; set; } = 20;
}
