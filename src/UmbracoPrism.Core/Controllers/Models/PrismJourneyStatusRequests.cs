using System.ComponentModel.DataAnnotations;

namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>An app that shows journey statuses, registering the device it runs on.</summary>
public class PrismJourneyStatusDeviceRequest
{
    /// <summary>Gets or sets <c>ios</c> or <c>android</c>.</summary>
    [Required]
    [RegularExpression("^(ios|android)$", ErrorMessage = "platform must be ios or android.")]
    public string Platform { get; set; } = string.Empty;

    /// <summary>Gets or sets the iOS token that lets the site start a Live Activity on this device.</summary>
    [MaxLength(500)]
    public string? StartToken { get; set; }
}

/// <summary>A running Live Activity's own token, from the iPhone running it.</summary>
public class PrismJourneyStatusActivityRequest
{
    /// <summary>Gets or sets the journey the activity shows.</summary>
    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the activity's push token.</summary>
    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;
}
