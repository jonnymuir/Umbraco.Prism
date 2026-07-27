namespace UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;

/// <summary>
/// Supplies action-catalog payloads for host-facing discovery endpoints.
/// </summary>
public interface IActionCatalogSource
{
    IReadOnlyList<ActionCatalogEntry> GetCatalog();
}
