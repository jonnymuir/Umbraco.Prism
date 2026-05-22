namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// Previews and publishes authored workflows into runtime-ready definitions.
/// </summary>
public interface IWorkflowPublishService
{
    Task<PublishPreviewResult> PreviewAsync(AuthoredWorkflow workflow, CancellationToken ct = default);

    Task<PublishResult> PublishAsync(AuthoredWorkflow workflow, CancellationToken ct = default);
}
