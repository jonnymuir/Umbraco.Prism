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
