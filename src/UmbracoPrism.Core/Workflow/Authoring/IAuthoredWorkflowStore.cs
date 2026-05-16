namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// Loads <see cref="AuthoredWorkflow"/> documents from a backing store.
/// V1 ships a filesystem implementation (<see cref="FilesystemAuthoredWorkflowStore"/>);
/// this interface is the extension point for multi-tenant or database-backed stores.
/// </summary>
public interface IAuthoredWorkflowStore
{
    /// <summary>
    /// Loads the authored workflow for the given definition key, or null if not found.
    /// </summary>
    Task<AuthoredWorkflow?> LoadAsync(string definitionKey, CancellationToken ct = default);

    /// <summary>
    /// Returns the definition keys of all authored workflows in the store.
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default);
}
