---
name: "workflow-stage-runtime-projection"
description: "Author workflow journeys as stages and project them into Prism-compatible runtime shells without duplicating low-level state metadata"
domain: "workflow-backend"
confidence: "medium"
source: "observed"
---

## Context

Use this when a workflow editor needs to describe service design concepts like stages, audiences, handoffs, deadlines, assignments, and backstage work, but the existing Prism runtime still expects component-driven steps, transitions, waiting metadata, and stable response envelopes.

## Patterns

- Keep the authored model **stage-centric**:
  - one authored stage describes intent, actors, route, handoffs, deadlines, and audience-specific views
  - do not force authors to hand-author multiple low-level Prism states for the same service moment
- Treat authored `views[]` as audience projections:
  - `public`
  - `member`
  - `business-app`
  - `operator`
- Use a projection layer to emit Prism-compatible runtime states:
  - input-heavy view → `question`
  - summary/review view → `check-answers`
  - waiting view → `defer` + `waiting`
  - read-only progress view → `status-timeline`
  - completion view → `confirmation`
  - task decomposition view → `task-list`
- Keep compatibility by preserving:
  - `definitionKey`
  - `initialState`
  - `instancePolicy`
  - component-authored field semantics
  - transition/action keys
  - `StateVersion`
  - `WorkflowProblem`
  - `WorkflowResponseEnvelope`
- Keep operational case truth outside workflow answers:
  - assignments
  - reviewer notes
  - linked subjects
  - evidence manifests
  - SLA/deadline clocks
  - third-party proofing status

## Examples

- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`
- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs`
- `src/UmbracoPrism.Core/Models/Workflow/WorkflowRenderShellResolver.cs`
- `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json`

## Anti-Patterns

- Requiring authors to duplicate stage intent in both high-level journey metadata and low-level Prism shell metadata.
- Storing assignments, reviewer decisions, or linked-subject facts inside generic workflow answer payloads.
- Treating backstage work as invisible implementation detail when authors need to model handoffs, queues, and deadlines explicitly.
