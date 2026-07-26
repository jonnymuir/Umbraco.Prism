namespace UmbracoPrism.ServiceBlueprintEditor.Authoring;

/// <summary>
/// Supplies blueprint action metadata for editor discovery and authored-workflow validation.
/// </summary>
public interface IActionCatalogProvider
{
    IReadOnlyList<ActionCatalogEntry> GetEntries();

    ActionCatalogEntry? GetEntry(string actionType);
}
