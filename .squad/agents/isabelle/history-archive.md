# Isabelle — History Archive

Summarized entries from earlier periods preserved for reference.

## Pre-2026-05-16 Summary

**Project foundation (2026-03 through 2026-05-14):**
- Component system migration completed
- GDS integration and accessibility patterns established
- SEC-005 workflow shell security closed
- V1 workflow editor UX requirements refined through design cycle
- Reference editor shell v1 hosted

**Key learnings consolidated:**
- Dual-mode graph navigation (visual + linear) is the accessibility pattern for graph canvases
- Conversation pane is the primary agent surface
- Explicit save (not autosave) is safe baseline
- Split authoring surface (library → editor → validation) not JSON-first
- axe-core shadow DOM quirks: no `<header>` in shadow DOM, every overflow region needs tabindex, `role="alert"` breaks `<ul>` structure
- Mock drafters belong next to components, not shared test folders
- Story page state can bleed; reset at play() start

---

**Archive created:** 2026-05-18 for history summarization gate

## Archived Entries

---
date: 2026-05-18T12:17:12Z
summary: "Issue #63 undo/redo implementation — host-owned stack with UI, keyboard, selection restore"
entries_archived: 8
---

Frontend Dev Isabelle completed comprehensive undo/redo support for the workflow editor, including 50-step history stack, toolbar integration, keyboard shortcuts, selection restoration across undo/redo operations, and Playwright acceptance coverage for stage, transition, action, reorder, and parameter-change scenarios.

---

### 2026-05-18T13:17:12.103+01:00 — Issue #63 undo and redo workflow changes slice

- **Undo/redo ownership:** keep the bounded history stack in `prism-workflow-editor` so every structural graph mutation and every inspector-driven stage/transition/action edit is captured through the shared `workflow-updated` event seam instead of duplicating history logic in child components.
- **Accessibility pattern:** expose undo/redo as real toolbar buttons with disabled states, `aria-keyshortcuts`, a visible history status bar, and a polite live announcement so keyboard and screen-reader users get the same recovery feedback as pointer users.
- **History boundary:** preview/reject flows and validation surfaces must not clear local undo history; only loading a fresh workflow should reset the stack, and the editor should cap retained snapshots to the latest 50 changes.
- **Validation gate for this slice:** `npm run build`, `dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts tests/workflow-editor/workflow-transition-editor.spec.ts tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the undo/redo changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-history.spec.ts`.


Implemented host-owned undo/redo stack with 50-step cap, toolbar UI, keyboard shortcuts (Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z), selection restore, and comprehensive Playwright coverage for stage, transition, action, reorder, and parameter-change flows.

### 2026-05-18T13:17:12.103+01:00 — Issue #64 copy and paste stages and actions slice

- **Clipboard ownership:** keep copy/paste state, toolbar affordances, and `Ctrl/Cmd+C` / `Ctrl/Cmd+V` handling in `prism-workflow-editor` so stage workspace selection and inspector action selection share one accessible clipboard contract.
- **Copy boundary:** stage copy duplicates authored stage properties, fields, waits, and actions but intentionally excludes inbound/outbound transitions; pasted stages rely on existing workspace/inspector validation to surface missing routes immediately.
- **Action paste rule:** pasted actions keep all params, but timing must be normalised against the destination context (`stage.onEntry`, `stage.onExit`, or `transition`) so the same copied action can move safely between stages and other action targets.
- **Accessibility pattern:** expose clipboard state in the toolbar with visible copy/paste buttons, `aria-keyshortcuts`, and selection highlighting in `prism-workflow-action-editor` so keyboard users know what will paste before they invoke it.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, and `node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts tests/workflow-editor/workflow-editor-copy-paste.spec.ts --reporter=line` all passed after the copy/paste changes; the attempted authoring `.NET` test run was blocked by an existing `UmbracoPrism.TestSite` missing `govuk-frontend.min.css` asset.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-action-editor.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-copy-paste.spec.ts`.

## 2026-05-18T12:17:12Z — Issue #64 completed

Delivered complete copy/paste functionality with:
- Host-owned clipboard state in prism-workflow-editor
- Toolbar copy/paste buttons and Ctrl/Cmd+C / Ctrl/Cmd+V shortcuts
- Safe stage duplication (fresh keys, no transitions)
- Action duplication with destination-aware normalization
- Immediate post-paste selection for accessibility
- Comprehensive Playwright coverage for graph, action editor, and toolbar behavior


## Learnings
