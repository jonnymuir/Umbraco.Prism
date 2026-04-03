namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request body for the genre subscribe/unsubscribe endpoints.
/// </summary>
public class PrismSubscribeRequest
{
    /// <summary>Gets or sets the notification genre identifier (e.g. "news", "alerts").</summary>
    public string Genre { get; set; } = string.Empty;
}
