using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.WorkflowEditor.Extensions;

/// <summary>
/// DI registration extension for Prism Workflow Editor services.
/// </summary>
public static class WorkflowEditorServiceExtensions
{
    /// <summary>
    /// Registers the Prism Workflow Editor backend services:
    /// <see cref="IWorkflowProjector"/>, <see cref="IWorkflowPatchService"/>,
    /// <see cref="IWorkflowPreviewService"/>, and a filesystem-backed <see cref="IAuthoredWorkflowStore"/>
    /// pointing at <paramref name="authoredWorkflowBasePath"/>.
    /// </summary>
    public static IServiceCollection AddPrismWorkflowEditor(
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
