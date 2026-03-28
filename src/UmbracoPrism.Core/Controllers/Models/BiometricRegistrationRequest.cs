using System.ComponentModel.DataAnnotations;

namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request body for POST /umbraco/prism/mobile/biometric/register.
/// </summary>
public class BiometricRegistrationRequest
{
    /// <summary>
    /// Client-generated UUID identifying this device.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Optional friendly name for the device (e.g. "iPhone 15 Pro").
    /// </summary>
    [StringLength(255)]
    public string? DeviceName { get; set; }

    /// <summary>
    /// Optional device platform identifier ('ios' or 'android').
    /// </summary>
    [StringLength(50)]
    public string? Platform { get; set; }
}
