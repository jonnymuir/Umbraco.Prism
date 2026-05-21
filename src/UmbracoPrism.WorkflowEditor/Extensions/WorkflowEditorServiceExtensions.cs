using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <see cref="IWorkflowPreviewService"/>, and default filesystem-backed persistence services
    /// pointing at <paramref name="authoredWorkflowBasePath"/>.
    /// Hosts can pre-register custom persistence implementations before calling this method.
    /// </summary>
    public static IServiceCollection AddPrismWorkflowEditor(
        this IServiceCollection services,
        string authoredWorkflowBasePath,
        string? publishedWorkflowBasePath = null)
    {
        publishedWorkflowBasePath ??= GetDefaultPublishedWorkflowBasePath(authoredWorkflowBasePath);

        services.TryAddSingleton<IAuthoredWorkflowStore>(
            _ => new FilesystemAuthoredWorkflowStore(authoredWorkflowBasePath));
        services.TryAddSingleton<IPublishedWorkflowStore>(
            _ => new FilesystemPublishedWorkflowStore(publishedWorkflowBasePath));
        services.TryAddSingleton<IWorkflowAuthoringProvenanceStore>(
            _ => new FilesystemWorkflowAuthoringProvenanceStore(
                Path.Combine(Path.GetFullPath(authoredWorkflowBasePath), ".provenance")));
        services.AddSingleton<IParameterWidgetMapper, DefaultParameterWidgetMapper>();
        services.AddSingleton<BuiltInActionCatalogProvider>();
        services.AddSingleton<IActionCatalogProvider>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IActionCatalogSource>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IWorkflowProjector, WorkflowProjector>();
        services.AddSingleton<IWorkflowPatchService, WorkflowPatchService>();
        services.AddSingleton<IWorkflowPreviewService, WorkflowPreviewService>();
        services.AddSingleton<IWorkflowPublishService, WorkflowPublishService>();
        services.AddSingleton<IWorkflowSimulationService, WorkflowSimulationService>();
        return services;
    }

    private static string GetDefaultPublishedWorkflowBasePath(string authoredWorkflowBasePath)
    {
        var authoredRoot = Path.GetFullPath(authoredWorkflowBasePath);
        var parent = Directory.GetParent(authoredRoot)?.FullName ?? authoredRoot;
        return Path.Combine(parent, "workflow-seeds");
    }
}
