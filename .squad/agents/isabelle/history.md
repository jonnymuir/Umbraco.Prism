# History: Isabelle (Frontend Dev)


### Quality Gate

All six gates pass:
1. ✅ Authoring-focused .NET workflow tests
2. ✅ Client build
3. ✅ Storybook CI across browsers with axe
4. ✅ Existing workflow graph/list keyboard contract
5. ✅ Dedicated Playwright stage-editor behavioural contract
6. ✅ Live planning workflow smoke

**Status:** Green and acceptance-complete per Tangy's recheck.

### 2026-05-18T13:17:12.103+01:00 — Issue #63 undo and redo workflow changes slice

- **Undo/redo ownership:** keep the bounded history stack in `prism-workflow-editor` so every structural graph mutation and every inspector-driven stage/transition/action edit is captured through the shared `workflow-updated` event seam instead of duplicating history logic in child components.
- **Accessibility pattern:** expose undo/redo as real toolbar buttons with disabled states, `aria-keyshortcuts`, a visible history status bar, and a polite live announcement so keyboard and screen-reader users get the same recovery feedback as pointer users.
- **History boundary:** preview/reject flows and validation surfaces must not clear local undo history; only loading a fresh workflow should reset the stack, and the editor should cap retained snapshots to the latest 50 changes.
- **Validation gate for this slice:** `npm run build`, `dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts tests/workflow-editor/workflow-transition-editor.spec.ts tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the undo/redo changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-history.spec.ts`.

## 2026-05-18T12:17:12Z — Issue #63 completed

Implemented host-owned undo/redo stack with 50-step cap, toolbar UI, keyboard shortcuts (Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z), selection restore, and comprehensive Playwright coverage for stage, transition, action, reorder, and parameter-change flows.
