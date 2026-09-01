# AI-Ready Service Blueprint Authoring

A guide for integrators. Let an AI agent (Claude Code or any MCP client) list, read,
validate, simulate, and save your business app's service blueprints.

Wayfinder doesn't build AI into itself. It ships a toolkit your business app hosts, the same
way it ships the service blueprint editor for humans to host (see
[Embedding the Service Blueprint Editor](./embedding-the-service-blueprint-editor.md)), you add one or two
lines to your own pipeline, and the AI-facing surface runs inside your app's own process,
subject to your own auth.

---

## What You Get

Three layers, mirroring how the service blueprint engine itself is already layered:

| Layer | Package | What it does |
|---|---|---|
| Reusable authoring logic | `Wayfinder.Engine` | `ServiceBlueprintAuthoringService`, list/read/validate/save/simulate against an `IServiceBlueprintSourceStore` you implement. `ServiceBlueprintSimulationRunner` dry-runs a definition through the real engine with zero persistence. |
| REST surface | `Wayfinder.Engine.Api` | `MapServiceBlueprintAuthoringApi()`, one extension method, maps the same operations as HTTP endpoints. |
| MCP surface | `Wayfinder.Engine.Mcp` | `MapServiceBlueprintAuthoringMcp()`, one extension method, maps the same operations as MCP tools over HTTP, so Claude Code (or any MCP client) can call them directly. |

Both surfaces call the same `ServiceBlueprintAuthoringService`, in-process. That matters: an MCP
server can't run *inside* an externally-spawned stdio process and still see your app's
live state, but hosted this way, a `save_service_blueprint` tool call reaches your running engine
immediately, no restart, no separate process to keep track of, no proxying.

`UmbracoPrism.MockBusinessApp` is the reference implementation, see
[`Program.cs`](../../src/UmbracoPrism.MockBusinessApp/Program.cs) for exactly how it wires
both surfaces to its own `IServiceBlueprintSourceStore`.

## What You Write

You need an `IServiceBlueprintSourceStore`:

```csharp
public interface IServiceBlueprintSourceStore
{
    Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default);
    Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default);
    Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint service-blueprint, int expectedVersion, CancellationToken ct = default);
}
```

Two ready-made implementations already exist in `Wayfinder.Engine.Stores`:
`FilesystemServiceBlueprintSourceStore` (one JSON file per service blueprint) and, in
`MockBusinessApp`, `InMemoryRuntimePublishedServiceBlueprintStore`, the pattern to copy if you
want a save to update your live runtime engine immediately (it calls
`engine.UpdateDefinition(...)` inside `SaveAsync`). A real app would usually back this
with a database.

### `SaveAsync` must be an atomic compare-and-swap

A human in the editor and an AI agent can both be working against the same service blueprint at
once, without a real concurrency check, whichever one saves last silently overwrites the
other with no warning. `SaveAsync` only writes if `expectedVersion` still matches what's
currently persisted, and returns `ServiceBlueprintSaveResult(Saved, CurrentVersion, Location)` so
the caller can tell success from a conflict. **This must be a single atomic operation, not
a separate read-then-compare-then-write**, the reference implementations use an
in-process lock (correct for a single-process app only); a real database-backed store
should use the `WHERE` clause itself as the atomic compare:

```sql
UPDATE Service-Blueprints SET Definition = @json, Version = Version + 1
WHERE DefinitionKey = @key AND Version = @expectedVersion
```

If `0` rows are affected, either the row doesn't exist yet or `Version` had already moved
on, either way, that's a conflict, not a success. `ServiceBlueprintAuthoringService.SaveAsync`
wraps this into `ServiceBlueprintSaveOutcome` (`Status`: `Saved`/`Invalid`/`Conflict`), which both
the REST `PUT` (409 on conflict) and the MCP `save_service_blueprint` tool already surface, you
don't need to build this part yourself, just implement the store correctly.

### Wiring it up

```csharp
builder.Services.AddSingleton<IServiceBlueprintSourceStore, YourServiceBlueprintSourceStore>();
builder.Services.AddServiceBlueprintAuthoring();      // registers ServiceBlueprintAuthoringService
builder.Services.AddServiceBlueprintAuthoringMcp();    // registers the MCP server

var app = builder.Build();

app.MapServiceBlueprintAuthoringApi();   // REST — GET/PUT /wayfinder/service-blueprint-authoring/service-blueprints/*
app.MapServiceBlueprintAuthoringMcp();   // MCP  — POST   /wayfinder/service-blueprint-authoring/mcp
```

Both `Map...` calls return a chainable endpoint builder, chain `.RequireAuthorization()`
(or any other ASP.NET Core policy) the same way you would for any other endpoint. Wayfinder
doesn't ship an auth story for this surface, the same way it doesn't enforce queue-level
access control for the runtime engine, that's always been the host's responsibility.
`MockBusinessApp` leaves both unauthenticated intentionally, to prove the boundary works
without inheriting an authoring policy.

## Connect Claude Code

Find your app's URL (under Aspire, `MockBusinessApp`'s dashboard row has a labeled
"Service Blueprint Authoring MCP (HTTP)" link, use the HTTP one, not HTTPS: most MCP HTTP clients,
including Claude Code's, won't trust a local ASP.NET Core dev certificate), then:

```
claude mcp add --transport http wayfinder-service-blueprint http://localhost:<port>/wayfinder/service-blueprint-authoring/mcp
```

