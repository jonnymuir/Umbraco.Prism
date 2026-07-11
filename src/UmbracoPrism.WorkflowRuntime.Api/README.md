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
| `POST` | `/workflows/validate` | `WorkflowDefinitionFile` | `200` `WorkflowValidationOutcome` (`{ isValid, errors }`) |
| `PUT` | `/workflows/{definitionKey}` | `WorkflowDefinitionFile` | `200` `WorkflowValidationOutcome` if saved; `400` (same shape) if invalid or if `definitionKey` doesn't match the route |
| `POST` | `/workflows/simulate` | `SimulateWorkflowRequest` (`{ workflow, steps }`) | `200` `WorkflowResponseEnvelope[]` — the state trace |

`validate`/`save`/`simulate` never throw for an invalid workflow — they report it in the
response body. `save` only persists (`IWorkflowSourceStore.SaveAsync`) when `isValid` is
true.

## Reference implementation

`UmbracoPrism.MockBusinessApp` wires this to `InMemoryRuntimePublishedWorkflowStore` —
see [`Program.cs`](../UmbracoPrism.MockBusinessApp/Program.cs). That store's `SaveAsync`
calls `engine.UpdateDefinition(...)`, so a save through this API is visible to the live
running engine immediately — no restart.
