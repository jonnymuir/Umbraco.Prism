namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Applies a <see cref="ProposalEnvelope"/> to an <see cref="AuthoredWorkflow"/> immutably.
/// All errors surface as <see cref="ProjectionDiagnostic"/> entries — never as exceptions.
/// </summary>
public interface IWorkflowPatchService
{
    /// <summary>
    /// Applies each op in <paramref name="envelope"/> to <paramref name="original"/> in sequence,
    /// producing a new <see cref="AuthoredWorkflow"/>. The original is never mutated.
    /// If any op fails or the output fails projection validation, the original is returned
    /// alongside the diagnostics.
    /// </summary>
    PatchResult Apply(ProposalEnvelope envelope, AuthoredWorkflow original);
}
