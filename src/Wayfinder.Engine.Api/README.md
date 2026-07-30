# Wayfinder.Engine.Api

Exposes Wayfinder's service blueprint authoring — list, read, validate, save, simulate — as
REST endpoints. This is a library, not a standalone service: a host calls
`MapPrismServiceBlueprintAuthoringApi()` from its own ASP.NET Core pipeline. See the
[AI-Ready Service Blueprint Authoring guide](../../docs/guides/ai-service-blueprint-authoring.md)
for the full integrator recipe, including the companion
[`Wayfinder.Engine.Mcp`](../Wayfinder.Engine.Mcp) package, which exposes the same
operations as MCP tools.

## Setup

```csharp
builder.Services.AddSingleton<IServiceBlueprintSourceStore, YourServiceBlueprintSourceStore>();
builder.Services.AddPrismServiceBlueprintAuthoring(); // Wayfinder.Engine.Extensions

var app = builder.Build();
app.MapPrismServiceBlueprintAuthoringApi(); // defaults to prefix "/prism/service-blueprint-authoring"
```

`MapPrismServiceBlueprintAuthoringApi()` returns a `RouteGroupBuilder` — chain
`.RequireAuthorization()` (or any other ASP.NET Core policy) the same way you would for
any other endpoint group. This extension applies none itself.

## Routes

All request/response bodies are `ServiceBlueprint` and friends from
`Wayfinder.Models.ServiceDesign` — no bespoke DTOs except `SimulateServiceBlueprintRequest`,
which bundles the simulate inputs.

| Method | Route | Body | Response |
|---|---|---|---|
| `GET` | `/blueprints` | — | `200` `ServiceBlueprintSourceSummary[]` |
| `GET` | `/blueprints/{definitionKey}` | — | `200` `ServiceBlueprint`, or `404` |
| `GET` | `/blueprints/{definitionKey}/version` | — | `200` `{ version }`, or `404` — cheap to poll for staleness without fetching the full definition |
| `POST` | `/blueprints/validate` | `ServiceBlueprint` | `200` `ServiceBlueprintValidationOutcome` (`{ isValid, diagnostics }`, each diagnostic `{ code, path, message, severity }`) |
| `PUT` | `/blueprints/{definitionKey}` | `ServiceBlueprint` | `200` `ServiceBlueprintSaveOutcome` if saved; `400` (same shape) if invalid or `definitionKey` doesn't match the route; `409` if `version` is stale |
| `POST` | `/blueprints/simulate` | `SimulateServiceBlueprintRequest` (`{ blueprint, steps, mockServiceInputs? }`) | `200` `ServiceBlueprintSimulationResult` (`{ trace, calculations }`) — `trace` is the state trace; `calculations` is the raw calculated field/series values per step (parallel array, `null` for steps with no calculations block). `mockServiceInputs` supplies any `source: "service"` calculation field — without it, those fields (and anything calculated from them) are simply unresolved, not an error. |

`validate`/`save`/`simulate` never throw for an invalid blueprint — they report it in the
response body.

### Optimistic concurrency

`PUT` only saves if the body's `version` field still matches what's currently persisted —
the same guarantee `IProcessManager.Advance`'s `expectedStateVersion` already gives
running instances, extended to definitions. There's no separate version parameter: a
client that `GET`s a blueprint gets back its current `version`, and round-trips that same
field in the body it later `PUT`s — that round-tripped value *is* the expected version.
The store ignores whatever `version` the client sends as a value to persist; it only
compares against it, then authoritatively sets the real new version itself, so a client
can't fabricate an arbitrary version number.

A stale version returns `409 Conflict` with a `ServiceBlueprintSaveOutcome`-shaped body
(`{ status: "Conflict", currentVersion, diagnostics }`) — re-`GET` to see what's actually
there now, reapply your change on top of it, and `PUT` again with the fresh `version`.

Every `IServiceBlueprintSourceStore` implementation must perform this compare-and-write
atomically. The reference implementations (`FilesystemServiceBlueprintSourceStore`,
`FilesystemServiceBlueprintStore`, `InMemoryRuntimePublishedServiceBlueprintStore`) use an
in-process lock — correct for a single-process app, but **a real database-backed store
should use an atomic `UPDATE ... WHERE Version = @expectedVersion`** (the `WHERE` clause
*is* the atomic compare) rather than a separate read-then-compare-then-write, which
races under concurrent writers.

## Reference implementation

`UmbracoPrism.MockBusinessApp` wires this to `InMemoryRuntimePublishedServiceBlueprintStore` —
see [`Program.cs`](../UmbracoPrism.MockBusinessApp/Program.cs). That store's `SaveAsync`
calls `engine.UpdateDefinition(...)`, so a save through this API is visible to the live
running engine immediately — no restart.
