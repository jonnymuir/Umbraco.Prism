using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.ServiceBlueprintEditor.Extensions;

/// <summary>
/// DI registration for Prism Blueprint Editor backend services.
/// </summary>
public static class ServiceBlueprintEditorServiceExtensions
{
    /// <summary>
    /// Registers the projector + patch + simulation + action-catalog services
    /// the editor library needs. Authored-workflow persistence and HTTP
    /// endpoints are <em>not</em> the editor's responsibility — hosts implement
    /// <c>ServiceBlueprintSource</c> in TypeScript and expose whatever transport they
    /// like.
    /// </summary>
    public static IServiceCollection AddPrismServiceBlueprintEditor(this IServiceCollection services)
    {
        services.AddSingleton<IParameterWidgetMapper, DefaultParameterWidgetMapper>();
        services.AddSingleton<BuiltInActionCatalogProvider>();
        services.AddSingleton<IActionCatalogProvider>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IActionCatalogSource>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IServiceBlueprintProjector, ServiceBlueprintProjector>();
        services.AddSingleton<IServiceBlueprintPatchService, ServiceBlueprintPatchService>();
        services.AddSingleton<IServiceBlueprintSimulationService, ServiceBlueprintSimulationService>();
        return services;
    }
}
