using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Extensions;

public static class WorkflowRuntimeServiceExtensions
{
    public static IServiceCollection AddPrismWorkflowRuntime(
        this IServiceCollection services,
        string workflowSeedPath) =>
        services.AddPrismWorkflowRuntime<WorkflowRuntimeEngine>(workflowSeedPath);

    public static IServiceCollection AddPrismWorkflowRuntime<TEngine>(
        this IServiceCollection services,
        string workflowSeedPath)
        where TEngine : class, IWorkflowRuntimeEngine
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowSeedPath);

        services.AddSingleton<IWorkflowDefinitionStore>(
            _ => new FilesystemWorkflowDefinitionStore(workflowSeedPath));
        services.AddSingleton<TEngine>();
        services.AddSingleton<IWorkflowRuntimeEngine>(sp => sp.GetRequiredService<TEngine>());

        return services;
    }
}
