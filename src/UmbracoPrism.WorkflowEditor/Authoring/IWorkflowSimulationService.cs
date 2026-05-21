namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Provides deterministic, editor-side workflow path simulation over the authored model.
/// </summary>
public interface IWorkflowSimulationService
{
    WorkflowSimulationResult Simulate(
        AuthoredWorkflow workflow,
        IReadOnlyList<string>? actions = null,
        int? maxSteps = null);
}
