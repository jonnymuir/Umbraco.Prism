namespace UmbracoPrism.Core.Controllers.Models;

/// <summary>
/// Request model for back-in-stock vinyl notifications.
/// </summary>
public class PrismVinylBackInStockRequest
{
    /// <summary>Tenant ID to send the notification to (required).</summary>
    public string? TenantId { get; set; }

    /// <summary>Title of the vinyl that is back in stock (required).</summary>
    public string? VinylTitle { get; set; }

    /// <summary>Optional genre — if provided, notification is sent to genre subscribers only.</summary>
    public string? Genre { get; set; }
}
