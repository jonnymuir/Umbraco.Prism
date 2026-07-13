# AI-Ready Workflow Authoring

A guide for integrators. Let an AI agent (Claude Code or any MCP client) list, read,
validate, simulate, and save your business app's workflow definitions.

Prism doesn't build AI into itself. It ships a toolkit your business app hosts, the same
way it ships the workflow editor for humans to host (see
[Embedding the Workflow Editor](./embedding-the-workflow-editor.md)) — you add one or two
lines to your own pipeline, and the AI-facing surface runs inside your app's own process,
subject to your own auth.

---

## What You Get

Three layers, mirroring how the workflow engine itself is already layered:

| Layer | Package | What it does |
|---|---|---|
| Reusable authoring logic | `UmbracoPrism.WorkflowRuntime` | `WorkflowAuthoringService` — list/read/validate/save/simulate against an `IWorkflowSourceStore` you implement. `WorkflowSimulationRunner` dry-runs a definition through the real engine with zero persistence. |
| REST surface | `UmbracoPrism.WorkflowRuntime.Api` | `MapPrismWorkflowAuthoringApi()` — one extension method, maps the same operations as HTTP endpoints. |
| MCP surface | `UmbracoPrism.WorkflowRuntime.Mcp` | `MapPrismWorkflowAuthoringMcp()` — one extension method, maps the same operations as MCP tools over HTTP, so Claude Code (or any MCP client) can call them directly. |

Both surfaces call the same `WorkflowAuthoringService`, in-process. That matters: an MCP
server can't run *inside* an externally-spawned stdio process and still see your app's
live state, but hosted this way, a `save_workflow` tool call reaches your running engine
immediately — no restart, no separate process to keep track of, no proxying.

`UmbracoPrism.MockBusinessApp` is the reference implementation — see
[`Program.cs`](../../src/UmbracoPrism.MockBusinessApp/Program.cs) for exactly how it wires
both surfaces to its own `IWorkflowSourceStore`.

## What You Write

You need an `IWorkflowSourceStore`:

```csharp
public interface IWorkflowSourceStore
{
    Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default);
    Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default);
    Task<WorkflowSaveResult> SaveAsync(WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default);
}
```

Two ready-made implementations already exist in `UmbracoPrism.WorkflowRuntime.Stores`:
`FilesystemWorkflowSourceStore` (one JSON file per workflow) and, in
`MockBusinessApp`, `InMemoryRuntimePublishedWorkflowStore` — the pattern to copy if you
want a save to update your live runtime engine immediately (it calls
`engine.UpdateDefinition(...)` inside `SaveAsync`). A real app would usually back this
with a database.

### `SaveAsync` must be an atomic compare-and-swap

A human in the editor and an AI agent can both be working against the same workflow at
once — without a real concurrency check, whichever one saves last silently overwrites the
other with no warning. `SaveAsync` only writes if `expectedVersion` still matches what's
currently persisted, and returns `WorkflowSaveResult(Saved, CurrentVersion, Location)` so
the caller can tell success from a conflict. **This must be a single atomic operation, not
a separate read-then-compare-then-write** — the reference implementations use an
in-process lock (correct for a single-process app only); a real database-backed store
should use the `WHERE` clause itself as the atomic compare:

```sql
UPDATE Workflows SET Definition = @json, Version = Version + 1
WHERE DefinitionKey = @key AND Version = @expectedVersion
```

If `0` rows are affected, either the row doesn't exist yet or `Version` had already moved
on — either way, that's a conflict, not a success. `WorkflowAuthoringService.SaveAsync`
wraps this into `WorkflowSaveOutcome` (`Status`: `Saved`/`Invalid`/`Conflict`), which both
the REST `PUT` (409 on conflict) and the MCP `save_workflow` tool already surface — you
don't need to build this part yourself, just implement the store correctly.

### Wiring it up

```csharp
builder.Services.AddSingleton<IWorkflowSourceStore, YourWorkflowSourceStore>();
builder.Services.AddPrismWorkflowAuthoring();      // registers WorkflowAuthoringService
builder.Services.AddPrismWorkflowAuthoringMcp();    // registers the MCP server

var app = builder.Build();

app.MapPrismWorkflowAuthoringApi();   // REST — GET/PUT /prism/workflow-authoring/workflows/*
app.MapPrismWorkflowAuthoringMcp();   // MCP  — POST   /prism/workflow-authoring/mcp
```

Both `Map...` calls return a chainable endpoint builder — chain `.RequireAuthorization()`
(or any other ASP.NET Core policy) the same way you would for any other endpoint. Prism
doesn't ship an auth story for this surface, the same way it doesn't enforce queue-level
access control for the runtime engine — that's always been the host's responsibility.
`MockBusinessApp` leaves both unauthenticated intentionally, to prove the boundary works
without inheriting an authoring policy.

## Connect Claude Code

