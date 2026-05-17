namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Produces a preview of applying a <see cref="ProposalEnvelope"/> to an <see cref="AuthoredWorkflow"/>.
/// No persistence, no side effects.
/// </summary>
public interface IWorkflowPreviewService
{
    /// <summary>
    /// Projects <paramref name="patched"/> and computes a semantic diff against <paramref name="original"/>.
    /// Also produces a deterministic happy-path journey trace through <paramref name="patched"/>.
    /// </summary>
    PreviewResult Preview(AuthoredWorkflow original, AuthoredWorkflow patched);
}
