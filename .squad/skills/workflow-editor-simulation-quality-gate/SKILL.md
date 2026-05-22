---
name: "workflow-editor-simulation-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor path-simulation slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #68 quality gate)"
---

## Context

Use this when validating workflow-editor work that lets authors simulate a workflow path from the initial stage, choose transitions, inspect blockers, and build confidence in branch outcomes without leaving the editor. The slice crosses authored-workflow routing semantics, graph/list state, validation rules, waiting/end-stage behaviour, and the live planning shell, so no single layer is enough to call it green honestly.

## Minimum Gate

1. `dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
2. `cd src/UmbracoPrism.Client && npm run build`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-validation.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-simulation.spec.ts --reporter=line`
7. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Workflow authoring .NET tests** protect initial-stage, transition-routing, and waiting-stage semantics the simulator must trust.
- **Client build** catches TypeScript state drift between the host editor, graph, simulation panel, and validation wiring.
- **Storybook CI across browsers with axe** proves the simulation panel and graph affordances still render accessibly.
- **Workflow graph keyboard coverage** protects the graph selection and focus contract that simulation highlighting builds on.
- **Workflow validation coverage** protects the blocker language and issue wiring that simulation should surface before letting authors advance.
- **Dedicated simulation coverage** is where acceptance actually lives: starts from the initial stage, lists transition labels, advances on click, accumulates breadcrumb/history, stops at waiting or end stages, blocks invalid moves, and highlights the traversed path for happy and rejection flows.
- **Planning smoke** proves the real editor shell, authoring API, and planning fixture still compose after simulation wiring lands.

## Acceptance Audit Heuristics

- Do not credit “starts at the beginning” if the simulator starts from the currently selected stage instead of the authored initial stage.
- Do not credit “available transitions listed” if the UI only shows target stage keys or inspector metadata; the transition labels authors use must be visible.
- Do not credit “simulation history” if state changes only appear in toasts or transient status text; there should be a persistent breadcrumb or step trail.
- Do not credit “path highlighted in graph” unless the current stage and the traversed path are both visibly flagged in the graph and asserted by the dedicated contract.
- Waiting and terminal stages must stop advancement cleanly and explain why no more transitions are available.
- Validation blockers must come from real workflow validation or guard data, not a hard-coded demo banner detached from the authored model.
- Cover at least one happy path and one rejection/backtrack path on the planning workflow fixture.

## Anti-Patterns

- Calling the slice green because stage preview exists while no simulation surface is rendered
- Treating graph selection styling as equivalent to traversed-path highlighting
- Relying on manual editor clicks without a dedicated workflow simulation behavioural contract
- Counting transition-editor coverage as proof that runtime path execution is simulated
