using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.WorkflowEditor.Extensions;

/// <summary>
/// DI registration for Prism Workflow Editor backend services.
/// </summary>
public static class WorkflowEditorServiceExtensions
{
    /// <summary>
    /// Registers the projector + patch + simulation + action-catalog services
    /// the editor library needs. Authored-workflow persistence and HTTP
    /// endpoints are <em>not</em> the editor's responsibility — hosts implement
    /// <c>WorkflowSource</c> in TypeScript and expose whatever transport they
    /// like.
    /// </summary>
    public static IServiceCollection AddPrismWorkflowEditor(this IServiceCollection services)
    {
        services.AddSingleton<IParameterWidgetMapper, DefaultParameterWidgetMapper>();
        services.AddSingleton<BuiltInActionCatalogProvider>();
        services.AddSingleton<IActionCatalogProvider>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IActionCatalogSource>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IWorkflowProjector, WorkflowProjector>();
        services.AddSingleton<IWorkflowPatchService, WorkflowPatchService>();
        services.AddSingleton<IWorkflowSimulationService, WorkflowSimulationService>();
        return services;
    }
}
