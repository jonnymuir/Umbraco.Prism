---
name: "workflow-editor-preview-quality-gate"
description: "Minimum honest validation for the workflow editor preview-edited-stage slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #67 quality gate)"
---

## Context

Use this when validating a workflow-editor slice that previews the currently selected authored stage in runtime terms. This feature crosses the authored model, projector/runtime-shell inference, editor chrome, and live app wiring, so surrounding UI health is necessary but not sufficient.

## Minimum Gate

1. `dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
2. `cd src/UmbracoPrism.Client && npm run build`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-preview.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Workflow authoring .NET tests** protect the deterministic projection and shell-inference seam the preview depends on.
- **Client build** catches editor contract drift before runtime-only checks hide it.
- **Storybook CI across browsers with axe** proves the preview chrome and its selectors stay rendered and accessible.
- **Workflow graph/stage-selection Playwright** proves the selected-stage handoff into the preview starts from a real author interaction.
- **Dedicated preview Playwright** is the only honest place to prove auto-refresh, view switching, read-only behaviour, slow-preview loading, and planning-stage rendering.
- **Live planning smoke** proves the shell, authoring API, and planning fixture still compose in the real app.

## Acceptance Audit Heuristics

- Do not credit the slice if the editor only shows inspector data or proposal-preview data; #67 is about selected-stage runtime preview.
- Treat “renders using forms engine” as incomplete unless the preview surface shows projected runtime content for planning stages, not just action metadata such as `forms.load`.
- Treat public/member/back-stage as incomplete unless authors can explicitly switch the previewed surface.
- Treat read-only as incomplete unless user interaction is blocked in the preview contract.
- Treat the slice as unshipped if there is no dedicated `workflow-editor-preview` behavioural contract.

## Anti-Patterns

- Calling #67 green because build, Storybook, and planning smoke pass while no preview panel exists
- Reusing proposal preview evidence for stage preview acceptance
- Treating the current workflow load banner as proof of slow preview loading
