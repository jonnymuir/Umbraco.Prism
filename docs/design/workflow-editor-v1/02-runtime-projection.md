# 02 — Runtime model and workflow actions

**Date:** 2026-05-16  
**Author:** Blathers (Backend Dev)  
**Status:** Proposed  
**Relates to:** `docs/design/workflow-editor-v1/README.md`

---

## 1. Overview

This document explains the workflow engine in simple terms.

The editor owns the **design-time workflow**. The runtime owns **execution**. To keep Prism compatible, the editor saves a richer workflow document and then projects it into the existing `WorkflowDefinitionFile` shape that the current runtime already knows how to load.

Use that compatibility seam when you need to talk about projection. For most of this document, it is clearer to talk about:

1. the **workflow definition** the editor saves
2. the **action catalog** the editor reads
3. the **workflow engine** that executes the definition
4. the **action handlers** that perform business work

---

## 2. The workflow model in plain terms

A workflow definition should describe three things:

- **stages** — the steps in the journey
- **transitions** — how the workflow moves from one stage to another
- **typed actions** — the business work that runs at a stage or during a transition

The key point is that movement through the graph and business side effects are not the same thing.

- A transition action such as `continue`, `submit`, `approve`, or `request-more-info` tells the engine **where to go next**.
- A typed runtime action such as `forms.submit`, `case.assign`, or `notifications.send-email` tells the business app **what to do**.

That split keeps the workflow JSON simple and keeps implementation details out of the definition.

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
