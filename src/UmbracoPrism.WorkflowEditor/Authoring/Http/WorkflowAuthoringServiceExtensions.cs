using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.WorkflowEditor.Extensions;

namespace UmbracoPrism.WorkflowEditor.Authoring.Http;

/// <summary>
/// DI registration extension for the workflow authoring services.
/// </summary>
public static class WorkflowAuthoringServiceExtensions
{
    /// <summary>
    /// Registers the workflow authoring services and default filesystem-backed persistence.
    /// Hosts can override the persistence services before calling this method.
    /// </summary>
    public static IServiceCollection AddWorkflowAuthoring(
        this IServiceCollection services,
        string authoredWorkflowBasePath,
        string? publishedWorkflowBasePath = null) =>
        services.AddPrismWorkflowEditor(authoredWorkflowBasePath, publishedWorkflowBasePath);
}
