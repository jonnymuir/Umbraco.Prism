using Spectre.Console.Cli;
using UmbracoPrism.WorkflowRuntime.Cli.Commands;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("prism-workflow");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List workflow definitions in a seed directory.");
    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate a workflow definition file (gateway routing, calculations).");
    config.AddCommand<SimulateCommand>("simulate")
        .WithDescription("Dry-run a sequence of actions through a workflow definition file.");
});

return await app.RunAsync(args);
