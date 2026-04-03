using System.ComponentModel.DataAnnotations;

namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request body for the push token registration endpoint.
/// </summary>
public class PrismPushRegisterRequest
{
    /// <summary>Gets or sets the Firebase Cloud Messaging device push token.</summary>
    [Required(ErrorMessage = "pushToken is required.")]
    [MaxLength(500, ErrorMessage = "pushToken must not exceed 500 characters.")]
    public string PushToken { get; set; } = string.Empty;
}
