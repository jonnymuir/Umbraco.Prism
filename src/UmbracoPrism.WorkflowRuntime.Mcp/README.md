# UmbracoPrism.WorkflowRuntime.Mcp

An MCP (Model Context Protocol) server that exposes Umbraco Prism workflow authoring —
list, read, validate, save, simulate — as tools an AI agent can call directly. Built on
the official [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk).

This is a thin, generic HTTP client wrapper — it holds no state and knows no domain
types. It proxies every tool call to a **running app's own**
`MapPrismWorkflowAuthoringApi()` endpoints
(from [`UmbracoPrism.WorkflowRuntime.Api`](../UmbracoPrism.WorkflowRuntime.Api)). That's
deliberate: an MCP server can't run *inside* the app process it's authoring against (it's
spawned as its own process over stdio), but it needs the app's live context — its live
engine, its live in-memory definitions — not a static copy of some files. Proxying to the
app's own HTTP API gives it exactly that, and lets a save show up to the running engine
immediately, with no restart.

[`UmbracoPrism.MockBusinessApp`](../UmbracoPrism.MockBusinessApp) is the reference
implementation of the API side — it calls `MapPrismWorkflowAuthoringApi()` wired to its
own live `IWorkflowSourceStore`, demonstrating exactly what a real host app does to expose
this surface.

## Tools

| Tool | Description |
|---|---|
| `list_workflows` | List every workflow definition in the store (key + display name). |
| `read_workflow` | Read a workflow definition's full JSON by `definitionKey`. |
| `validate_workflow` | Check gateway routing and any `calculations` block, without saving. |
| `save_workflow` | Validate and save. Invalid definitions are rejected, not saved. Visible to the live app immediately. |
| `simulate_workflow` | Dry-run a scripted sequence of actions with zero persistence, and return the resulting state trace — the same shape the real runtime reports to a client. |

## Connect it to Claude Code

Start the app you want to author workflows against (e.g. `MockBusinessApp`, or via
Aspire), then:

```
claude mcp add prism-workflow -- dotnet run --project src/UmbracoPrism.WorkflowRuntime.Mcp -- https://localhost:<port>
```

The base URL is required — either as the first argument (shown above) or via
`PRISM_WORKFLOW_API_BASE_URL`. There's no default; a real host's port isn't predictable,
especially under Aspire.

## Auth

If the target app's authoring endpoints require authentication (a real host should add
its own — see `MapPrismWorkflowAuthoringApi()`'s docs), set `PRISM_WORKFLOW_API_TOKEN`
and it's attached as an `Authorization: Bearer` header on every request. The reference
app (`MockBusinessApp`) doesn't require this — its endpoints are intentionally
unauthenticated, same as its existing editor endpoints.

## Other env vars

- `PRISM_WORKFLOW_API_PREFIX` — override the route prefix if the target host mapped
  `MapPrismWorkflowAuthoringApi()` at something other than the default
  `/prism/workflow-authoring`.
