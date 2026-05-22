using Microsoft.AspNetCore.Routing;
using UmbracoPrism.WorkflowEditor.Extensions;

namespace UmbracoPrism.WorkflowEditor.Authoring.Http;

/// <summary>
/// Back-compat endpoint mapping alias for the extracted workflow editor package.
/// </summary>
public static class WorkflowAuthoringEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowAuthoringEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPrismWorkflowEditor();
}
