using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowRuntime.Abstractions;

/// <summary>
/// Persists authored/runtime workflow definitions, keyed by <see cref="WorkflowDefinitionFile.DefinitionKey"/>.
/// This is the toolkit's extension point for AI-driven and human authoring alike — host apps
/// implement it against their own persistence (filesystem, database, CMS, ...).
/// </summary>
public interface IWorkflowSourceStore
{
    Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default);

    Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default);

    Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default);
}

/// <summary>Lightweight listing entry for discovering available workflows before loading one in full.</summary>
public sealed record WorkflowSourceSummary(string DefinitionKey, string DisplayName);
