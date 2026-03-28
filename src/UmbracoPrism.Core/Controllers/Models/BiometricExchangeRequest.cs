using System.ComponentModel.DataAnnotations;

namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request body for POST /umbraco/prism/mobile/biometric/exchange.
/// The biometric token JWT is the sole credential — no cookie is required.
/// </summary>
public class BiometricExchangeRequest
{
    /// <summary>
    /// The signed BiometricToken JWT previously issued during device registration.
    /// </summary>
    [Required]
    public string BiometricToken { get; set; } = string.Empty;
}
