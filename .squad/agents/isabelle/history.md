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

### 2026-05-18T13:17:12.103+01:00 — Issue #65 workflow validation and error reporting slice

- **Validation boundary:** treat orphaned and unreachable stages as blocking editor errors; keep dead ends and action-parameter gaps as workflow-friendly warnings so save stays available while authors finish detailed configuration.
- **Save seam:** use `POST /api/workflow-authoring/workflows/{key}/publish` for the host Save button until a dedicated authored-workflow save endpoint exists; the client labels it as Save, but the current persistence boundary is publish-backed.
- **Accessibility pattern:** make the validation rail a button-based jump list, preserve inline inspector errors, and move focus to the affected stage or action field when an author opens an issue from the rail.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-graph-keyboard.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts tests/workflow-editor/workflow-editor-copy-paste.spec.ts tests/workflow-editor/workflow-editor-validation.spec.ts --workers=1 --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the validation/error-reporting changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/workflow-validation.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-validation.spec.ts`.

## 2026-05-18T13:17:12Z — Issue #65 validation and error reporting completed

Delivered shared workflow validation infrastructure:
- Single validation pass in `prism-workflow-editor` serving rail, save state, and jump-to-item behaviour
- Error classification: blocking errors (orphaned/unreachable stages) vs. warnings (dead-end reminders, parameter issues)
- Validation rail button-driven with jump-to-item links for accessibility
- Inline inspector field errors tied to validation
- Save blocking for critical structural problems
- Focused behavioural contract covers validation rail, plain-language messages, and save blocking

**Status:** Acceptance-complete per Tangy's seven-seam gate. Ready for production.

### 2026-05-18T13:17:12.103+01:00 — Issue #66 help system and shortcut reference slice

- **Shortcut source of truth:** define workflow-editor commands in `src/UmbracoPrism.Client/src/workflow-editor/workflow-shortcuts.ts` and drive the toolbar `aria-keyshortcuts`, help modal content, and Playwright parity checks from that shared map so discoverability does not drift from implementation.
- **Accessibility pattern:** expose help as a host-owned modal opened by both the toolbar Help button and `F1`, trap focus while it is open, restore focus to the invoking control on close, and keep inline help on complex inspector/action-editor fields reachable by hover and keyboard focus.
- **Empty-state guidance:** when the workflow has no stages, replace the generic “nothing to display” message with actionable getting-started tips plus first-stage buttons inside `prism-workflow-graph` so authors can recover without guessing the next step.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-help.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts tests/workflow-editor/workflow-editor-copy-paste.spec.ts tests/workflow-editor/workflow-editor-validation.spec.ts tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-graph-keyboard.spec.ts --workers=1 --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the help/discoverability changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-inline-help.ts`, `src/UmbracoPrism.Client/src/workflow-editor/workflow-shortcuts.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-help.spec.ts`.

## 2026-05-18T12:17:12Z — Issue #66 help and shortcut discoverability completed

Delivered help and shortcut discoverability as host-editor responsibility:
- Shared shortcut catalog at `src/UmbracoPrism.Client/src/workflow-editor/workflow-shortcuts.ts` drives toolbar affordances, help modal, and parity tests
- Help button visible on toolbar; `F1` opens shortcut reference modal with focus trap and restore
- Inline help on complex inspector fields reachable by hover and keyboard focus
- Empty-state shows getting-started tips with action buttons instead of generic "nothing to display"
- Comprehensive Playwright coverage ensures keyboard paths and empty-state recovery work end-to-end

**Status:** Acceptance-complete per Tangy's six-seam gate. Production-ready.
