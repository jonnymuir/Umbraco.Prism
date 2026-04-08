using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Repository for workflow definition persistence operations.
/// </summary>
public interface IWorkflowDefinitionRepository
{
    /// <summary>
    /// Gets a workflow definition by key for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="definitionKey">The workflow definition key.</param>
    /// <returns>The workflow definition, or null if not found.</returns>
    Task<WorkflowDefinition?> GetByKeyAsync(string tenantId, string definitionKey);

    /// <summary>
    /// Gets all workflow definitions for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>A read-only list of workflow definitions.</returns>
    Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(string tenantId);

    /// <summary>
    /// Inserts or updates a workflow definition.
    /// </summary>
    /// <param name="definition">The workflow definition to persist.</param>
    Task UpsertAsync(WorkflowDefinition definition);
}
