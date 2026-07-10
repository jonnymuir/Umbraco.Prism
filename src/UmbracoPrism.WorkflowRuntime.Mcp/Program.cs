using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.WorkflowRuntime.Mcp;

var apiBaseUrl = ResolveApiBaseUrl(args);
var apiPrefix = Environment.GetEnvironmentVariable("PRISM_WORKFLOW_API_PREFIX") ?? "/prism/workflow-authoring";
var apiToken = Environment.GetEnvironmentVariable("PRISM_WORKFLOW_API_TOKEN");

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport carries JSON-RPC on stdout — nothing else may write there. All logs go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddHttpClient("PrismWorkflowApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    if (!string.IsNullOrWhiteSpace(apiToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
    }
});
builder.Services.AddSingleton(sp =>
    new WorkflowAuthoringApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("PrismWorkflowApi"), apiPrefix));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string ResolveApiBaseUrl(string[] args)
{
    if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        return args[0];

    var fromEnv = Environment.GetEnvironmentVariable("PRISM_WORKFLOW_API_BASE_URL");
    if (!string.IsNullOrWhiteSpace(fromEnv))
        return fromEnv;

    Console.Error.WriteLine(
        "PrismWorkflowRuntime.Mcp: no API base URL supplied. Pass it as the first argument " +
        "or set PRISM_WORKFLOW_API_BASE_URL, pointing at a running Prism-based app that has " +
        "called MapPrismWorkflowAuthoringApi() (e.g. https://localhost:5001). " +
        "See README.md.");
    Environment.Exit(1);
    return string.Empty; // unreachable
}
