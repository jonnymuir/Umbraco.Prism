---
name: "workflow-action-handler-registry"
description: "Model workflow runtime actions as declarative type+params references backed by a discoverable handler registry"
domain: "workflow-backend"
confidence: "medium"
source: "observed"
---

## Context

Use this when a workflow editor needs to let authors configure runtime actions safely while a business application remains responsible for executing them.

## Patterns

- Keep workflow JSON **declarative**:
  - authored action = stable `type` key + serialisable `params`
  - never embed callbacks, source code, or executable lambda bodies in JSON
- Separate **user transitions** from **runtime actions**:
  - transition verbs like `continue`, `submit`, `approve` remain graph/navigation concepts
  - runtime actions are side effects or integrations attached to stage entry / transition exit
- Use one **handler registry** for both discovery and execution:
  - editor asks registry for available action types and parameter schemas
  - runtime resolves the same action type keys to typed C# handlers
- Prefer typed parameter records plus a shared schema/descriptor export:
  - display name
  - summary
  - applicability rules
  - defaults/examples
  - JSON schema for params
  - outcome shape (`sync`, `deferred`, outputs)
- Let the business app own handler implementation while the generic engine owns orchestration.
- If you want callback ergonomics in the reference app, make lambdas an internal adapter over the registry, not the authoring contract.
- Treat forms-engine-backed actions and future integrations (email, ID&V, case creation) as peers in the same registry model.

## Examples

- `src/UmbracoPrism.WorkflowRuntime/Services/WorkflowRuntimeEngine.cs` — current runtime orchestration boundary
- `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs` — host-specific extension over generic runtime
- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs` — stable runtime response contract
- `docs/design/workflow-forms-engine-backend.md` — current business-app ownership of workflow behaviour
- `docs/design/workflow-editor-v1/02-runtime-projection.md` and `04-agentic-surfaces.md` — authored/projection/agent split

## Anti-Patterns

- Putting callback names or executable lambda logic directly in workflow JSON
- Making the editor learn available actions from hand-maintained docs instead of the runtime registry
- Hard-coding every new action type into the engine core with `switch` statements
- Treating forms actions as a one-off special case that bypasses the main action contract
