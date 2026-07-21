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
    /// <summary>
    /// Registers the MCP server and discovers <see cref="WorkflowAuthoringTools"/> and
    /// <see cref="WorkflowAuthoringResources"/> from this assembly.
    /// </summary>
    /// <param name="instructions">
    /// Optional host-specific guidance surfaced to any connecting MCP client at <c>initialize</c>
    /// time (the MCP spec's <c>ServerInstructions</c> field) — the place for facts specific to
    /// *this* host's hosting of the generic toolkit (e.g. "this host only ever has one queue"),
    /// which the generic toolkit itself must not hardcode. Leave null for a host with nothing
    /// host-specific to add.
    /// </param>
    public static IServiceCollection AddPrismWorkflowAuthoringMcp(this IServiceCollection services, string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMcpServer(options =>
            {
                if (instructions is not null) options.ServerInstructions = instructions;
            })
            .WithHttpTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly();

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
