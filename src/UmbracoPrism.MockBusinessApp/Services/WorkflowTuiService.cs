using Spectre.Console;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Background service that hosts a Spectre.Console REPL for driving the
/// <see cref="BusinessAppWorkflowEngine"/> from the terminal during local development.
/// Replaces the HTTP WorkflowEmulatorController.
/// </summary>
public sealed class WorkflowTuiService(BusinessAppWorkflowEngine engine) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let host startup messages settle before printing the banner.
        await Task.Delay(1500, stoppingToken).ConfigureAwait(false);

        PrintBanner();

        while (!stoppingToken.IsCancellationRequested)
        {
            AnsiConsole.Markup("[grey]> [/]");

            string? line;
            try
            {
                line = Console.ReadLine();
            }
            catch (Exception)
            {
                break;
            }

            if (line is null)
                break;

            line = line.Trim();
            if (line.Length == 0)
                continue;

            try
            {
                await DispatchAsync(line, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            }
        }
    }

    private Task DispatchAsync(string line, CancellationToken ct)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "list":
                HandleList();
                break;

            case "show" when parts.Length >= 2:
                HandleShow(parts[1]);
                break;

            case "show":
                AnsiConsole.MarkupLine("[yellow]Usage:[/] show <instanceId>");
                break;

            case "approve" when parts.Length >= 2:
                HandleReviewerAction(parts[1], "approve");
                break;

            case "approve":
                AnsiConsole.MarkupLine("[yellow]Usage:[/] approve <instanceId>");
                break;

            case "reject" when parts.Length >= 2:
                HandleReviewerAction(parts[1], "reject");
                break;

            case "reject":
                AnsiConsole.MarkupLine("[yellow]Usage:[/] reject <instanceId>");
                break;

            case "reset" when parts.Length >= 2:
                HandleReset(parts[1]);
                break;

            case "reset":
                AnsiConsole.MarkupLine("[yellow]Usage:[/] reset <instanceId>");
                break;

            case "defs":
                HandleDefs();
                break;

            case "help":
                HandleHelp();
                break;

            case "quit":
            case "exit":
                AnsiConsole.MarkupLine("[grey]Shutting down…[/]");
                Environment.Exit(0);
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(command)} — type [bold]help[/] for a list.");
                break;
        }

        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Command handlers
    // -----------------------------------------------------------------------

    private void HandleList()
    {
        var instances = engine.GetAllInstances().ToList();
        if (instances.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey](no workflow instances)[/]");
            return;
        }

        var table = new Table()
            .AddColumn("[bold]Instance ID[/]")
            .AddColumn("[bold]Workflow Key[/]")
            .AddColumn("[bold]State[/]")
            .AddColumn("[bold]Tenant[/]")
            .AddColumn("[bold]User[/]");

        foreach (var i in instances)
        {
            table.AddRow(
                Markup.Escape(i.InstanceId),
                Markup.Escape(i.WorkflowKey),
                $"[cyan]{Markup.Escape(i.CurrentState)}[/]",
                Markup.Escape(i.TenantId),
                Markup.Escape(i.UserId));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{instances.Count} instance(s)[/]");
    }

    private void HandleShow(string instanceId)
    {
        var instance = engine.GetAllInstances().FirstOrDefault(
            i => i.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));

        if (instance is null)
        {
            AnsiConsole.MarkupLine($"[red]Not found:[/] {Markup.Escape(instanceId)}");
            return;
        }

        var table = new Table { ShowHeaders = false }
            .AddColumn("Field")
            .AddColumn("Value");

        table.AddRow("Instance ID", Markup.Escape(instance.InstanceId));
        table.AddRow("Workflow Key", Markup.Escape(instance.WorkflowKey));
        table.AddRow("Current State", $"[cyan]{Markup.Escape(instance.CurrentState)}[/]");
        table.AddRow("Tenant", Markup.Escape(instance.TenantId));
        table.AddRow("User", Markup.Escape(instance.UserId));
        table.AddRow("State Version", instance.StateVersion.ToString());
        table.AddRow("Created", instance.CreatedAt.ToString("u"));
        table.AddRow("Updated", instance.UpdatedAt.ToString("u"));

        if (instance.FieldValues.Count > 0)
        {
            var fields = string.Join(", ", instance.FieldValues.Select(
                kv => $"{kv.Key}={kv.Value}"));
            table.AddRow("Field Values", Markup.Escape(fields));
        }

        AnsiConsole.Write(table);
    }

    private void HandleReviewerAction(string instanceId, string action)
    {
        var envelope = engine.AdvanceAsReviewer(instanceId, action);
        if (envelope.ResponseState == "error")
        {
            var msg = envelope.Problems.FirstOrDefault()?.Message ?? "Unknown error";
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(msg)}");
            return;
        }

        var newState = envelope.Render?.StateDisplayName ?? "unknown";
        AnsiConsole.MarkupLine(
            $"[green]✔[/] {Markup.Escape(action)} applied to [bold]{Markup.Escape(instanceId)}[/] → [cyan]{Markup.Escape(newState)}[/]");
    }

    private void HandleReset(string instanceId)
    {
        var removed = engine.Reset(instanceId);
        if (removed)
            AnsiConsole.MarkupLine($"[green]✔[/] Instance [bold]{Markup.Escape(instanceId)}[/] removed.");
        else
            AnsiConsole.MarkupLine($"[red]Not found:[/] {Markup.Escape(instanceId)}");
    }

    private void HandleDefs()
    {
        var defs = engine.GetAllDefinitions().ToList();
        if (defs.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey](no definitions loaded)[/]");
            return;
        }

        var table = new Table()
            .AddColumn("[bold]Key[/]")
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]States[/]")
            .AddColumn("[bold]Transitions[/]");

        foreach (var d in defs)
        {
            table.AddRow(
                Markup.Escape(d.DefinitionKey),
                Markup.Escape(d.DisplayName),
                d.States.Count.ToString(),
                d.Transitions.Count.ToString());
        }

        AnsiConsole.Write(table);
    }

    private static void HandleHelp()
    {
        var table = new Table { ShowHeaders = false }
            .AddColumn("Command")
            .AddColumn("Description");

        table.AddRow("[bold]list[/]", "List all workflow instances");
        table.AddRow("[bold]show <id>[/]", "Show details of a single instance");
        table.AddRow("[bold]approve <id>[/]", "Advance instance as reviewer with action 'approve'");
        table.AddRow("[bold]reject <id>[/]", "Advance instance as reviewer with action 'reject'");
        table.AddRow("[bold]reset <id>[/]", "Delete an instance from engine state");
        table.AddRow("[bold]defs[/]", "List all loaded workflow definitions");
        table.AddRow("[bold]help[/]", "Show this help");
        table.AddRow("[bold]quit[/] / [bold]exit[/]", "Shut down the application");

        AnsiConsole.Write(table);
    }

    private static void PrintBanner()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Umbraco Prism — Workflow TUI[/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine("[grey]Type [bold]help[/] for available commands.[/]");
        AnsiConsole.WriteLine();
    }
}