If your endpoint requires auth, pass it at registration:

```
claude mcp add --transport http wayfinder-service-blueprint <url> --header "Authorization: Bearer <token>"
```

`UmbracoPrism.MockBusinessApp` is this repo's only MCP host, it leaves the endpoint
unauthenticated intentionally, to prove the toolkit's auth boundary is real without inheriting
a policy (its `/admin/service-blueprint/*` and `/service-blueprint-editor` routes have no auth
either). If you want an authed example, add `.RequireAuthorization()` (or any other ASP.NET
Core policy) to your own `Map...` calls per the "Wiring it up" section above, Umbraco's own
client-credentials grant on `/umbraco/management/api/v1/security/back-office/token` works well
for this if your host is Umbraco-based (the same flow the interactive backoffice SPA uses,
just with `grant_type=client_credentials`), but that's a decision for your own host to make,
not something this toolkit prescribes.

## Reference material for the agent

Two things worth pointing an agent at before it starts authoring, rather than
letting it infer syntax from trial and error:

- **[The Wayfinder Calculation Language](./calculation-language.md)**: the grammar,
  function reference, and worked example for the `calculations` block and
  `showWhen` expressions. Also exposed as an MCP resource,
  `service-blueprint-docs://calculation-language`, so an agent connected only over MCP (no
  repo checkout) can fetch it directly.
- **[Reference Service Blueprint Contract](./reference-service-blueprint-contract.md)**: the full
  `ServiceBlueprint` shape: stages, routes, gateways, queues, components,
  response states. Also exposed as `service-blueprint-docs://authoring-guide`.
- **[Service Design Principles](./service-design-principles.md)**: the Design
  Council Double Diamond, the GOV.UK Service Standard, and Lou Downe's 15
  principles of good services, industry-agnostic and mapped to concrete
  authoring decisions. Also exposed as `service-blueprint-docs://service-design-principles`.
  It deliberately stops short of sector-specific regulation or domain best
  practice (FCA Consumer Duty, PASA standards, and the like), bring that
  yourself, as your own reference material alongside this one.

## The author loop

The MCP/REST tools compose into one iteration loop, whether the caller is a human
using them through a chat interface or an agent driving them directly:

1. **`list_service_blueprints`** → **`read_service_blueprint`** to see what exists and its current
   shape (and `version`, needed to save later).
2. **`list_queue_capabilities`**, if you haven't authored for this service blueprint's
   queues before, check what component types the queue's host actually
   supports before drafting, rather than finding out from
   `QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT` after the fact.
3. **Draft** a change against the real contract, reference the two docs above
   rather than guessing syntax.
4. **`validate_service_blueprint`** on the draft *before* touching anything live, it
   checks gateway routing and every calculation/`showWhen` expression, returning
   structured diagnostics (`code`, `path`, `message`) an agent can act on directly
   rather than a single opaque error.
5. **`simulate_service_blueprint`** to dry-run the draft through the real engine with no
   persistence, confirms it actually behaves as intended (right stage shown at
   the right time, right actions available) before it's saved. Returns the raw
   calculated field/series values alongside the trace, so you can check the maths
   directly instead of parsing rendered UI text. If the definition has a
   `source: "service"` calculation field, pass `mockServiceInputsJson` to resolve
   it, without one, those fields simply stay unresolved rather than erroring, the
   same as against a host with no data for them.
6. **`save_service_blueprint`** with the `version` read in step 1. A concurrent edit
   (human or another agent) surfaces as a conflict, not a silent overwrite,
   reload and reapply.

This mirrors the proposal-first pattern the visual editor already follows for
human+AI co-authoring (draft → validate → simulate/preview → apply), one shared
validation engine, one shared source of truth, whichever surface is doing the
editing.

### A note on tool selection

If Claude Code is running from a checkout of your app's own source (or Prism's), it has
ordinary file tools available alongside the MCP ones, nothing stops it from finding and
editing a seed/source file directly instead of calling `save_service_blueprint`. Doing so has no
effect on a running app (source files are typically only read at process startup) and
skips validation entirely. The tool descriptions call this out explicitly. For a clean
test of tool selection, run Claude Code from a directory with no copy of your app's source
in it, the MCP tools stay reachable over HTTP regardless of working directory.

## Next Steps

1. **Implement `IServiceBlueprintSourceStore`** for your business app's real persistence.
2. **Add the two `Map...` calls** to your `Program.cs`, with whatever `.RequireAuthorization()` policy you need.
3. **Read the reference implementation** at `src/UmbracoPrism.MockBusinessApp/Program.cs`.
4. **Read the toolkit projects' own READMEs** for the full wire contract:
   [`Wayfinder.Engine.Api`](https://github.com/jonnymuir/Wayfinder/blob/main/Wayfinder.Engine.Api/README.md),
   [`Wayfinder.Engine.Mcp`](https://github.com/jonnymuir/Wayfinder/blob/main/Wayfinder.Engine.Mcp/README.md).

---

## Related Documentation

- [Embedding the Service Blueprint Editor](./embedding-the-service-blueprint-editor.md), the equivalent recipe for the human-facing visual editor
- [Reference Service Blueprint Contract](./reference-service-blueprint-contract.md), the shape of `ServiceBlueprint`, gateway routing rules

---

[← Back to Guides](README.md)
