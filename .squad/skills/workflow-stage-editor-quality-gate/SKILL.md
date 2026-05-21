---
name: "workflow-stage-editor-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor stage creation/editing slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #60 quality gate)"
---

## Context

Use this when validating workflow-editor work that lets authors create, edit, configure, and delete stages. The slice crosses graph/list workspace behaviour, inspector editing, action-catalog discovery, and authored-workflow validation, so no single test layer is enough.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd /Users/jonnymuir/Documents/Projects/Umbraco.Prism && dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-stage-editor.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript contract drift in the shared workflow model and editor shell.
- **Authoring .NET tests** protect the action-catalog and workflow-validation seams the UI depends on.
- **Storybook CI** proves stories still render and remain WCAG-clean.
- **Graph/list keyboard coverage** protects the selection and inspector-entry affordances inherited from issues #58 and #59.
- **Dedicated stage-editor Playwright coverage** is where dialog validation, delete confirmation, action add/reorder flows, and keyboard-only editing should live.
- **Planning smoke** proves the real host shell still wires graph/list selection into the inspector and authoring API.

## Acceptance Audit Heuristics

- Do not credit “create stage dialog” if add-stage buttons insert a canned template immediately.
- Do not credit the inspector requirement unless the selected stage can edit title, description, actor, and type from the inspector itself.
- Treat the slice as incomplete if the TypeScript workflow model or API client drops `description` or `actions` even when the C# authored model exposes them.
- Require client-side consumption of `/api/workflow-authoring/action-catalog` before crediting “lists available from catalog”.
- Do not credit delete acceptance unless the confirmation names affected transitions before removal.
- Treat keyboard acceptance as incomplete until create, edit, action reorder, and delete-confirm flows all work without pointer-only affordances.

## Anti-Patterns

- Calling the issue green because graph/list selection works while the inspector is still read-only
- Treating inline table edits as a substitute for the requested inspector editing surface
- Counting action badges or counts as action editing
- Accepting immediate delete as equivalent to explicit confirmation
