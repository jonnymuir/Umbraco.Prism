namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Loads <see cref="AuthoredWorkflow"/> documents from a backing store.
/// V1 ships a filesystem implementation (<see cref="FilesystemAuthoredWorkflowStore"/>);
/// this interface is the extension point for multi-tenant or database-backed stores.
/// </summary>
public interface IAuthoredWorkflowStore
{
    /// <summary>
    /// Returns the authored workflow entries known to the store.
    /// <see cref="AuthoredWorkflowStoreEntry.WorkflowKey"/> is the host-facing lookup key used by list and load routes.
    /// </summary>
    Task<IReadOnlyList<AuthoredWorkflowStoreEntry>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads the authored workflow for the given workflow key, or null if not found.
    /// </summary>
    Task<AuthoredWorkflow?> LoadAsync(string workflowKey, CancellationToken ct = default);

    /// <summary>
    /// Returns the workflow keys of all authored workflows in the store.
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists <paramref name="workflow"/> to the store under <paramref name="workflowKey"/>,
    /// overwriting any existing document for the same lookup key.
    /// Returns the path where the file was written (store-implementation-specific).
    /// </summary>
    Task<string> SaveAsync(string workflowKey, AuthoredWorkflow workflow, CancellationToken ct = default);

    /// <summary>
    /// Persists <paramref name="workflow"/> using its <see cref="AuthoredWorkflow.DefinitionKey"/>
    /// as the lookup key.
    /// </summary>
    Task<string> SaveAsync(AuthoredWorkflow workflow, CancellationToken ct = default);
}
