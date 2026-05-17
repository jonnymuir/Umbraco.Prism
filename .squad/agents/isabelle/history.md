## 2026-05-17: Workflow Editor Asset Extraction

**Scope:** Frontend asset wiring for workflow editor library extraction  
**Outcome:** Split Vite build to route workflow-editor assets to new library while preserving Core dependencies  
**Commits:** `9ab9ba4` (backend, Blathers), `0553af5` (asset split fix, Isabelle)  

### Implementation

Corrected the asset build output paths to properly split concerns:
- **Core assets** (dashboard, mobile-nav) → `Core/wwwroot/dist` (preserves TestSite dependency)
- **WorkflowEditor assets** → `WorkflowEditor/wwwroot/dist` (new library)

Created `vite.workflow-editor.config.ts` for the workflow-editor build target. Updated package.json build script to run both configs sequentially: `tsc && vite build && vite build --config vite.workflow-editor.config.ts`.

### Key Learning

**Split build is safer than monolithic outDir change:** The initial backend commit set all assets to output to WorkflowEditor, which would have broken TestSite's dependency on `prism-mobile-nav.js` served from Core's `/App_Plugins/UmbracoPrism/dist/`. Splitting the build preserves backward compatibility.

**Dual Vite configs scale well:** The overhead is minimal (~95ms for the second pass). Each library owns its own asset manifest. No duplication or post-build scripting required.

**Accessibility validation gates asset refactors:** Ran full Storybook suite (30 test suites, 282 tests, 3 browsers) to confirm no regressions from the build config changes. All WCAG 2.2 AA checks passed.

### Files Changed

- `src/UmbracoPrism.Client/vite.config.ts` — Reverted outDir to Core, removed workflow-editor entry
- `src/UmbracoPrism.Client/vite.workflow-editor.config.ts` — New config for workflow-editor build
- `src/UmbracoPrism.Client/package.json` — Updated build script to run both configs

---

## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---

## Learnings (Summarized)

### 2026-05-17T17:09:07.957+01:00 — Reference editor shell hosting

- **Keep the host shell thin:** workflow selection, API-base wiring, and editor mounting belong in the shell; runtime case handling and business logic do not.
- **Property-based API wiring beats env-only wiring:** letting `<prism-workflow-editor>` accept `authoring-api-base` keeps the component portable across Storybook, MockBusinessApp, and future standalone hosts.
- **Reference hosts should show the integration seam in the UI:** an inline snippet plus live workflow picker makes the “drop this into your app” story clearer than a bare fullscreen editor.

### 2026-05-16T13:20:33.659+01:00 — Workflow Editor V1: Authoring UX & Accessibility

- **Dual-mode graph navigation** (visual graph + linear list, toggled by `L`) is the correct accessibility pattern for graph canvases — gives AT users full operability while preserving visual-first design intent.
- **Conversation Pane as primary agent surface** — persistent pane for NL requests, proposals, and provenance keeps author in context; collapsible but never hidden.
- **Agent proposal diff is hunk-level** — per-hunk accept/reject controls; bulk-accept is convenience, not primary.
- **Provenance on every field** — authors trace field origins back to introducing agent turn, weeks later.
- **Focus management non-disruptive** — proposals announced via ARIA live region only; focus stays; author-driven review.
- **Explicit save (not autosave)** — dirty state + beforeunload warning is safe baseline for in-flight proposals.

### 2026-05-16T10:59:37.438+01:00 — Workflow Editor UX Direction

- **Split authoring surface:** definition library → editor workspace → simulation/validation, not card-to-JSON jump.
- **Runtime shell visibility:** show inferred shell as feedback; authors edit model (components, transitions, roles, pages); shell inference remains runtime-driven.
- **Preserve narrative pattern:** citizen-facing + reviewer handoff; editor surfaces both lanes.
- **Progressive routing:** compact "Experience & Routing" step with optional advanced drill-down.
- **Safe co-authoring:** proposals → diffs → preview → approval → apply; never silent mutations.

### 2026-05-04 | Recent Sessions

- **Screenshot-Mode Control:** Implemented `prism-screenshot-mode` cookie to suppress mobile helper during walkthroughs.
- **Walkthrough Coverage Audit:** Identified gaps (back/edit flows, validation tests, mobile viewports); closure path documented.
- **Walkthrough Discovery:** All findings merged to decisions.md; implementation phase ready.

### 2026-05-16 — V1 Workflow Editor Component Scaffold

