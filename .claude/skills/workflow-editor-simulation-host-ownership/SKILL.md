---
name: "workflow-editor-simulation-host-ownership"
description: "Keep workflow path simulation state in the host editor so graph, validation, and a11y feedback stay aligned"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #68)"
---

## Context

Use this when a workflow editor needs a lightweight path simulation panel that highlights a route in the graph and explains why some authored transitions cannot be followed yet.

## Pattern

1. Keep simulation state in the host editor alongside the authored workflow and shared validation results.
2. Start from `initialStageKey` and record a breadcrumb history of entered stages plus the chosen transition labels.
3. Pass only highlight props into the graph (`currentStageKey`, stage path, transition path); the graph should not infer or mutate simulation state itself.
4. Stop automatically at waiting stages, terminal stages, or dead ends.
5. Disable transition buttons only for route-specific **blocking** validation issues; show conditions and role guards as copy, not as fake runtime evaluation.
6. Reset simulation when the authored workflow changes so the highlighted path never drifts from the edited model.

## Why this works

- The host editor already owns workflow edits, validation, and selection, so simulation can reuse the same source of truth without duplicating state.
- Graph highlighting stays simple and deterministic when it consumes path props instead of inventing its own state machine.
- This keeps the simulation honest: authors see useful confidence signals without mistaking the panel for a full runtime executor.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-simulation.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-simulation.spec.ts`

## Anti-Patterns

- Letting the graph own simulation history or choose the next route
- Executing guard expressions in the browser as if the editor were the runtime engine
- Blocking the whole simulation because of unrelated warnings elsewhere in the workflow
- Leaving the old simulated path visible after the author edits stages or transitions
