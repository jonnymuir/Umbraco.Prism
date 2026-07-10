using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Cli.Commands;

public sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Directory containing workflow definition JSON files. Defaults to the current directory.")]
        public string? Path { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var path = settings.Path ?? Directory.GetCurrentDirectory();
        var service = new WorkflowAuthoringService(new FilesystemWorkflowSourceStore(path));
        var summaries = await service.ListAsync();

        if (summaries.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No workflow definitions found in[/] {path}");
            return 0;
        }

        var table = new Table().AddColumn("Definition Key").AddColumn("Display Name");
        foreach (var summary in summaries)
        {
            table.AddRow(summary.DefinitionKey, summary.DisplayName);
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
