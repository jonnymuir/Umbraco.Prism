---
name: "workflow-transition-editor-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor transition creation/editing slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #61 quality gate)"
---

## Context

Use this when validating workflow-editor work that lets authors create, edit, retarget, validate, and delete transitions. The slice spans graph gestures, list affordances, inspector editing, authored-workflow validation, and live-shell wiring, so no single layer is enough.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd /Users/jonnymuir/Documents/Projects/Umbraco.Prism && dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-transition-editor.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript contract drift in the shared authored workflow and editor shell.
- **Authoring .NET tests** protect transition serialization, condition metadata, and validation diagnostics.
- **Storybook CI** proves stories still render and remain WCAG-clean.
- **Graph keyboard coverage** keeps baseline selection/context-menu accessibility honest.
- **Dedicated transition-editor Playwright coverage** is where creation, retargeting, guard editing, delete, keyboard-only flows, and post-edit connectivity assertions should live.
- **Planning smoke** proves the real host shell and authored seed still load in the live app.

## Acceptance Audit Heuristics

- Do not credit “drag creates transition with label prompt” if the graph silently creates a default transition label and skips an explicit prompt.
- Do not credit list-view creation unless authors can create a transition from list mode, not just inspect outbound counts.
- Treat the inspector requirement as incomplete until source, target, label, and condition or guard data are editable from the inspector itself.
- Treat the TypeScript client as incomplete if it collapses structured `conditions` to a single legacy string or drops transition actions needed by the editor.
- Do not credit validation until unreachable-stage warnings surface through the authoring validation path, not just per-stage “no outbound transition” hints.
- Require a focused spec to assert graph connectivity after create, retarget, and delete operations.

## Anti-Patterns

- Calling the slice green because stage-editor tests still pass
- Counting transition rendering or selection as equivalent to transition editing
- Treating delete support alone as full transition coverage
- Accepting pointer-only drag creation when the keyboard path for equivalent authoring is missing
