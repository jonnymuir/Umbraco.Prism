using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace UmbracoPrism.WorkflowRuntime.Mcp;

/// <summary>
/// Registers and maps the Prism workflow authoring toolkit as MCP tools over HTTP. Hosted
/// in-process alongside the host's own <c>MapPrismWorkflowAuthoringApi()</c>, so tool calls
/// reach the same live <c>WorkflowAuthoringService</c> — no separate process, no stdio.
/// </summary>
public static class PrismWorkflowAuthoringMcpExtensions
{
    /// <summary>Registers the MCP server and discovers <see cref="WorkflowAuthoringTools"/> from this assembly.</summary>
    public static IServiceCollection AddPrismWorkflowAuthoringMcp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        return services;
    }

    /// <summary>
    /// Maps the MCP-over-HTTP endpoint. Returns the endpoint conventions so the host can
    /// chain its own policy, e.g. <c>.RequireAuthorization()</c>; this applies none.
    /// </summary>
    public static IEndpointConventionBuilder MapPrismWorkflowAuthoringMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/prism/workflow-authoring/mcp") =>
        endpoints.MapMcp(pattern);
}
