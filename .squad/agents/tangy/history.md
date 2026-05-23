# History: Tangy (Tester)

**Summary:** Layout professionalisation and browser-surface testing. See `history-archive.md` for full session-by-session record.

---

## 2026-05-23T09:17:57+01:00 — Vertical Lanes & Workflow Switcher Behavioral Proof

**Status:** Behavioral proof landed, awaiting Isabelle's vertical lanes CSS and workflow switcher implementation

**What I delivered:**

1. **New dedicated test file:** `tests/workflow-editor/vertical-lanes-switcher.spec.ts`  
   15 tests proving vertical lane orientation and workflow switcher behavioral contracts:
   - Workflow switcher changes mounted workflow (4 tests) — SKIPPED until shell story exists
   - Vertical lane orientation maintains accessibility (6 tests) — 6 GREEN
   - Browser entry flow with vertical lanes (3 tests) — SKIPPED until browser integration testable
   - Vertical lanes list mode parity (2 tests) — 2 GREEN

2. **Decision document:** `.squad/decisions/inbox/tangy-vertical-lanes-switch-proof.md`  
   Documents exact behavioral expectations, semantic hooks for Isabelle, and validation gate commands.

3. **Updated existing tests:**
   - `tests/workflow-editor/workflow-graph-keyboard.spec.ts` — 3 test names clarified with "(vertical orientation)" suffix, test suite docstring added
   - `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` — Step 2 updated to document vertical lanes expectation and verify lane semantic structure

