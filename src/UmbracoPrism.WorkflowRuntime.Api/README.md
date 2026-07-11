# UmbracoPrism.WorkflowRuntime.Api

Exposes Umbraco Prism workflow authoring — list, read, validate, save, simulate — as REST
endpoints. This is a library, not a standalone service: a host calls
`MapPrismWorkflowAuthoringApi()` from its own ASP.NET Core pipeline. See the
[AI-Ready Workflow Authoring guide](../../docs/guides/ai-workflow-authoring.md) for the
full integrator recipe, including the companion
[`UmbracoPrism.WorkflowRuntime.Mcp`](../UmbracoPrism.WorkflowRuntime.Mcp) package, which
exposes the same operations as MCP tools.

## Setup

```csharp
builder.Services.AddSingleton<IWorkflowSourceStore, YourWorkflowSourceStore>();
builder.Services.AddPrismWorkflowAuthoring(); // UmbracoPrism.WorkflowRuntime.Extensions

var app = builder.Build();
app.MapPrismWorkflowAuthoringApi(); // defaults to prefix "/prism/workflow-authoring"
```

`MapPrismWorkflowAuthoringApi()` returns a `RouteGroupBuilder` — chain
`.RequireAuthorization()` (or any other ASP.NET Core policy) the same way you would for
any other endpoint group. This extension applies none itself.

## Routes

All request/response bodies are `WorkflowDefinitionFile` and friends from
`UmbracoPrism.Shared.Models.Workflow` — no bespoke DTOs except `SimulateWorkflowRequest`,
which just bundles the two simulate inputs.

| Method | Route | Body | Response |
|---|---|---|---|
| `GET` | `/workflows` | — | `200` `WorkflowSourceSummary[]` |
| `GET` | `/workflows/{definitionKey}` | — | `200` `WorkflowDefinitionFile`, or `404` |
| `GET` | `/workflows/{definitionKey}/version` | — | `200` `{ version }`, or `404` — cheap to poll for staleness without fetching the full definition |
| `POST` | `/workflows/validate` | `WorkflowDefinitionFile` | `200` `WorkflowValidationOutcome` (`{ isValid, errors }`) |
| `PUT` | `/workflows/{definitionKey}` | `WorkflowDefinitionFile` | `200` `WorkflowSaveOutcome` if saved; `400` (same shape) if invalid or `definitionKey` doesn't match the route; `409` if `version` is stale |
| `POST` | `/workflows/simulate` | `SimulateWorkflowRequest` (`{ workflow, steps }`) | `200` `WorkflowResponseEnvelope[]` — the state trace |

`validate`/`save`/`simulate` never throw for an invalid workflow — they report it in the
response body.

### Optimistic concurrency

`PUT` only saves if the body's `version` field still matches what's currently persisted —
the same guarantee `IWorkflowRuntimeEngine.Advance`'s `expectedStateVersion` already gives
running instances, extended to definitions. There's no separate version parameter: a
client that `GET`s a workflow gets back its current `version`, and round-trips that same
field in the body it later `PUT`s — that round-tripped value *is* the expected version.
The store ignores whatever `version` the client sends as a value to persist; it only
compares against it, then authoritatively sets the real new version itself, so a client
can't fabricate an arbitrary version number.

A stale version returns `409 Conflict` with a `WorkflowSaveOutcome`-shaped body
(`{ status: "Conflict", currentVersion, errors }`) — re-`GET` to see what's actually
there now, reapply your change on top of it, and `PUT` again with the fresh `version`.

Every `IWorkflowSourceStore` implementation must perform this compare-and-write
atomically. The reference implementations (`FilesystemWorkflowSourceStore`,
`FilesystemPublishedWorkflowStore`, `InMemoryRuntimePublishedWorkflowStore`) use an
in-process lock — correct for a single-process app, but **a real database-backed store
should use an atomic `UPDATE ... WHERE Version = @expectedVersion`** (the `WHERE` clause
*is* the atomic compare) rather than a separate read-then-compare-then-write, which
races under concurrent writers.

## Reference implementation

`UmbracoPrism.MockBusinessApp` wires this to `InMemoryRuntimePublishedWorkflowStore` —
see [`Program.cs`](../UmbracoPrism.MockBusinessApp/Program.cs). That store's `SaveAsync`
calls `engine.UpdateDefinition(...)`, so a save through this API is visible to the live
running engine immediately — no restart.
