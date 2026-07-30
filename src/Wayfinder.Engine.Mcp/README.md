# Wayfinder.Engine.Mcp

Exposes Wayfinder's service blueprint authoring — list, read, validate, save, simulate — as MCP
(Model Context Protocol) tools over HTTP, so an AI agent can call them directly. Built on
the official [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)'s HTTP
transport (`ModelContextProtocol.AspNetCore`).

This is a library, not a standalone process. A host adds `MapPrismServiceBlueprintAuthoringMcp()`
to its own ASP.NET Core pipeline, alongside
[`MapPrismServiceBlueprintAuthoringApi()`](../Wayfinder.Engine.Api) — both hit the
same live `ServiceBlueprintAuthoringService`/`IServiceBlueprintSourceStore`, in-process. That matters: an
MCP server can't run *inside* an externally-spawned stdio process and still see an app's
live state, but hosted this way, a `save_service_blueprint` call reaches the running engine
immediately — no restart, no separate process to keep track of.

[`UmbracoPrism.MockBusinessApp`](../UmbracoPrism.MockBusinessApp) is the reference
implementation — it calls both `MapPrismServiceBlueprintAuthoringApi()` and
`MapPrismServiceBlueprintAuthoringMcp()` against its own live store, demonstrating what a real
host app does to expose this surface.

## Tools

| Tool | Description |
|---|---|
| `list_service_blueprints` | List every service blueprint definition in the store (key + display name). |
| `read_service_blueprint` | Read a service blueprint definition by `definitionKey`. |
| `list_queue_capabilities` | List every queue this host has declared render capabilities for, and which component types each supports. A queue absent from the result is unrestricted (not this host's declared concern). |
| `validate_service_blueprint` | Check gateway routing, any `calculations` block, and (when the host declares queue render capabilities) that every component is supported by its state's queue, without saving. |
| `save_service_blueprint` | Validate and save. Invalid definitions are rejected, not saved. Visible to the live app immediately. |
| `simulate_service_blueprint` | Dry-run a scripted sequence of actions with zero persistence. Returns `{ trace, calculations }` — the resulting state trace, plus the raw calculated field/series values per step (not just what's baked into rendered UI text). Accepts optional `mockServiceInputsJson` to resolve any `source: "service"` calculation field. |

## Resources

Alongside the tools, this project also registers four MCP resources — the canonical
authoring docs, embedded from `docs/guides/` at build time, fetchable directly by any
MCP client with no repo checkout:

| Resource URI | Content |
|---|---|
| `service-blueprint-docs://calculation-language` | [The Prism Calculation Language](../../docs/guides/calculation-language.md) — grammar, functions, tables/series, `showWhen`. |
| `service-blueprint-docs://authoring-guide` | [Reference Service Blueprint Contract](../../docs/guides/reference-service-blueprint-contract.md) — the full `ServiceBlueprint` JSON shape. |
| `service-blueprint-docs://service-design-principles` | [Service Design Principles](../../docs/guides/service-design-principles.md) — Double Diamond, the GOV.UK Service Standard, and Lou Downe's 15 principles of good services, industry-agnostic. |
| `service-blueprint-docs://ai-service-blueprint-authoring` | [AI-Ready Service Blueprint Authoring — Integrator Guide](../../docs/guides/ai-service-blueprint-authoring.md) — how a host app wires this MCP surface into its own pipeline. |

## Connect it to Claude Code

Start the app you want to author service blueprints against, find its URL (via the Aspire
dashboard — `MockBusinessApp`'s row has a labeled **"Service Blueprint Authoring MCP (HTTP)"**
link), then:

```
claude mcp add --transport http prism-service-blueprint http://localhost:<port>/prism/service-blueprint-authoring/mcp
```

Use the **HTTP** URL, not HTTPS. There's also a plain "Service Blueprint Authoring MCP" link on
HTTPS in the dashboard, but most MCP HTTP clients — including Claude Code's — reject the
local ASP.NET Core dev certificate with "unable to verify the first certificate" since
it's self-signed. Plain HTTP is fine here: it never leaves localhost. SSE is deprecated;
this uses the modern Streamable HTTP transport.

## Auth

If the host's authoring endpoints require authentication (a real host should add its own
— see `MapPrismServiceBlueprintAuthoringMcp()`, whose return value chains `.RequireAuthorization()`
the same way `MapPrismServiceBlueprintAuthoringApi()`'s does), pass credentials at registration:

```
claude mcp add --transport http prism-service-blueprint <url> --header "Authorization: Bearer <token>"
```

The reference app (`MockBusinessApp`) doesn't require this — its endpoints are
intentionally unauthenticated, same as its existing editor endpoints.

## A note on tool selection

If you're running Claude Code from within a checkout of the Prism repo itself (or any
repo that happens to contain the same seed/source files the connected app was built
from), the agent has ordinary file tools available alongside these MCP tools — nothing
stops it from finding and editing a seed JSON file directly instead of calling
`save_service_blueprint`. Doing so has no effect on the running app (seed files are only read at
process startup) and skips validation entirely. The tool descriptions call this out
explicitly, but if you want a clean test of tool selection, run Claude Code from a
directory with no copy of the host app's source in it — MCP tools remain reachable over
HTTP regardless of working directory; there's just nothing else to find.
