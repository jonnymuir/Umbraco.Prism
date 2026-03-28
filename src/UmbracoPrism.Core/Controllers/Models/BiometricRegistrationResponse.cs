namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Response body for POST /umbraco/prism/mobile/biometric/register.
/// </summary>
public class BiometricRegistrationResponse
{
    /// <summary>
    /// The signed BiometricToken JWT to be stored on the device.
    /// </summary>
    public string BiometricToken { get; set; } = string.Empty;

    /// <summary>
    /// UTC datetime when the biometric token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
