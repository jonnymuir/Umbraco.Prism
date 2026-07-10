using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Cli.Commands;

public sealed class SimulateCommand : Command<SimulateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to a workflow definition JSON file.")]
        public required string File { get; init; }

        [CommandOption("--actions")]
        [Description("Comma-separated actions to advance through in order, e.g. continue,continue,submit.")]
        public string Actions { get; init; } = "";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var workflow = WorkflowFileReader.Read(settings.File);
        var service = new WorkflowAuthoringService(new FilesystemWorkflowSourceStore(
            System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(settings.File))!));

        var steps = settings.Actions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(action => new WorkflowRuntimeSimulationStep(action))
            .ToArray();

        var trace = service.Simulate(workflow, steps);

        var table = new Table().AddColumn("Step").AddColumn("Response State").AddColumn("Stage");
        for (var i = 0; i < trace.Count; i++)
        {
            var envelope = trace[i];
            table.AddRow(
                i.ToString(),
                envelope.ResponseState,
                envelope.Render?.StateDisplayName ?? "");
        }

        AnsiConsole.Write(table);

        var lastProblems = trace[^1].Problems;
        if (lastProblems.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]Problems on final step:[/]");
            foreach (var problem in lastProblems)
            {
                AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(problem.Message)}");
            }
            return 1;
        }

        return 0;
    }
}
