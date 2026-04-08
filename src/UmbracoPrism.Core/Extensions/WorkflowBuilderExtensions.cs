using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.Workflow;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Extension methods for registering the Prism Workflow Engine services.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Registers the Prism Workflow Engine services with the DI container.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <returns>The Umbraco builder for method chaining.</returns>
    public static IUmbracoBuilder AddPrismWorkflowEngine(this IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        builder.Services.AddScoped<IWorkflowInstanceService, WorkflowInstanceService>();
        builder.Services.AddScoped<IWorkflowRenderService, WorkflowRenderService>();
        builder.Services.AddScoped<IWorkflowTenantGuard, WorkflowTenantGuard>();
        
        // Seed service for workflow definitions
        builder.Services.AddSingleton<IWorkflowSeedService, WorkflowSeedServiceImpl>();
        builder.Services.AddHostedService<WorkflowSeedService>();

        return builder;
    }
}
