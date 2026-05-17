using Microsoft.Extensions.DependencyInjection;

namespace UmbracoPrism.WorkflowEditor.Authoring.Http;

/// <summary>
/// DI registration extension for the workflow authoring services.
/// </summary>
public static class WorkflowAuthoringServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IWorkflowProjector"/>, <see cref="IWorkflowPatchService"/>,
    /// <see cref="IWorkflowPreviewService"/>, and a filesystem-backed <see cref="IAuthoredWorkflowStore"/>
    /// pointing at <paramref name="authoredWorkflowBasePath"/>.
    /// </summary>
    public static IServiceCollection AddWorkflowAuthoring(
        this IServiceCollection services,
        string authoredWorkflowBasePath)
    {
        services.AddSingleton<IAuthoredWorkflowStore>(
            _ => new FilesystemAuthoredWorkflowStore(authoredWorkflowBasePath));
        services.AddSingleton<IWorkflowProjector, WorkflowProjector>();
        services.AddSingleton<IWorkflowPatchService, WorkflowPatchService>();
        services.AddSingleton<IWorkflowPreviewService, WorkflowPreviewService>();
        return services;
    }
}
