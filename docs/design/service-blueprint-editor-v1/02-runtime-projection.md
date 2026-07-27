# 02 — Runtime Model and Service Blueprint Actions

**Date:** 2026-05-16  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented  
**Relates to:** `docs/design/service-blueprint-editor-v1/README.md`

---

## 1. Overview

This document explains the service blueprint engine architecture and the projection boundary between Prism's service-design domain and the host's business domain.

The editor owns the **design-time service blueprint** (the `AuthoredServiceBlueprint` model). The runtime owns **execution** (the `ServiceBlueprint` model that drives the service blueprint engine). To maintain compatibility with existing runtimes, the Prism projector (`IServiceBlueprintProjector`) transforms the richer authored model into the runtime shape that the engine already understands.

This document explains:

1. the **service blueprint** the editor saves (`AuthoredServiceBlueprint`)
2. the **action catalog** the editor reads (`ServiceBlueprintActionCatalog`)
3. the **service blueprint engine** that executes the definition (Prism runtime)
4. the **action handlers** that perform business work (host-specific)
5. the **projection** from authored to runtime model (`IServiceBlueprintProjector`)
6. the **publishing** boundary (host concern, not editor concern)

---

## 2. The Two Models

### Authored Model (`AuthoredServiceBlueprint`)

This is the service-design model. It lives in `UmbracoPrism.Core` (C#) and `UmbracoPrism.Client` (TypeScript). The editor reads and writes this model.

Key fields:

- `definitionKey` — stable identifier for the service blueprint
- `displayName` — human-readable name
- `initialStageKey` — where the service blueprint starts
- `stages[]` — the steps in the journey (each has `key`, `label`, `kind`, `actor`, `view`, `actions`)
- `gateways[]` — routing points (each has `key`, `title`, `kind` [Split/Join], `source?`, `routes[]`)

### Runtime Model (`ServiceBlueprint`)

This is the execution model. It lives in `UmbracoPrism.Core`. The runtime engine reads this model.

Key fields:

- `key` — stable identifier
- `initialState` — where the service blueprint starts
- `states[]` — the runtime states (projected from stages)
- `transitions[]` — the runtime transitions (projected from gateway routes)
- `components[]` — the UI components for each state

The runtime model is **flatter** and **simpler** than the authored model. It is optimized for execution, not authoring.

---

## 3. The Projection Boundary

The `IServiceBlueprintProjector` interface (in `UmbracoPrism.Core`) is the Prism API for converting authored service blueprints into runtime definitions.

### Interface

```csharp
public interface IServiceBlueprintProjector
{
    /// <summary>
    /// Projects an authored service-blueprint into the runtime definition shape.
    /// Returns a validation result with the projected definition or diagnostic messages.
    /// </summary>
    WorkflowProjectionResult Project(AuthoredServiceBlueprint authoredWorkflow);
}
```

The projector:

- Validates the authored structure (schema + structural lint rules).
- Converts `stages[]` + `gateways[]` into `states[]` + `transitions[]`.
- Strips editor-only metadata (e.g., canvas layout hints).
- Preserves authored assignment data (`actor`, `roleGates`) for runtime authorization.
- Returns diagnostics if the service blueprint cannot be projected (e.g., missing stage, orphan gateway).

### Projection Rules

| Authored | Runtime |
|----------|---------|
| `AuthoredTouchpoint` | `StepDefinition` |
| `AuthoredGateway.routes[]` | `ServiceBlueprintRouteDefinition[]` (one per route, `from = gateway.source`, `to = route.target`) |
| `AuthoredRoute.trigger` | `ServiceBlueprintRouteDefinition.Trigger` |
| `AuthoredRoute.condition` | `ServiceBlueprintRouteDefinition.condition` |
| `AuthoredRoute.requiresRole` | `ServiceBlueprintRouteDefinition.requiresRole` |
| `AuthoredRoute.actions` | `ServiceBlueprintRouteDefinition.actions` (typed actions for handlers) |

**Key point:** Gateways do not appear in the runtime model as first-class entities. They are expanded into a flat list of transitions. The runtime engine does not need to understand gateway semantics — it just walks transitions.

---

## 4. Typed Actions

A typed action is a declarative instruction for the runtime to execute some business work. Each action has:

- `type` — stable key (e.g., `forms.submit`, `notifications.send-email`, `case.assign`)
- `params` — serializable JSON object (validated against the action's `paramsSchema` at design time)

Example:

```json
{
  "type": "forms.submit",
  "params": {
    "formDefinitionId": "planning-applicant-details"
  }
}
```

The editor validates the `params` shape against the action's schema (provided by the `ServiceBlueprintActionCatalog`). The runtime resolves the `type` to a handler and executes the action.

This split keeps service blueprints **declarative**. No callbacks, no source code, no app-specific method names. Just stable keys and serializable params.

---

## 5. Action Catalog

The `ServiceBlueprintActionCatalog` interface (in `UmbracoPrism.Client`) is the boundary contract for listing available actions at design time.

```typescript
export interface ServiceBlueprintActionCatalog {
  entries(): Promise<ActionCatalogEntry[]>;
}
```

Each `ActionCatalogEntry` includes:

- `type` — stable key
- `label` — display name for the editor
- `summary` — what the action does
- `appliesTo` — where the action is valid (`stage.onEntry`, `stage.onExit`, `transition`)
- `paramsSchema` — JSON Schema for parameters
- `defaultParams` — starter values
- `examples` — example configurations
- `status` — `available`, `planned`, or `internal`
- `implementation` — whether the reference business app has a handler

Prism ships `BuiltInServiceBlueprintActionCatalog` with generic actions (Send Email, Assign Case, etc.). Hosts extend it to add domain-specific actions.

---

## 6. Runtime Execution (Handler Registry Pattern)

The runtime engine (in `UmbracoPrism.ProcessManager`) is **generic**. It:

- Loads the service blueprint (`ServiceBlueprint`).
- Tracks the current state for each instance.
- Checks which transitions are allowed.
- Moves the instance to the next state.
- Builds the runtime response envelope.

The engine does **not** execute typed actions itself. It delegates to a handler registry.

### Recommended Handler Interface

```csharp
public interface IServiceBlueprintActionHandler
{
    string ActionType { get; }
    Task<ActionExecutionResult> ExecuteAsync(
        ServiceBlueprintActionDefinition action,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IServiceBlueprintActionRegistry
{
    IServiceBlueprintActionHandler? Resolve(string actionType);
    IReadOnlyList<ActionCatalogEntry> GetCatalog();
}
```

### Runtime Flow

1. The engine loads the current state.
2. The engine finds any stage or transition actions that should run.
3. For each action, the engine asks the registry for the handler that matches `action.type`.
4. The handler reads the typed `params` and the runtime context.
5. The handler performs the business work (send email, assign case, etc.).
6. The engine continues the service blueprint using the transition result.

This pattern keeps action execution **out of the engine**. The engine stays generic. The host provides the handlers.

---

## 7. Publishing — A Host Concern, Not an Editor Concern

**Publishing** is the act of snapshotting an authored service blueprint into a runtime store so that the runtime engine can load it and execute instances.

This is a **host concern**, not an editor concern. The editor never publishes service blueprints itself. The editor only saves `AuthoredServiceBlueprint` objects through the host's `ServiceBlueprintSource`.

The host decides:

- **When** to publish (on save? on explicit publish button? on approval by a reviewer?).
- **Where** to publish (database? blob storage? in-memory cache?).
- **How** to version (overwrite? keep history? snapshot to a new key?).
- **Who** can publish (enforce authorization in the host's publish endpoint, not in the editor).

### Example Flow (MockBusinessApp)

MockBusinessApp demonstrates the pattern:

1. The editor saves an `AuthoredServiceBlueprint` via `PUT /mockapp/service-blueprints/{key}`.
2. MockBusinessApp stores the authored service blueprint in memory (its `ReferenceAuthoredServiceBlueprintStore`).
3. Separately, MockBusinessApp has a `ServiceBlueprintPublishService` (in `MockBusinessApp/Services/Publishing/`).
4. When the host calls `publishService.PublishAsync(blueprintKey)`, the service:
   - Loads the authored service blueprint from the store.
   - Calls `IServiceBlueprintProjector.Project(authoredWorkflow)` to get the runtime definition.
   - Validates the projection result.
   - Saves the runtime definition to the published store (`IPublishedWorkflowStore`).
5. The runtime engine (in `UmbracoPrism.ProcessManager`) loads definitions from the published store, never from the authored store.

The editor has no opinion about steps 3-5. Those are host concerns.

### Why This Boundary Matters

Different hosts have different publishing needs:

- **Approval service blueprint:** Some hosts require a reviewer to approve a service blueprint before it goes live. The editor does not enforce this — the host does.
- **Multi-tenancy:** Some hosts partition service blueprints by tenant. The editor does not know about tenants — the host does.
- **Versioning:** Some hosts keep every version of a service blueprint. Some overwrite. The editor does not care — the host decides.
- **Rollback:** Some hosts can roll back to a previous version. The editor does not implement rollback — the host does.

Keeping publishing out of the editor keeps the editor simple and flexible.

---

## 8. Summary

The service blueprint editor and runtime are separated by two boundaries:

1. **Authored ↔ Runtime** — `IServiceBlueprintProjector` converts the rich authored model into the flat runtime model.
2. **Editor ↔ Host** — `ServiceBlueprintSource` gives the editor access to authored service blueprints without coupling to the host's storage, identity, or publishing logic.

The editor describes service blueprints in business terms: stages, gateways, routes, typed actions.

The runtime executes service blueprints in engine terms: states, transitions, action handlers.

The host bridges the two: it stores authored service blueprints, it projects them to runtime definitions, it publishes them when appropriate, and it provides the handlers that execute typed actions.

This architecture keeps the editor domain-agnostic and keeps the runtime flexible.

---

## 3. What the service blueprint JSON should say

The service blueprint JSON should stay declarative. It should not contain callbacks, source code, or app-specific method names.

A simple shape looks like this:

```jsonc
{
  "definitionKey": "planning-application",
  "displayName": "Planning application",
  "initialStageKey": "collect-applicant",
  "stages": [
    {
      "key": "collect-applicant",
      "label": "Applicant details",
      "kind": "form",
      "actor": "public",
      "view": {
        "form": {
          "formDefinitionId": "planning-applicant-details"
        }
      },
      "actions": {
        "onEntry": [
          {
            "type": "forms.load",
            "params": {
              "formDefinitionId": "planning-applicant-details"
            }
          }
        ],
        "onExit": [
          {
            "type": "forms.save",
            "params": {
              "formDefinitionId": "planning-applicant-details"
            }
          }
        ]
      }
    },
    {
      "key": "declaration",
      "label": "Declaration",
      "kind": "review",
      "actor": "public"
    },
    {
      "key": "awaiting-triage",
      "label": "Awaiting triage",
      "kind": "waiting",
      "actor": "system",
      "actions": {
        "onEntry": [
          {
            "type": "case.enqueue",
            "params": {
              "queue": "planning-triage"
            }
          }
        ]
      }
    }
  ],
  "transitions": [
    {
      "from": "collect-applicant",
      "to": "declaration",
      "action": "continue"
    },
    {
      "from": "declaration",
      "to": "awaiting-triage",
      "action": "submit",
      "actions": [
        {
          "type": "forms.submit",
          "params": {
            "formDefinitionId": "planning-applicant-details"
          }
        }
      ]
    }
  ]
}
```

### Rules for this JSON

- **Stages** describe the journey step.
- **Transitions** describe the allowed route out of a stage.
- **Typed actions** describe runtime work.
- Stage assignment is authored through `actor` and `roleGates`; editor-only lane or surface hints must not be stored in the authored file or projected runtime file.
- Every typed action has a stable `type` and serialisable `params`.
- The editor can validate the shape of `params` without knowing how the handler works internally.
- The runtime can execute the action by resolving `type` to a handler.

### Why this matters

This gives us a clean answer to the original design question:

- **Design time** is responsible for listing available action types and their parameter requirements.
- **Runtime** is responsible for executing those actions.
- **Projection** keeps the Umbraco-facing contract clean by carrying authored assignment data forward without leaking editor-only surface metadata.

---

## 4. What the editor needs from the action catalog

The editor needs an action catalog. This is the source of truth for what authors can pick.

Each action catalog entry should include:

- `type` — stable key such as `forms.submit` or `notifications.send-email`
- `label` — short display name for the editor
- `summary` — simple explanation of what the action does
- `appliesTo` — where the action is valid, for example `stage.onEntry`, `stage.onExit`, or `transition`
- `paramsSchema` — typed parameter schema the editor can validate
- `defaultParams` — starter values
- `examples` — example configurations for authors
- `status` — for example `available`, `planned`, or `internal`
- `implementation` — whether the reference business app currently has a handler for it

A simple catalog entry might look like this:

```jsonc
{
  "type": "forms.submit",
  "label": "Submit form",
  "summary": "Validate and persist a forms-engine form.",
  "appliesTo": ["transition"],
  "paramsSchema": {
    "type": "object",
    "required": ["formDefinitionId"],
    "properties": {
      "formDefinitionId": {
        "type": "string"
      }
    }
  },
  "defaultParams": {
    "formDefinitionId": ""
  },
  "status": "available",
  "implementation": "reference-business-app"
}
```

This catalog gives the editor everything it needs to:

- show a picker of available actions
- render the right parameter fields
- validate action configuration before save
- explain which actions are ready now and which are future options

The editor should not hard-code this list in the UI.

---

## 5. How the runtime should work in the reference business app

The reference business app should use a **handler registry**.

The service blueprint engine stays responsible for the generic service blueprint job:

- load the service blueprint
- keep track of the current stage
- check which transitions are allowed
- move the instance to the next stage
- build the runtime response envelope Prism already uses

The business app then handles typed actions through a registry:

```csharp
public interface IServiceBlueprintActionHandler
{
    string ActionType { get; }
    Task<ActionExecutionResult> ExecuteAsync(
        ServiceBlueprintActionDefinition action,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IServiceBlueprintActionRegistry
{
    IServiceBlueprintActionHandler? Resolve(string actionType);
    IReadOnlyList<ActionCatalogEntry> GetCatalog();
}
```

### Runtime flow

1. The engine loads the current stage.
2. The engine finds any stage or transition actions that should run.
3. For each action, the engine asks the registry for the handler that matches `action.type`.
4. The handler reads the typed `params` and the runtime context.
5. The handler performs the business work.
6. The engine continues the service blueprint using the transition result.

This is a better long-term model than embedding app logic directly into the service blueprint engine. It also keeps the editor and runtime aligned because both use the same stable action type keys.

### How this fits the current reference app

Today, the shared runtime already handles the generic service blueprint concerns: definitions, instances, transitions, envelopes, component rendering, and shell inference. The next backend seam is to add action execution beside that runtime, not inside the editor contract.

In other words:

- `ProcessManagerEngine` stays the generic engine
- `BusinessAppProcessManager` stays the host-specific extension point
- a handler registry becomes the host-specific way to execute typed actions

---

## 6. Forms-backed actions now, other actions later

Forms-backed actions fit this model immediately.

For the first iteration, many service blueprint stages are really forms-engine stages. That is fine. We should model them as normal typed actions, not as a separate special case.

Examples:

- `forms.load`
- `forms.save`
- `forms.submit`
- `forms.reset`

Those actions can use parameters such as:

- `formDefinitionId`
- `submissionMode`
- `validationProfile`
- `saveAsDraft`

Later actions use the same contract.

Examples:

- `notifications.send-email`
- `identity-verification.start`
- `identity-verification.poll`
- `case.assign`
- `case.create`

That means the editor model does not need to change when we add email or identity verification later. We only add:

1. a new catalog entry for design time
2. a new handler for runtime

This is the main benefit of typed actions.

---

## 7. How this maps to Prism compatibility

The current Prism runtime still consumes `ServiceBlueprint`.

So the editor-friendly service blueprint is projected into that runtime shape. That projection should stay simple and predictable:

- stages project to runtime states
- transitions project to `RouteFile`
- stage views project to Prism component trees
- shell choice still comes from the existing component-based inference rules
- typed actions stay attached as service blueprint metadata for the business app runtime to execute
- UI-only fields (such as temporary editor surface hints) are stripped before projection, leaving only the authored assignment data (actor, roleGates) that drives runtime behaviour

We should avoid making authors think in low-level Prism terms such as inferred shell metadata unless they are debugging compatibility.

The important compatibility point is simple: authors edit stages, transitions, and typed actions; Prism still receives the runtime definition it already understands.

---

## 8. Recommended responsibility split

### Design time

Design time should own:

- the service blueprint JSON structure
- the list of allowed action types
- the parameter schema for each action type
- validation and editor guidance
- simple examples and implementation status

### Runtime

Runtime should own:

- action handler registration
- action execution
- integration with forms, email, case management, or external services
- runtime context, retries, and failure handling

This is the cleanest way to explain the system:

- the **editor** describes the service blueprint
- the **catalog** describes available actions
- the **engine** runs the service blueprint
- the **handlers** do the work

---

## 9. Summary

The service blueprint should describe the journey in business terms: stages, transitions, and typed actions.

The editor needs a catalog that says which action types exist and what parameters they take. The runtime needs a handler registry that turns those action types into real business behaviour. Forms-backed actions work in this model now, and email or other integrations fit later without changing the core service blueprint shape.
