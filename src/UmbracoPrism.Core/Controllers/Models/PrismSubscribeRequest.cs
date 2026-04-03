using System.ComponentModel.DataAnnotations;

namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request body for the genre subscribe/unsubscribe endpoints.
/// </summary>
public class PrismSubscribeRequest
{
    /// <summary>Gets or sets the notification genre identifier (e.g. "news", "alerts").</summary>
    [Required(ErrorMessage = "genre is required.")]
    [MaxLength(50, ErrorMessage = "genre must not exceed 50 characters.")]
    [RegularExpression("^[a-z0-9_-]+$", ErrorMessage = "genre must contain only lowercase letters, numbers, hyphens, and underscores.")]
    public string Genre { get; set; } = string.Empty;
}