Find your app's URL (under Aspire, `MockBusinessApp`'s dashboard row has a labeled
"Workflow Authoring MCP (HTTP)" link — use the HTTP one, not HTTPS: most MCP HTTP clients,
including Claude Code's, won't trust a local ASP.NET Core dev certificate), then:

```
claude mcp add --transport http prism-workflow http://localhost:<port>/prism/workflow-authoring/mcp
```

If your endpoints require auth, pass it at registration:

```
claude mcp add --transport http prism-workflow <url> --header "Authorization: Bearer <token>"
```

## Reference material for the agent

Two things worth pointing an agent at before it starts authoring, rather than
letting it infer syntax from trial and error:

- **[The Prism Calculation Language](./calculation-language.md)** — the grammar,
  function reference, and worked example for the `calculations` block and
  `showWhen` expressions. Also exposed as an MCP resource,
  `workflow-docs://calculation-language`, so an agent connected only over MCP (no
  repo checkout) can fetch it directly.
- **[Reference Workflow Contract](./reference-workflow-contract.md)** — the full
  `WorkflowDefinitionFile` shape: states, routes, gateways, queues, components,
  response states. Also exposed as `workflow-docs://authoring-guide`.
- **[Service Design Principles](./service-design-principles.md)** — the Design
  Council Double Diamond, the GOV.UK Service Standard, and Lou Downe's 15
  principles of good services, industry-agnostic and mapped to concrete
  authoring decisions. Also exposed as `workflow-docs://service-design-principles`.
  It deliberately stops short of sector-specific regulation or domain best
  practice (FCA Consumer Duty, PASA standards, and the like) — bring that
  yourself, as your own reference material alongside this one.

## The author loop

The MCP/REST tools compose into one iteration loop, whether the caller is a human
using them through a chat interface or an agent driving them directly:

1. **`list_workflows`** → **`read_workflow`** to see what exists and its current
   shape (and `version`, needed to save later).
2. **`list_queue_capabilities`**, if you haven't authored for this workflow's
   queues before — check what component types the queue's host actually
   supports before drafting, rather than finding out from
   `QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT` after the fact.
3. **Draft** a change against the real contract — reference the two docs above
   rather than guessing syntax.
4. **`validate_workflow`** on the draft *before* touching anything live — it
   checks gateway routing and every calculation/`showWhen` expression, returning
   structured diagnostics (`code`, `path`, `message`) an agent can act on directly
   rather than a single opaque error.
5. **`simulate_workflow`** to dry-run the draft through the real engine with no
   persistence — confirms it actually behaves as intended (right stage shown at
   the right time, right actions available) before it's saved. Returns the raw
   calculated field/series values alongside the trace, so you can check the maths
   directly instead of parsing rendered UI text. If the definition has a
   `source: "service"` calculation field, pass `mockServiceInputsJson` to resolve
   it — without one, those fields simply stay unresolved rather than erroring, the
   same as against a host with no data for them.
6. **`save_workflow`** with the `version` read in step 1. A concurrent edit
   (human or another agent) surfaces as a conflict, not a silent overwrite —
   reload and reapply.

This mirrors the proposal-first pattern the visual editor already follows for
human+AI co-authoring (draft → validate → simulate/preview → apply) — one shared
validation engine, one shared source of truth, whichever surface is doing the
editing.

### A note on tool selection

If Claude Code is running from a checkout of your app's own source (or Prism's), it has
ordinary file tools available alongside the MCP ones — nothing stops it from finding and
editing a seed/source file directly instead of calling `save_workflow`. Doing so has no
effect on a running app (source files are typically only read at process startup) and
skips validation entirely. The tool descriptions call this out explicitly. For a clean
test of tool selection, run Claude Code from a directory with no copy of your app's source
in it — the MCP tools stay reachable over HTTP regardless of working directory.

## Next Steps

1. **Implement `IWorkflowSourceStore`** for your business app's real persistence.
2. **Add the two `Map...` calls** to your `Program.cs`, with whatever `.RequireAuthorization()` policy you need.
3. **Read the reference implementation** at `src/UmbracoPrism.MockBusinessApp/Program.cs`.
4. **Read the toolkit projects' own READMEs** for the full wire contract:
   [`UmbracoPrism.WorkflowRuntime.Api`](../../src/UmbracoPrism.WorkflowRuntime.Api/README.md),
   [`UmbracoPrism.WorkflowRuntime.Mcp`](../../src/UmbracoPrism.WorkflowRuntime.Mcp/README.md).

---

## Related Documentation

- [Embedding the Workflow Editor](./embedding-the-workflow-editor.md) — the equivalent recipe for the human-facing visual editor
- [Reference Workflow Contract](./reference-workflow-contract.md) — the shape of `WorkflowDefinitionFile`, gateway routing rules

---

[← Back to Guides](README.md)