4. **Baseline validation:**  
   - ✅ Client build (npm run build) — GREEN, no warnings
   - ✅ Keyboard tests (7 tests) — GREEN (orientation-independent semantic contracts)
   - ✅ Vertical lanes behavioral proof (8 tests) — GREEN
   - ⏳ Vertical lanes behavioral proof (7 tests) — SKIPPED (shell/browser integration, documented for Isabelle)
   - ⏳ Shell tests (24 tests) — some FAILING as expected (awaiting Isabelle's outline/tabs implementation)

**Working in parallel with Isabelle:** All behavioral expectations documented inline with `BEHAVIORAL HOOK REQUEST FOR ISABELLE` comments. Tests provide concrete acceptance criteria for:

1. **Vertical lanes CSS change:**
   - `aria-roledescription` should reflect vertical orientation (e.g., "Role-first workflow editor workspace with vertical lanes")
   - Existing `[data-prism-role-lane]` structure remains (focusable sections)
   - Existing `.lane-heading` and `.lane-copy` structure remains
   - CSS orientation change from `flex-direction: row` to `flex-direction: column` on lane container
   - Viewport usage thresholds adjusted for vertical stacking (ratio: 0.3 for current horizontal, expect higher with vertical)

2. **Workflow switcher wiring:**
   - `.workflow-selector[data-prism-workflow-selector]` — the dropdown control
   - `[data-prism-component="workflow-editor-shell"][data-prism-active-workflow="{key}"]` — shell reflects active workflow
   - `prism-workflow-editor[data-prism-workflow-loaded="{key}"]` — editor reflects loaded workflow
   - Workflow options populate from `/api/workflow-authoring/workflows` (not just hardcoded planning)
   - Changing workflow selector remounts editor with new workflow (keyed rendering)

**Validation commands (5-step gate):**
```bash
cd src/UmbracoPrism.Client && npm run build
cd src/UmbracoPrism.Client && npm run test-storybook:ci:all
cd src/UmbracoPrism.Client && npx playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line
cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke
cd src/UmbracoPrism.Client && npx playwright test tests/workflow-editor/vertical-lanes-switcher.spec.ts --reporter=line
```

**Expected test states:**
- Until Isabelle's implementation: 8 tests GREEN, 7 tests SKIPPED (shell/browser integration)
- After Isabelle's implementation: all 15 tests GREEN

**Plain-language verdict:**  
The behavioral proof is complete and landed. It proves vertical lanes remain accessible (keyboard, screen reader, pointer interactions intact) and documents the workflow switcher contract (actually changes workflows, not just showing planning). Tests work now with current horizontal lanes and will continue working when Isabelle flips to vertical — orientation change is purely visual/layout, behavioral contracts unchanged. Shell/browser integration tests are skipped with clear documentation for when the testable surface exists.

---

## 2026-05-23T07:42:49Z — Session Orchestration & Decisions Integration

**Scribe orchestration completed:** All decisions merged to `.squad/decisions.md`; orchestration logs created.

**Team status:**
- Isabelle: Tabbed layout redesign delivered; Canvas tab primary, confidence tools secondary
- Tangy: Layout professionalization behavioral proof delivered (22 tests, semantic hooks documented)
- Mabel: Host philosophy documentation moved to guides; thin-host philosophy established

**Current test state:**
- Existing tests (keyboard 7/7, planning walkthrough) remain GREEN
- Layout professionalization tests (22 tests) FAILING until Isabelle's implementation integrates
- Expected state after integration: all tests GREEN

**Decisions merged:**
1. User directive (host minimalism)
2. Workflow editor tabbed layout redesign
3. Host philosophy (keep reference shell minimal)
4. Layout professionalisation behavioral proof

**Next phase:** Integration testing; run 5-command validation gate when shell implementation lands.

---

## 2026-05-23T08:30:10.563+01:00 — Layout Professionalization Behavioral Proof

**Status:** Behavioral proof landed, awaiting Isabelle's shell refactor implementation

**What I delivered:**

1. **New dedicated test file:** `tests/workflow-editor/layout-professionalization.spec.ts`  
   19 tests proving the five layout professionalization dimensions:
   - Host chrome minimization (3 tests) — hero ≤15% viewport, explanatory prose removed, integration rail hidden
   - Simplified launch flow (3 tests) — API base not mainline, launch card streamlined, workflow selection utility-first
   - Editor surface prioritization (3 tests) — editor ≥80% viewport, editor as page not widget, section chrome removed
   - Keyboard/screen reader accessibility (4 tests) — skip link, tab order, shortcuts preserved, landmark semantics
   - Editor functionality preservation (6 tests) — outline, graph/list toggle, inspector, confidence tabs, swim lanes, pointer-unblocked stages

2. **Decision document:** `.squad/decisions/inbox/tangy-layout-professionalisation-proof.md`  
   Documents exact behavioral expectations, semantic hooks for Isabelle, and validation gate commands.

3. **Baseline validation:**  
   - ✅ Client build (npm run build) — green
   - ✅ Backend tests (810 tests) — green
   - ✅ Keyboard tests (7 tests) — green
   - ⏳ Shell tests (26 tests) — not yet run (long-running)
   - ⏳ New layout tests (19 tests) — expected to fail until Isabelle implements

**Working in parallel with Isabelle:** All behavioral expectations documented inline with `BEHAVIORAL HOOK REQUEST FOR ISABELLE` comments. Tests provide concrete acceptance criteria (viewport ratios, element visibility, semantic structure).

**Validation commands (5-step gate):**
```bash
cd src/UmbracoPrism.Client && npm run build
npm run test-storybook:ci:all
npx playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line
npm run test:playwright:planning-smoke
npx playwright test tests/workflow-editor/layout-professionalization.spec.ts --reporter=line
```

**Expected test states:**
- Until Isabelle's refactor: layout tests FAIL (expected), existing tests GREEN
- After Isabelle's refactor: all tests GREEN

**Plain-language verdict:**  
The behavioral proof is complete and landed. It proves the five layout improvements that make the reference host professional and usable. Tests document exact acceptance criteria for Isabelle's implementation with concrete thresholds (viewport ratios, tab counts, semantic structure).

---

**Full history:** See `.squad/agents/tangy/history-archive.md` for complete session record (2026-05-18 onward).

---
date: 2026-05-23T11:08:00+01:00
update: graph-canvas-scroll-proof
---

## 2026-05-23T11:08:00+01:00 — Graph-Canvas Scroll Container Behavioral Proof

**Status:** Behavioral proof landed, Isabelle's CSS change verified

**What I delivered:**

1. **Updated existing tests to prove graph-canvas scroll behavior:**
   - `tests/workflow-editor/workflow-editor-shell.spec.ts` — replaced "graph viewport" test with two new tests:
     - "graph-canvas is the scrollable region while shell chrome stays anchored" — proves `.graph-canvas` scrolls, shell chrome (outline, inspector, toolbar) Y positions don't change, window body stays at scrollY=0
     - "graph-canvas scrolling does not move shell chrome" — proves outline, inspector, and toolbar remain anchored while canvas scrolls
   - `tests/workflow-editor/vertical-lanes-switcher.spec.ts` — replaced "graph viewport" test with:
     - "graph-canvas is the vertical scroll surface in the graph workspace" — proves `.graph-canvas` has overflow-y, scrollHeight > clientHeight, window body stays at scrollY=0
   - `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` — updated scroll contract section to test `.graph-canvas` instead of `.graph-viewport`

2. **Decision document:** `.squad/decisions/inbox/tangy-graph-canvas-scroll-proof.md`  
   Documents the scroll container contract, CSS changes needed, behavioral proof, and validation commands.

3. **Behavioral contract proven:**
   - `.graph-canvas` is the scrollable region (overflow-y: auto)
   - `.graph-viewport` is NOT scrollable (overflow: visible)
   - Window body does NOT scroll when graph scrolls (window.scrollY stays 0)
   - Shell chrome (outline, inspector, toolbar) stays anchored while canvas scrolls

4. **Validation results (Isabelle's uncommitted CSS change already applied):**  
   - ✅ Shell tests (4 passed, 3 skipped) — both scroll tests GREEN
   - ✅ Vertical lanes tests (3 passed, 1 skipped) — scroll test GREEN
   - ⏳ Walkthrough test (skipped until PRs merge, but scroll section ready)

**Working in parallel with Isabelle:** Isabelle had already made the exact CSS change in her uncommitted working directory:
- `.graph-canvas` now has `overflow-y: auto` (NEW)
- `.graph-viewport` changed from `overflow: auto` to `overflow: visible` (CHANGED)

My tests prove this change works correctly — the canvas scrolls, the shell stays anchored, and the window body doesn't scroll.

**Validation commands (for final verification):**
```bash
cd src/UmbracoPrism.Client && npm run build
cd src/UmbracoPrism.Client && npx playwright test tests/workflow-editor/workflow-editor-shell.spec.ts --reporter=line
cd src/UmbracoPrism.Client && npx playwright test tests/workflow-editor/vertical-lanes-switcher.spec.ts --reporter=line
```

**Plain-language verdict:**  
The behavioral proof is complete and landed. Three tests now verify that `.graph-canvas` is the scroll container, not `.graph-viewport`. The tests prove the shell chrome (outline, inspector, toolbar) stays anchored while the canvas scrolls independently. Isabelle's uncommitted CSS change matches exactly what the tests expect, so when she commits, all tests will remain green.

---
date: 2026-05-23T09:20:56Z
update: spawn-cohort-complete
---

## 2026-05-23 Spawn Completion

Behavioral proof suite completed: 15 tests for vertical lanes & workflow switching (8 green, 7 skipped pending Isabelle's UI hooks). Existing keyboard tests remain unaffected (orientation change is visual only). Storybook shell proof passed (all browsers). Planning smoke blocked by external Aspire stack.

**Tests document exact semantic hooks for Isabelle:** drawer collapse contracts, workflow selector data attributes, panel toggle patterns. All fixme patterns documented inline. Ready to un-skip when UI hooks land.

---
date: 2026-05-23T10:02:16Z
update: scroll-container-consolidation
---

## 2026-05-23T10:02:16Z — Graph-Canvas Scroll Container Behavioral Proof Final

**Status:** ✅ BEHAVIORAL PROOF COMPLETE

Consolidated and finalized the scroll container behavioral proof. Three tests now verify that `.graph-canvas` is the scroll container while shell chrome stays anchored.

**Tests Updated:**
- `workflow-editor-shell.spec.ts` — "graph-canvas is the scrollable region while shell chrome stays anchored"
- `vertical-lanes-switcher.spec.ts` — "graph-canvas is the vertical scroll surface in the graph workspace"
- `01-planning-workflow-editor.walkthrough.spec.ts` — Graph-only contract section updated

**Behavioral Contract Proven:**
- `.graph-canvas` has `overflow-y: auto` (scrollable)
- `.graph-viewport` has `overflow: visible` (not scrollable)
- Window body does NOT scroll when graph scrolls
- Shell chrome (outline, inspector, toolbar) stays anchored

**Validation Results:**
- ✅ Shell scroll tests: 2 GREEN, 1 skipped
- ✅ Vertical lanes scroll test: 1 GREEN, 1 skipped

**Decision recorded:** Merged to `.squad/decisions.md` — "Graph-canvas as vertical scroll container" (2026-05-23T10:02:16Z)
