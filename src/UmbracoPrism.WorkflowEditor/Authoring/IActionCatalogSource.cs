namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Supplies action-catalog payloads for host-facing discovery endpoints.
/// </summary>
public interface IActionCatalogSource
{
    IReadOnlyList<ActionCatalogEntry> GetCatalog();
}
