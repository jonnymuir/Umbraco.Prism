# Isabelle — History

Frontend Dev specializing in workflow editor UX and component system.

**Current Focus:**
- Issue #58: Graph workspace slice for workflow authoring editor
- Workflow editor V1 component architecture
- Accessibility and WCAG 2.2 AA compliance

**Latest:** Issue #58 graph workspace acceptance-ready (2026-05-18)

## Recent Work

### 2026-05-17: Workflow Editor Asset Extraction

**Scope:** Frontend asset wiring for workflow editor library extraction  
**Outcome:** Split Vite build to route workflow-editor assets to new library while preserving Core dependencies  

**Key Learning:** Split build is safer than monolithic outDir change. Dual Vite configs scale well (~95ms overhead). Accessibility validation (30 test suites, 282 tests, 3 browsers) gates asset refactors.

### 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with architecture, UX, runtime, integration, and agentic surfaces  

**Contributions:** Authoring UX — four editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory, workflow-native editing model, forms-backed action configuration.

**Key Decisions:**
- Minimum great V1 = structural authoring (not JSON editing)
- Graph/list duality for orientation
- Focused inspector for detail editing
- Persistent conversation/proposal surface
- Preview/simulation surface
- Explicit save with undo/redo

### 2026-05-16: Editor Host Page V1 Implementation

**Scope:** Composed host page from four V1 components, wired to HTTP API, tested WCAG 2.2 AA  

**Artifacts:** `src/workflow-editor/fixtures/`, `workflow-authoring-client.ts`, `workflow-authoring-mock-drafter.ts`, `prism-workflow-editor.ts`, `workflow-editor.html`

**Key Learnings:**
- axe-core shadow DOM quirks: no `<header>` in shadow DOM, every overflow region needs tabindex, `role="alert"` breaks `<ul>` structure
- Stale Storybook: kill prior process on port 6006 before running tests
- Story page state bleed: reset at play() start
- Mock drafters belong next to components

### 2026-05-18: Issue #58 Graph Workspace Completion

**Scope:** Frontend graph workspace slice  
**Outcome:** Two-lane workflow graph surface with routed transition chips, stage/transition selection, context actions, drag-to-create transitions, zoom/fit controls, inspector handoff, and Storybook/Playwright coverage  

**Key Decisions:**
- **Graph owns local structural interactions** — Stage add/delete, transition creation, selection, zoom, fit-to-screen, context-menu affordances
- **Inspector is the edit surface** — Double-click or keyboard inspection moves focus into inspector
- **Front-stage/back-stage inference** — Client accepts optional `editorSurface` hint but defaults to lane inference
- **Keyboard parity mandatory** — All core interactions remain keyboard-accessible

**Status:** ✅ Implementation complete, handed off to Tangy for visual regression quality gate

---

**Earlier learnings and archived entries:** See history-archive.md

## Learnings

### 2026-05-18T13:17:12.103+01:00 — Issue #59 list/table workspace slice

- **List workspace stays on the shared authored model:** the table edits the same `workflow.stages` array as graph mode, including stage-key renames that also retarget `initialStageKey` and transition `fromStage`/`toStage` references.
- **Interaction boundary:** the list owns compact structural editing only (filters, inline key/title/actor/type edits, add/insert/delete, reorder, row selection); the inspector remains the detailed edit surface and row activation should move focus there.
- **Accessibility pattern:** use a semantic table with a dedicated row-focus trigger, Alt+ArrowUp/Alt+ArrowDown keyboard reorder, drag-handle parity, and polite live-region announcements so screen-reader users get the same structural editing feedback as pointer users.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the list workspace changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.stories.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts`, `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`.

## 2026-05-18: Issue #59 List Workspace Completed

**Outcome:** ✅ Acceptance-complete  

Turned list mode into a real accessible table workspace for the workflow editor:
- Semantic `<table>` with stage rows
- Inline editing for key/title/actor/type
- Front/back-stage filters
- Add/insert/delete/reorder controls  
- Keyboard navigation and live announcements
- Row activation opens the inspector
- Storybook and Playwright coverage

**Key decisions:**
- List and graph share one authored workflow model (no separate editing model)
- List workspace owns compact structural edits; inspector owns detailed configuration
- Keyboard parity is mandatory
- Same workflow object consumed by both surfaces (dual-surface authoring)

**Tangy's recheck:** All four quality gates pass; issue #59 is green and acceptance-complete.

---

