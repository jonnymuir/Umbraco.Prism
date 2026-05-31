# 02 — Runtime Model and Workflow Actions

**Date:** 2026-05-16  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented  
**Relates to:** `docs/design/workflow-editor-v1/README.md`

---

## 1. Overview

This document explains the workflow engine architecture and the projection boundary between Prism's service-design domain and the host's business domain.

The editor owns the **design-time workflow** (the `AuthoredWorkflow` model). The runtime owns **execution** (the `WorkflowDefinitionFile` model that drives the workflow engine). To maintain compatibility with existing runtimes, the Prism projector (`IWorkflowProjector`) transforms the richer authored model into the runtime shape that the engine already understands.

This document explains:

1. the **workflow definition** the editor saves (`AuthoredWorkflow`)
2. the **action catalog** the editor reads (`WorkflowActionCatalog`)
3. the **workflow engine** that executes the definition (Prism runtime)
4. the **action handlers** that perform business work (host-specific)
5. the **projection** from authored to runtime model (`IWorkflowProjector`)
6. the **publishing** boundary (host concern, not editor concern)

---

## 2. The Two Models

### Authored Model (`AuthoredWorkflow`)

This is the service-design model. It lives in `UmbracoPrism.Core` (C#) and `UmbracoPrism.Client` (TypeScript). The editor reads and writes this model.

Key fields:

- `definitionKey` — stable identifier for the workflow
- `displayName` — human-readable name
- `initialStageKey` — where the workflow starts
- `stages[]` — the steps in the journey (each has `key`, `label`, `kind`, `actor`, `view`, `actions`)
- `gateways[]` — routing points (each has `key`, `title`, `kind` [Split/Join], `source?`, `routes[]`)

### Runtime Model (`WorkflowDefinitionFile`)

This is the execution model. It lives in `UmbracoPrism.Core`. The runtime engine reads this model.

Key fields:

- `key` — stable identifier
- `initialState` — where the workflow starts
- `states[]` — the runtime states (projected from stages)
- `transitions[]` — the runtime transitions (projected from gateway routes)
- `components[]` — the UI components for each state

The runtime model is **flatter** and **simpler** than the authored model. It is optimized for execution, not authoring.

---

## 3. The Projection Boundary

The `IWorkflowProjector` interface (in `UmbracoPrism.Core`) is the Prism API for converting authored workflows into runtime definitions.

### Interface

```csharp
public interface IWorkflowProjector
{
    /// <summary>
    /// Projects an authored workflow into the runtime definition shape.
    /// Returns a validation result with the projected definition or diagnostic messages.
    /// </summary>
    WorkflowProjectionResult Project(AuthoredWorkflow authoredWorkflow);
}
```

The projector:

- Validates the authored structure (schema + structural lint rules).
- Converts `stages[]` + `gateways[]` into `states[]` + `transitions[]`.
- Strips editor-only metadata (e.g., canvas layout hints).
- Preserves authored assignment data (`actor`, `roleGates`) for runtime authorization.
- Returns diagnostics if the workflow cannot be projected (e.g., missing stage, orphan gateway).

### Projection Rules

| Authored | Runtime |
|----------|---------|
| `AuthoredStage` | `WorkflowState` |
| `AuthoredGateway.routes[]` | `WorkflowTransition[]` (one per route, `from = gateway.source`, `to = route.target`) |
| `AuthoredRoute.trigger` | `WorkflowTransition.action` |
| `AuthoredRoute.condition` | `WorkflowTransition.condition` |
| `AuthoredRoute.requiresRole` | `WorkflowTransition.requiresRole` |
| `AuthoredRoute.actions` | `WorkflowTransition.actions` (typed actions for handlers) |

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

The editor validates the `params` shape against the action's schema (provided by the `WorkflowActionCatalog`). The runtime resolves the `type` to a handler and executes the action.

This split keeps workflow definitions **declarative**. No callbacks, no source code, no app-specific method names. Just stable keys and serializable params.

---

## 5. Action Catalog

The `WorkflowActionCatalog` interface (in `UmbracoPrism.Client`) is the boundary contract for listing available actions at design time.

```typescript
export interface WorkflowActionCatalog {
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

Prism ships `BuiltInWorkflowActionCatalog` with generic actions (Send Email, Assign Case, etc.). Hosts extend it to add domain-specific actions.

---

## 6. Runtime Execution (Handler Registry Pattern)

The runtime engine (in `UmbracoPrism.WorkflowRuntime`) is **generic**. It:

- Loads the workflow definition (`WorkflowDefinitionFile`).
- Tracks the current state for each instance.
- Checks which transitions are allowed.
- Moves the instance to the next state.
- Builds the runtime response envelope.

The engine does **not** execute typed actions itself. It delegates to a handler registry.

### Recommended Handler Interface

```csharp
public interface IWorkflowActionHandler
{
    string ActionType { get; }
    Task<ActionExecutionResult> ExecuteAsync(
        WorkflowActionDefinition action,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IWorkflowActionRegistry
{
    IWorkflowActionHandler? Resolve(string actionType);
    IReadOnlyList<ActionCatalogEntry> GetCatalog();
}
```

### Runtime Flow

1. The engine loads the current state.
2. The engine finds any stage or transition actions that should run.
3. For each action, the engine asks the registry for the handler that matches `action.type`.
4. The handler reads the typed `params` and the runtime context.
5. The handler performs the business work (send email, assign case, etc.).
6. The engine continues the workflow using the transition result.

This pattern keeps action execution **out of the engine**. The engine stays generic. The host provides the handlers.

---

## 7. Publishing — A Host Concern, Not an Editor Concern

**Publishing** is the act of snapshotting an authored workflow into a runtime store so that the runtime engine can load it and execute instances.

This is a **host concern**, not an editor concern. The editor never publishes workflows itself. The editor only saves `AuthoredWorkflow` objects through the host's `WorkflowSource`.

The host decides:

- **When** to publish (on save? on explicit publish button? on approval by a reviewer?).
- **Where** to publish (database? blob storage? in-memory cache?).
- **How** to version (overwrite? keep history? snapshot to a new key?).
- **Who** can publish (enforce authorization in the host's publish endpoint, not in the editor).

### Example Flow (MockBusinessApp)

MockBusinessApp demonstrates the pattern:

1. The editor saves an `AuthoredWorkflow` via `PUT /mockapp/workflows/{key}`.
2. MockBusinessApp stores the authored workflow in memory (its `ReferenceAuthoredWorkflowStore`).
3. Separately, MockBusinessApp has a `WorkflowPublishService` (in `MockBusinessApp/Services/Publishing/`).
4. When the host calls `publishService.PublishAsync(workflowKey)`, the service:
   - Loads the authored workflow from the store.
   - Calls `IWorkflowProjector.Project(authoredWorkflow)` to get the runtime definition.
   - Validates the projection result.
   - Saves the runtime definition to the published store (`IPublishedWorkflowStore`).
5. The runtime engine (in `UmbracoPrism.WorkflowRuntime`) loads definitions from the published store, never from the authored store.

The editor has no opinion about steps 3-5. Those are host concerns.

### Why This Boundary Matters

Different hosts have different publishing needs:

- **Approval workflow:** Some hosts require a reviewer to approve a workflow before it goes live. The editor does not enforce this — the host does.
- **Multi-tenancy:** Some hosts partition workflows by tenant. The editor does not know about tenants — the host does.
- **Versioning:** Some hosts keep every version of a workflow. Some overwrite. The editor does not care — the host decides.
- **Rollback:** Some hosts can roll back to a previous version. The editor does not implement rollback — the host does.

Keeping publishing out of the editor keeps the editor simple and flexible.

---

## 8. Summary

The workflow editor and runtime are separated by two boundaries:

1. **Authored ↔ Runtime** — `IWorkflowProjector` converts the rich authored model into the flat runtime model.
2. **Editor ↔ Host** — `WorkflowSource` gives the editor access to authored workflows without coupling to the host's storage, identity, or publishing logic.

The editor describes workflows in business terms: stages, gateways, routes, typed actions.

The runtime executes workflows in engine terms: states, transitions, action handlers.

The host bridges the two: it stores authored workflows, it projects them to runtime definitions, it publishes them when appropriate, and it provides the handlers that execute typed actions.

This architecture keeps the editor domain-agnostic and keeps the runtime flexible.

---

## 3. What the workflow JSON should say

The workflow JSON should stay declarative. It should not contain callbacks, source code, or app-specific method names.

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

The workflow engine stays responsible for the generic workflow job:

- load the workflow definition
- keep track of the current stage
- check which transitions are allowed
- move the instance to the next stage
- build the runtime response envelope Prism already uses

The business app then handles typed actions through a registry:

```csharp
public interface IWorkflowActionHandler
{
    string ActionType { get; }
    Task<ActionExecutionResult> ExecuteAsync(
        WorkflowActionDefinition action,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IWorkflowActionRegistry
{
    IWorkflowActionHandler? Resolve(string actionType);
    IReadOnlyList<ActionCatalogEntry> GetCatalog();
}
```

### Runtime flow

1. The engine loads the current stage.
2. The engine finds any stage or transition actions that should run.
3. For each action, the engine asks the registry for the handler that matches `action.type`.
4. The handler reads the typed `params` and the runtime context.
5. The handler performs the business work.
6. The engine continues the workflow using the transition result.

This is a better long-term model than embedding app logic directly into the workflow engine. It also keeps the editor and runtime aligned because both use the same stable action type keys.

### How this fits the current reference app

Today, the shared runtime already handles the generic workflow concerns: definitions, instances, transitions, envelopes, component rendering, and shell inference. The next backend seam is to add action execution beside that runtime, not inside the editor contract.

In other words:

- `WorkflowRuntimeEngine` stays the generic engine
- `BusinessAppWorkflowEngine` stays the host-specific extension point
- a handler registry becomes the host-specific way to execute typed actions

---

## 6. Forms-backed actions now, other actions later

Forms-backed actions fit this model immediately.

For the first iteration, many workflow stages are really forms-engine stages. That is fine. We should model them as normal typed actions, not as a separate special case.

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

The current Prism runtime still consumes `WorkflowDefinitionFile`.

So the editor-friendly workflow definition is projected into that runtime shape. That projection should stay simple and predictable:

- stages project to runtime states
- transitions project to `WorkflowTransitionFile`
- stage views project to Prism component trees
- shell choice still comes from the existing component-based inference rules
- typed actions stay attached as workflow metadata for the business app runtime to execute
- UI-only fields (such as temporary editor surface hints) are stripped before projection, leaving only the authored assignment data (actor, roleGates) that drives runtime behaviour

We should avoid making authors think in low-level Prism terms such as inferred shell metadata unless they are debugging compatibility.

The important compatibility point is simple: authors edit stages, transitions, and typed actions; Prism still receives the runtime definition it already understands.

---

## 8. Recommended responsibility split

### Design time

Design time should own:

- the workflow JSON structure
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

- the **editor** describes the workflow
- the **catalog** describes available actions
- the **engine** runs the workflow
- the **handlers** do the work

---

## 9. Summary

The workflow definition should describe the journey in business terms: stages, transitions, and typed actions.

The editor needs a catalog that says which action types exist and what parameters they take. The runtime needs a handler registry that turns those action types into real business behaviour. Forms-backed actions work in this model now, and email or other integrations fit later without changing the core workflow shape.