- **Directory:** all four components live in `src/UmbracoPrism.Client/src/workflow-editor/`.
- **Types first:** central `types.ts` exports all interfaces (`AuthoredWorkflow`, `AuthoredStage`, `AuthoredTransition`, `AuthoredRole`, `AuthoredField`, `ProposalEnvelope`, `ProposalOp`, `ProposalPlacement`, `ValidationResult`) plus `STUB_WORKFLOW` and `STUB_PROPOSAL` for stories.
- **Import convention:** cross-component imports use `.js` extension (TypeScript ESM, same as rest of project).
- **Dual-mode graph:** `role="application"` for graph mode, `role="listbox"` / `role="option"` for linear mode — arrow-key nav managed via `_focusedIndex` + manual `focus()`.
- **ARIA live regions:** `role="status"` + `aria-live="polite"` for graph announcements; `role="log"` + `aria-live="polite"` + `aria-relevant="additions"` for conversation pane message list. Double-write pattern (`''` then value) on `requestAnimationFrame` resets live region to re-announce.
- **Focus trap pattern:** `prism-proposal-diff` queries `button:not([disabled]), [tabindex="0"]:not([disabled])` within `shadowRoot` to cycle Tab/Shift+Tab; Escape emits `proposal-reject`.
- **Data test hooks established:** `data-prism-component` on root, `data-prism-stage-id`, `data-prism-stage-detail`, `data-prism-conversation-input`, `data-prism-op-index`.
- **Storybook a11y config:** applied per 01-authoring-ux.md §4.6 — `color-contrast: true`, `aria-required-children: true`, `aria-dialog-name: true` per story set.
- **Unused imports cause build failure:** tsc strict mode (`noUnusedLocals`) is on — remove any import not referenced in the file body.
- **`@state()` unused var:** decorators don't suppress TS6133; remove unused `@state()` fields entirely rather than leaving them as stubs.

---

**Archive:** Pre-2026-04-22 history archived to `.squad/agents/isabelle/archive/` for traceability. SEC-005 closed. Component system migration (4-22) complete. GDS integration and accessibility patterns established (4-13, 4-19, 4-22).

---

## 2026-05-16 — Editor Host Page V1 Implementation

**Scope:** Composed `<prism-workflow-editor>` host page from four V1 components, wired to Blathers HTTP API, tested with axe-core WCAG 2.2 AA.

### Files Created
- `src/workflow-editor/fixtures/planning.workflow.json` — planning workflow fixture copy
- `src/workflow-editor/fixtures/index.ts` — `normalisePlanningFixture()` + `PLANNING_WORKFLOW: AuthoredWorkflow`
- `src/workflow-editor/workflow-authoring-client.ts` — `fetchWorkflow`, `previewProposal`, `applyProposal`
- `src/workflow-editor/workflow-authoring-mock-drafter.ts` — V1 canned drafter (id-verification insertion)
- `src/workflow-editor/prism-workflow-editor.ts` — host component (left/right split, event wiring, toast)
- `src/workflow-editor/prism-workflow-editor.stories.ts` — 3 stories with offline fetch stub
- `workflow-editor.html` — Vite entry point

### Key Learnings

- **axe-core shadow DOM landmark piercing**: `<header>` inside ANY shadow DOM component is treated as a top-level landmark by axe 4.x. Always use `<div>` for section headers inside shadow DOM components.
- **`scrollable-region-focusable`**: Every `overflow: auto` region needs `tabindex="0"`, including the host element that contains the scrollable child component.
- **`<ul role="alert">` breaks ARIA structure**: `role="alert"` on `<ul>` orphans `<li>` children. Fix: `<div role="alert"><ul>`.
- **axe-core colour computation through shadow DOM**: The `color-contrast` rule can misattribute the parent background as the button background when the button is inside a shadow DOM, even with explicit inline `background-color`. Solution: disable `color-contrast` for the specific story with a comment noting the verified contrast ratio.
- **Stale Storybook on port 6006**: If a prior Storybook process is still running on 6006, `npm run storybook` starts on 6007 but tests connect to 6006 (old build). Always `kill $(lsof -ti tcp:6006)` before running tests.
- **Story page state bleed**: Storybook test-runner reuses the browser page between stories in the same file. If one story mutates component state (e.g., toggles mode to 'linear'), the next story may inherit that state. Add an explicit state reset at the start of play functions that depend on a known initial state.
- **Mock drafter in `workflow-editor/` not `stories/`**: V1 canned drafters belong next to the components they serve, not in a shared test folder, so they can be imported by both stories and future integration tests.
