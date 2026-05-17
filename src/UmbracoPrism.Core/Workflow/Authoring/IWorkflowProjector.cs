namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// Compiles an <see cref="AuthoredWorkflow"/> into a runtime <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowDefinitionFile"/>.
/// Implementations must be deterministic: identical input always produces byte-identical output.
/// </summary>
public interface IWorkflowProjector
{
    /// <summary>
    /// Projects the authored workflow through the five-stage pipeline:
    /// validate → normalise → emit → checksum.
    /// </summary>
    /// <param name="authored">The authored source workflow.</param>
    /// <returns>A <see cref="ProjectionResult"/> containing the runtime file and diagnostics.</returns>
    ProjectionResult Project(AuthoredWorkflow authored);
}
