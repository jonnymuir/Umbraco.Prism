using Spectre.Console;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Background service that hosts a Spectre.Console REPL for driving the
/// <see cref="BusinessAppWorkflowEngine"/> from the terminal during local development.
/// Replaces the HTTP WorkflowEmulatorController.
/// </summary>
public sealed class WorkflowTuiService(BusinessAppWorkflowEngine engine) : BackgroundService
{
    /// <summary>Currently selected instance ID, used as default when no ID is supplied to a command.</summary>
    private string? _selectedInstanceId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Skip TUI in non-interactive environments (CI, Aspire, containers, etc.)
        if (Console.IsInputRedirected)
            return;

        // Let host startup messages settle before printing the banner.
        await Task.Delay(1500, stoppingToken).ConfigureAwait(false);

        PrintBanner();

        while (!stoppingToken.IsCancellationRequested)
        {
            var prompt = _selectedInstanceId is not null
                ? $"[cyan]({Markup.Escape(TruncateId(_selectedInstanceId))})[/] [grey]>[/] "
                : "[grey]>[/] ";
            AnsiConsole.Markup(prompt);

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

    private static string TruncateId(string id) =>
        id.Length > 13 ? id[..8] + "…" : id;

    private Task DispatchAsync(string line, CancellationToken ct)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        // Helper: resolve instance ID — accepts an explicit GUID or a list number,
        // or falls back to the currently selected instance.
        string? ResolveId(string? arg = null)
        {
            if (arg is null)
                return _selectedInstanceId;

            // If arg is a small integer, treat it as a 1-based list index.
            if (int.TryParse(arg, out var idx))
            {
                var all = engine.GetAllInstances().ToList();
                if (idx < 1 || idx > all.Count)
                {
                    AnsiConsole.MarkupLine($"[red]No instance at position {idx}. Run [bold]list[/] to see available instances.[/]");
                    return null;
                }
                return all[idx - 1].InstanceId;
            }

            return arg;
        }

        switch (command)
        {
            case "list":
                HandleList();
                break;

            case "select" when parts.Length >= 2:
                HandleSelect(parts[1]);
                break;

            case "select":
                AnsiConsole.MarkupLine("[yellow]Usage:[/] select <number|instanceId>  — pick an instance as the default for other commands");
                break;

            case "show":
            {
                var id = ResolveId(parts.Length >= 2 ? parts[1] : null);
                if (id is null) { AnsiConsole.MarkupLine("[yellow]Usage:[/] show <id|number>  (or select an instance first)"); break; }
                HandleShow(id);
                break;
            }

            case "approve":
            {
                var id = ResolveId(parts.Length >= 2 ? parts[1] : null);
                if (id is null) { AnsiConsole.MarkupLine("[yellow]Usage:[/] approve <id|number>  (or select an instance first)"); break; }
                HandleReviewerAction(id, "approve");
                break;
            }

            case "reject":
            {
                var id = ResolveId(parts.Length >= 2 ? parts[1] : null);
                if (id is null) { AnsiConsole.MarkupLine("[yellow]Usage:[/] reject <id|number>  (or select an instance first)"); break; }
                HandleReviewerAction(id, "reject");
                break;
            }

            case "reset":
            {
                var id = ResolveId(parts.Length >= 2 ? parts[1] : null);
                if (id is null) { AnsiConsole.MarkupLine("[yellow]Usage:[/] reset <id|number>  (or select an instance first)"); break; }
                HandleReset(id);
                // Clear selection if we just removed the selected instance.
                if (id.Equals(_selectedInstanceId, StringComparison.OrdinalIgnoreCase))
                    _selectedInstanceId = null;
                break;
            }

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
            .AddColumn("[bold]#[/]")
            .AddColumn("[bold]Instance ID[/]")
            .AddColumn("[bold]Workflow Key[/]")
            .AddColumn("[bold]State[/]")
            .AddColumn("[bold]Tenant[/]")
            .AddColumn("[bold]User[/]");

        for (var n = 0; n < instances.Count; n++)
        {
            var i = instances[n];
            var isSelected = i.InstanceId.Equals(_selectedInstanceId, StringComparison.OrdinalIgnoreCase);
            var numCell = isSelected ? $"[green bold]{n + 1} ✔[/]" : (n + 1).ToString();
            table.AddRow(
                numCell,
                Markup.Escape(i.InstanceId),
                Markup.Escape(i.WorkflowKey),
                $"[cyan]{Markup.Escape(i.CurrentState)}[/]",
                Markup.Escape(i.TenantId),
                Markup.Escape(i.UserId));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{instances.Count} instance(s) — use [bold]select <number>[/] to set the active instance[/]");
    }

    private void HandleSelect(string arg)
    {
        var instances = engine.GetAllInstances().ToList();

        string? resolvedId;
        if (int.TryParse(arg, out var idx))
        {
            if (idx < 1 || idx > instances.Count)
            {
                AnsiConsole.MarkupLine($"[red]No instance at position {idx}. Run [bold]list[/] first.[/]");
                return;
            }
            resolvedId = instances[idx - 1].InstanceId;
        }
        else
        {
            var match = instances.FirstOrDefault(
                i => i.InstanceId.Equals(arg, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                AnsiConsole.MarkupLine($"[red]Instance not found:[/] {Markup.Escape(arg)}");
                return;
            }
            resolvedId = match.InstanceId;
        }

        _selectedInstanceId = resolvedId;
        AnsiConsole.MarkupLine($"[green]✔[/] Selected instance: [bold]{Markup.Escape(resolvedId)}[/]");
        AnsiConsole.MarkupLine("[grey]Commands (show, approve, reject, reset) will now use this instance if no ID is given.[/]");
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

        table.AddRow("[bold]list[/]", "List all workflow instances (with row numbers)");
        table.AddRow("[bold]select[/] <number|id>", "Set the active instance (used as default by other commands)");
        table.AddRow("[bold]show[/] [[<id|number>]]", "Show details of an instance (uses active if omitted)");
        table.AddRow("[bold]approve[/] [[<id|number>]]", "Advance instance as reviewer with action 'approve'");
        table.AddRow("[bold]reject[/] [[<id|number>]]", "Advance instance as reviewer with action 'reject'");
        table.AddRow("[bold]reset[/] [[<id|number>]]", "Delete an instance from engine state");
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
