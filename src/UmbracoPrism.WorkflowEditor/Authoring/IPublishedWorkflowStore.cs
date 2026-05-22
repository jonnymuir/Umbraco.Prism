using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Persists published runtime workflow definitions.
/// </summary>
public interface IPublishedWorkflowStore
{
    Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default);

    Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default);
}
