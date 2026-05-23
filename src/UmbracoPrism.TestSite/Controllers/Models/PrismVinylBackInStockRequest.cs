namespace UmbracoPrism.TestSite.Controllers.Models;

/// <summary>
/// Request model for back-in-stock vinyl notifications.
/// Tenant is determined from the authenticated user's session context, not from request data.
/// </summary>
public class PrismVinylBackInStockRequest
{
    /// <summary>Title of the vinyl that is back in stock (required).</summary>
    public string? VinylTitle { get; set; }

    /// <summary>Optional genre — if provided, notification is sent to genre subscribers only.</summary>
    public string? Genre { get; set; }
}
