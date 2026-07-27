using UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;

namespace UmbracoPrism.MockBusinessApp.Services.Actions;

public sealed class ActionRegistry : IWorkflowActionRegistry, IActionCatalogSource
{
    private readonly IReadOnlyList<ActionCatalogEntry> _catalog;
    private readonly IReadOnlyDictionary<string, IWorkflowActionHandler> _handlersByType;

    public ActionRegistry(
        IActionCatalogProvider catalogProvider,
        IEnumerable<IWorkflowActionHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(catalogProvider);
        ArgumentNullException.ThrowIfNull(handlers);

        _catalog = catalogProvider.GetEntries();
        _handlersByType = handlers.ToDictionary(
            handler => handler.ActionType,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<ActionCatalogEntry> GetCatalog() => _catalog;

    public IWorkflowActionHandler? Resolve(string actionType) =>
        _handlersByType.TryGetValue(actionType, out var handler) ? handler : null;
}
