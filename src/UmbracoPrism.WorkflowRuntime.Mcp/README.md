# UmbracoPrism.WorkflowRuntime.Mcp

An MCP (Model Context Protocol) server that exposes Umbraco Prism workflow authoring —
list, read, validate, save, simulate — as tools an AI agent can call directly. Built on
the official [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk).

This is a thin protocol adapter. All the actual logic lives in
[`UmbracoPrism.WorkflowRuntime`](../UmbracoPrism.WorkflowRuntime) —
`WorkflowAuthoringService`, `IWorkflowSourceStore`, `WorkflowSimulationRunner` — so any
.NET host can build its own front door (MCP, HTTP API, CLI) on the same primitives. This
project is the reference one, wired up against
[`UmbracoPrism.MockBusinessApp`'s `workflow-seeds/`](../UmbracoPrism.MockBusinessApp/workflow-seeds)
directory by default.

## Tools

| Tool | Description |
|---|---|
| `list_workflows` | List every workflow definition in the store (key + display name). |
| `read_workflow` | Read a workflow definition's full JSON by `definitionKey`. |
| `validate_workflow` | Check gateway routing and any `calculations` block, without saving. |
| `save_workflow` | Validate and save. Invalid definitions are rejected, not saved. |
| `simulate_workflow` | Dry-run a scripted sequence of actions with zero persistence, and return the resulting state trace — the same shape the real runtime reports to a client. |

## Connect it to Claude Code

```
claude mcp add prism-workflow -- dotnet run --project src/UmbracoPrism.WorkflowRuntime.Mcp --
```

By default it points at `../UmbracoPrism.MockBusinessApp/workflow-seeds` relative to its
own build output. To point it at a different directory (a real host app's workflow store),
pass the path as the first argument or set `PRISM_WORKFLOW_SEEDS_PATH`:

```
claude mcp add prism-workflow -- dotnet run --project src/UmbracoPrism.WorkflowRuntime.Mcp -- /path/to/workflow-seeds
```

## A note on saves

The MCP server runs as its own process — it can't share a running `MockBusinessApp`
web process's in-memory engine. `save_workflow` writes straight to the seed `.json` files
on disk. A running `MockBusinessApp` reloads its definitions from those files on restart
(the same "memory-only, restart reloads from seed files" behaviour already documented for
the visual editor) — it does not hot-reload from a save made by this server.
