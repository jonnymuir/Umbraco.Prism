using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.MockBusinessApp.Services.Actions;

public static class ActionServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessAppActions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWorkflowActionHandler, FormsLoadWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, FormsSaveWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, FormsSubmitWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, CaseAssignWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, CaseEnqueueWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, CaseSetStatusWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, CaseAddNoteWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, NotificationsSendEmailWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionHandler, NotificationsSendSmsWorkflowActionHandler>();
        services.AddSingleton<IWorkflowActionRegistry, ActionRegistry>();
        services.AddSingleton<IActionCatalogSource>(sp => (IActionCatalogSource)sp.GetRequiredService<IWorkflowActionRegistry>());

        return services;
    }
}
