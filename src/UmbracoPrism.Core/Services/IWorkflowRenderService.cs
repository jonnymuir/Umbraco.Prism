using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for rendering workflow states into UI-ready payloads.
/// </summary>
public interface IWorkflowRenderService
{
    /// <summary>
    /// Renders a workflow instance's current state into a UI payload.
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="definition">The workflow definition.</param>
    /// <returns>A render payload for UI presentation.</returns>
    Task<WorkflowRenderPayload> RenderAsync(WorkflowInstance instance, WorkflowDefinition definition);
}
