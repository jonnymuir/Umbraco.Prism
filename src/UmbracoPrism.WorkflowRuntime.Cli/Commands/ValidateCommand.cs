using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using UmbracoPrism.WorkflowRuntime.Services;
using UmbracoPrism.WorkflowRuntime.Stores;

namespace UmbracoPrism.WorkflowRuntime.Cli.Commands;

public sealed class ValidateCommand : Command<ValidateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("Path to a workflow definition JSON file.")]
        public required string File { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var workflow = WorkflowFileReader.Read(settings.File);
        var service = new WorkflowAuthoringService(new FilesystemWorkflowSourceStore(
            System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(settings.File))!));

        var outcome = service.Validate(workflow);

        if (outcome.IsValid)
        {
            AnsiConsole.MarkupLine($"[green]Valid:[/] {workflow.DefinitionKey}");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]Invalid:[/] {workflow.DefinitionKey}");
        foreach (var error in outcome.Errors)
        {
            AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(error)}");
        }

        return 1;
    }
}
