# UmbracoPrism.WorkflowRuntime.Mcp

Exposes Umbraco Prism workflow authoring — list, read, validate, save, simulate — as MCP
(Model Context Protocol) tools over HTTP, so an AI agent can call them directly. Built on
the official [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)'s HTTP
transport (`ModelContextProtocol.AspNetCore`).

This is a library, not a standalone process. A host adds `MapPrismWorkflowAuthoringMcp()`
to its own ASP.NET Core pipeline, alongside
[`MapPrismWorkflowAuthoringApi()`](../UmbracoPrism.WorkflowRuntime.Api) — both hit the
same live `WorkflowAuthoringService`/`IWorkflowSourceStore`, in-process. That matters: an
MCP server can't run *inside* an externally-spawned stdio process and still see an app's
live state, but hosted this way, a `save_workflow` call reaches the running engine
immediately — no restart, no separate process to keep track of.

[`UmbracoPrism.MockBusinessApp`](../UmbracoPrism.MockBusinessApp) is the reference
implementation — it calls both `MapPrismWorkflowAuthoringApi()` and
`MapPrismWorkflowAuthoringMcp()` against its own live store, demonstrating what a real
host app does to expose this surface.

## Tools

| Tool | Description |
|---|---|
| `list_workflows` | List every workflow definition in the store (key + display name). |
| `read_workflow` | Read a workflow definition by `definitionKey`. |
| `validate_workflow` | Check gateway routing and any `calculations` block, without saving. |
| `save_workflow` | Validate and save. Invalid definitions are rejected, not saved. Visible to the live app immediately. |
| `simulate_workflow` | Dry-run a scripted sequence of actions with zero persistence, and return the resulting state trace. |

## Connect it to Claude Code

Start the app you want to author workflows against, find its URL (via the Aspire
dashboard — `MockBusinessApp`'s row has a labeled **"Workflow Authoring MCP (HTTP)"**
link), then:

```
claude mcp add --transport http prism-workflow http://localhost:<port>/prism/workflow-authoring/mcp
```

Use the **HTTP** URL, not HTTPS. There's also a plain "Workflow Authoring MCP" link on
HTTPS in the dashboard, but most MCP HTTP clients — including Claude Code's — reject the
local ASP.NET Core dev certificate with "unable to verify the first certificate" since
it's self-signed. Plain HTTP is fine here: it never leaves localhost. SSE is deprecated;
this uses the modern Streamable HTTP transport.

## Auth

If the host's authoring endpoints require authentication (a real host should add its own
— see `MapPrismWorkflowAuthoringMcp()`, whose return value chains `.RequireAuthorization()`
the same way `MapPrismWorkflowAuthoringApi()`'s does), pass credentials at registration:

```
claude mcp add --transport http prism-workflow <url> --header "Authorization: Bearer <token>"
```

The reference app (`MockBusinessApp`) doesn't require this — its endpoints are
intentionally unauthenticated, same as its existing editor endpoints.

## A note on tool selection

If you're running Claude Code from within a checkout of the Prism repo itself (or any
repo that happens to contain the same seed/source files the connected app was built
from), the agent has ordinary file tools available alongside these MCP tools — nothing
stops it from finding and editing a seed JSON file directly instead of calling
`save_workflow`. Doing so has no effect on the running app (seed files are only read at
process startup) and skips validation entirely. The tool descriptions call this out
explicitly, but if you want a clean test of tool selection, run Claude Code from a
directory with no copy of the host app's source in it — MCP tools remain reachable over
HTTP regardless of working directory; there's just nothing else to find.
