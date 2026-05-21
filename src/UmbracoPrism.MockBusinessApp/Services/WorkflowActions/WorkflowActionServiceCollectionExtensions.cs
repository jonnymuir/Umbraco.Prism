using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.MockBusinessApp.Services.WorkflowActions;

public static class WorkflowActionServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessAppWorkflowActions(this IServiceCollection services)
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
        services.AddSingleton<IWorkflowActionRegistry, WorkflowActionRegistry>();
        services.AddSingleton<IActionCatalogSource>(sp => (IActionCatalogSource)sp.GetRequiredService<IWorkflowActionRegistry>());

        return services;
    }
}
