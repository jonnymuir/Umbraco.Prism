using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport carries JSON-RPC on stdout — nothing else may write there. All logs go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<IWorkflowSourceStore>(
    _ => new FilesystemWorkflowSourceStore(ResolveSeedsPath(args)));
builder.Services.AddSingleton<WorkflowAuthoringService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string ResolveSeedsPath(string[] args)
{
    if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        return args[0];

    var fromEnv = Environment.GetEnvironmentVariable("PRISM_WORKFLOW_SEEDS_PATH");
    if (!string.IsNullOrWhiteSpace(fromEnv))
        return fromEnv;

    // Reference-app default: MockBusinessApp's demo seed files, resolved relative to this
    // project's own source directory so `dotnet run` works from a repo checkout untouched.
    return Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "UmbracoPrism.MockBusinessApp", "workflow-seeds"));
}
