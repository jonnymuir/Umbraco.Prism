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

### 2026-05-17T12:45:42.676+01:00 — Fast-Fail CI Strategy for Flaky Tests

**Context:** PR #52 planning workflow editor walkthrough was failing in CI but passing locally. Feedback loop was 20+ minutes per iteration due to alphabetical test execution order and insufficient diagnostics on timeout.

**Strategy implemented:**

1. **Test execution order:** Renamed test to `01-planning-workflow-editor.walkthrough.spec.ts` to run FIRST in the localhost-auth lane. Reduces feedback latency from 20+ mins to <5 mins on failure.

2. **Decisive readiness diagnostics:** Wrapped the workflow editor readiness wait in try/catch with:
   - Custom element registration check (`customElements.get('prism-workflow-editor')`)
   - Module script loading status (all `script[type="module"]` src attributes)
   - `data-prism-workflow-loaded` attribute value (empty = still loading, workflow key = ready)
   - Body content snippet (first 500 chars)
   - Full-page screenshot saved to `test-results/planning-editor-readiness-failure.png`
   - Structured JSON logged to console and thrown in error message

**Result:** Next CI failure will pinpoint the exact hydration/fetch/module issue without guesswork or manual trace inspection. No blanket retries or timeout inflation.

**Pattern for reuse:** For any async readiness wait (custom elements, API fetches, service workers) in CI-flaky tests:
- Wrap in try/catch
- Capture semantic state indicators (attributes, flags, registration checks)
- Screenshot to `test-results/` (uploaded by CI artifact step)
- Structured JSON to console
- Prefix test filename with `01-`, `02-` if under active development (remove when stable)

**Files modified:**
- `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` (renamed + diagnostics)
- `.squad/decisions/inbox/tangy-fast-fail-diagnostics.md` (pattern decision)

**Commit:** `c27c8fd` (test: fast-fail CI strategy for planning workflow editor walkthrough)

---

### 2026-05-16T17:47:42.605+01:00 — V1 Test Seam Scaffolding

**Test seam layout:**
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/PlanningWorkflowFixtureTests.cs` — C# fixture contract; 4 skipped tests awaiting Blathers' `planning.workflow.json`. Skip message documents expected fixture shape (stages, transitions, roles, fields). Round-trip uses `JsonSerializerOptions { CamelCase, WriteIndented }` matching projection pipeline.
- `src/UmbracoPrism.Client/tests/agent-loop/planning-workflow-agent-loop.spec.ts` — 3 agent-loop stubs: NL→diff (skip), validation-fail→accept-disabled (skip), ID&V waiting state (`test.fixme()`). Storybook base URL `http://127.0.0.1:6006` via `playwright.config.ts`.
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts` — 2 keyboard contract stubs: arrow-key navigation in linear mode (skip), mode toggle aria-pressed (skip). Both await Isabelle's Storybook stories.

**Behavioural naming conventions used:**
- Test names are complete sentences describing observable author/applicant behaviour, not implementation details.
- Pattern: `"{Actor} {can/cannot} {verb} {object} when {condition}"` — e.g. `"Author cannot apply a proposal whose validation status is fail"`.
- `test.fixme()` used when the dependency is a later wave (full projection pipeline). `test.skip(true, reason)` used when the dependency is a parallel-wave Storybook story (Isabelle).

**Hooks consumed (Isabelle's contracts from 01-authoring-ux.md §10):**
- `data-testid="conversation-pane"` — `<prism-conversation-pane>` root
- `data-testid="conversation-input"` / `data-testid="conversation-send"` — NL chat input/send
- `data-proposal-id="{id}"` — `<prism-proposal-diff>` root
- `data-testid="proposal-accept-all"` / `data-testid="proposal-reject"` — bulk accept/reject
- `data-hunk-id="{n}"` — individual diff hunk
- `data-testid="workflow-graph"` — `<prism-workflow-graph>` root
- `data-testid="linear-list"` — `<prism-linear-list>` root
- `data-testid="toolbar-list-view"` — mode toggle button (`aria-pressed`)
- `data-testid="graph-announcer"` — live region for screen-reader announcements

**Result:** dotnet test 690 passed + 4 skipped (0 failures); Playwright 4 passed + 10 skipped (0 failures). Commit: `916045e`.

---

### 2026-05-17T12:30:00+01:00 — Planning Workflow Editor CI Flake Fix (Web Component Hydration Race)

**Context:** PR #52 failed CI at run 25988472206. First pass (commits 17657db, 07f0070) hardened the readiness probe against unseeded splash pages. That fixed the probe, but the test still failed at line 74 inside the spec. Test passed locally in 1.1m but timed out on CI waiting for heading `/planning permission/i`.

**Root cause:** Web component hydration race. The workflow editor is a Lit custom element that:
1. Loads as ES module (`<script type="module" src="/workflow-editor.js">`)
2. Registers via `@customElement('prism-workflow-editor')`
3. Fetches workflow data from `/api/workflow-authoring/workflows/{key}` in `connectedCallback()`
4. Renders heading in shadow DOM AFTER fetch completes

On CI, `page.goto()` completes on `load` event before the module executes and the async fetch finishes. The page snapshot (from CI trace) showed empty `<body>` with no custom element content yet.

**Diagnosis method:** Downloaded CI trace artifacts via `gh run download 25988472206`. Inspected:
- `0-trace.trace` (JSON event log): showed `goto` completed successfully, then heading expect timed out after 30s
- Page snapshots: revealed empty body after navigation
- Network log: confirmed no API fetch visible at failure point

**Fix applied (commit ffea002):** Added explicit wait for the workflow data load signal BEFORE the `step()` call:
```typescript
await page.waitForSelector('[data-prism-workflow-loaded]:not([data-prism-workflow-loaded=""])', {
  timeout: 30_000,
});
```

The `data-prism-workflow-loaded` attribute is set by the component (line 200 of `prism-workflow-editor.ts`) to the workflow key once `_workflow` is populated. Empty string means still loading.

**Why not other approaches:**
- Bumping heading timeout → masks the race, doesn't wait for right signal
- Waiting for network idle → too broad, not semantic
- Waiting for graph canvas → happens AFTER heading check in `assertHealthyPage()`

**Key learning:** When testing pages with ES modules + web components + async fetches, **identify the semantic ready signal** and wait for it explicitly. Don't rely on `page.goto()` load event alone — modules and custom elements hydrate AFTER load, especially on slower CI hardware.

**Trace artifact workflow:** CI already uploads traces on failure (ci-tests.yml lines 149-157). This was essential for diagnosis. Without the trace, the "passes locally, fails on CI" gap would be unsolvable.

**Files modified:** `src/UmbracoPrism.Client/tests/walkthroughs/planning-workflow-editor.walkthrough.spec.ts` (8-line addition)

**Validation:** Test still passes locally in 1.1m. Pushed to PR branch; awaiting CI confirmation.

---

### 2026-05-17T11:30:59+01:00 — TestSite Readiness Probe Hardening

**Context:** PR #52 (`squad/planning-workflow-editor-walkthrough`) failed the `localhost-auth-playwright` CI lane (run 25987849590) due to a race condition: Umbraco's HTTP listener started responding with the default "No Published Content" page before seeding completed, and the probe abandoned.

**Root cause:** The probe checks `https://localhost:44345/` for `data-prism-home-ready="true"` (emitted by `Views/homePage.cshtml` only when seeded content is published). When Umbraco boots but hasn't seeded yet, it returns HTTP 200 with the unseeded splash page. The probe treated this body-mismatch as any other failure and eventually timed out.

**Fix applied (commit 17657db):**
- Added `umbracoUnseededPageMarkers` constant with known unseeded-page patterns: `<title>Umbraco: No Published Content</title>`, `Welcome to your Umbraco installation`, etc.
- Modified body-check logic to detect unseeded splash and classify as "still seeding" (not a hard failure). The probe now distinguishes "Umbraco booting" (timeout/ECONNREFUSED) from "Umbraco up but unseeded" (200 + splash body) from "Umbraco fully ready" (200 + `data-prism-home-ready`).
- Added inline comments referencing PR #52 and CI run ID for traceability.

**File modified:** `src/UmbracoPrism.Client/tests/support/live-app-host.ts` (lines 9-23, 321-337).

**Trade-offs:** Did NOT implement a dedicated `/__prism/seed-status` endpoint (would require Blathers' backend work). The pattern-matching approach is cheaper and sufficient. Documented the endpoint recommendation in a decision file for future consideration.

**Validation:** TypeScript pre-existing errors unrelated to this change. Pushed to PR branch; CI will validate the fix.

---

### 2026-05-17 — Planning Workflow Editor Walkthrough (Wave 1 Headline)

**Scope:** Delivered the walkthrough spec, markdown narrative, keyboard test activation, and seam-test skip-reason updates for the planning workflow editor.

**Key findings from codebase exploration:**

**Shadow DOM and Playwright selectors:**
- Playwright's `getByRole()` (and all ARIA role locators) **pierce shadow DOM automatically** in Playwright ≥ 1.38. Use them for all component-internal elements.
- CSS attribute selectors (`page.locator('[data-testid="..."]')`) do **not** pierce shadow DOM. Never use them to reach inside Lit/web-components shadow roots.
- `toBeFocused()` works on shadow-DOM elements — Playwright checks `element.matches(':focus')` which resolves correctly inside shadow roots.
- `toHaveCount(0)` is the correct assertion when an element is **removed** from the DOM (e.g. graph canvas when switching to linear mode). `toBeHidden()` fails because the element is absent, not just hidden.

**Actual hooks vs design doc:**
- `docs/design/workflow-editor-v1/01-authoring-ux.md §10` documents `data-testid="workflow-graph"`, `data-testid="linear-list"`, `data-testid="toolbar-list-view"` etc. These hooks were **not implemented** by Isabelle's components.
- Actual hooks shipped: `data-prism-component="workflow-graph"`, `data-prism-mode="graph|linear"`, `data-prism-stage="{stageKey}"`, `data-prism-stage-detail="{stageKey}"`, `data-prism-component="conversation-pane"`, `data-prism-conversation-input`, `data-prism-component="step-inspector"`, `data-prism-op-index`.
- **Role-based selectors are preferred over attribute hooks** — they are shadow-DOM-piercing and encode the WCAG contract simultaneously.

**Storybook story IDs (verified against .stories.ts files):**
- `workflow-editor-workflow-graph--populated-workflow` → STUB_WORKFLOW loaded, graph mode, no play() mode change
- `workflow-editor-workflow-graph--linear-mode` → renders already in linear mode (play() switches)
- `workflow-editor-workflow-graph--empty` → empty graph
- `workflow-editor-workflow-graph--stage-selected` → stage pre-selected
- `workflow-editor-conversation-pane--empty`, `--with-proposal` (NOT `--with-mocked-proposal`)
- `workflow-editor-proposal-diff--no-proposal`, `--with-proposal` (NOT `--with-failing-proposal`)
- Previous skipped tests referenced wrong story IDs (`--default`, `--with-mocked-proposal`, `--with-failing-proposal`).

**Keyboard test activation (workflow-graph-keyboard.spec.ts):**
- Both tests un-skipped. Changed to `--populated-workflow` story (starts in graph mode, no play() side effects).
- `data-testid="linear-list"` → `page.getByRole('listbox')` (pierces shadow DOM)
- `data-testid="workflow-graph"` → `page.getByRole('application')` (graph canvas in shadow DOM)
- `data-testid="toolbar-list-view"` → `page.getByRole('button', { name: 'List view' })` (label switches to "Graph view" when active)
- In linear mode, items have `role="option"` (not `role="row"`) — `getByRole('option')` finds them.
- After toggling to linear mode, graph canvas is **removed** from shadow DOM → `toHaveCount(0)` assertion.

**Walkthrough skip pattern:**
- `test.skip(true, reason)` placed in describe scope immediately before `test(...)` skips that specific test.
- When ALL tests in a describe block are skipped this way, Playwright skips `beforeAll`/`afterAll` hooks too — so `LiveAppHost.start()` is NOT invoked for skipped walkthroughs. This is why skipped walkthroughs pass in the default Playwright config (Storybook) without starting Aspire.

**Screenshot capture command:**
```bash
cd src/UmbracoPrism.Client
CAPTURE_SCREENSHOTS=1 npx playwright test \
  --config=playwright.localhost-auth.config.ts \
  --grep "Planning Workflow Editor walkthrough" \
  --reporter=line
```

**STUB_WORKFLOW stages** (types.ts): `applicant-details`, `check-answers`, `waiting-for-review`, `reviewer-assessment`, `confirmation` — these are the actual stage keys in the test stub. The design doc references `declaration`, `application-form`, `submitted` which are the future planning-specific stages Blathers will deliver.

**Deliverables:**
- CREATED: `src/UmbracoPrism.Client/tests/walkthroughs/planning-workflow-editor.walkthrough.spec.ts`
- CREATED: `docs/walkthroughs/planning-workflow-editor.md`
- CREATED: `docs/images/walkthroughs/planning-workflow-editor/.gitkeep`
- MODIFIED: `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts` (un-skipped; corrected story IDs and selectors)
- MODIFIED: `src/UmbracoPrism.Client/tests/agent-loop/planning-workflow-agent-loop.spec.ts` (skip reasons clarified)
- MODIFIED: `src/UmbracoPrism.Core.Tests/Workflow/Authoring/PlanningWorkflowFixtureTests.cs` (skip reasons updated)
- MODIFIED: `docs/walkthroughs/README.md` (linked new walkthrough)

---

### 2026-05-16T13:20:33.659+01:00 — Agentic Surfaces & Test Seam (V1 Design)

**Agentic Operating Model:**
- Agent plane sits above projection plane: never direct `WorkflowDefinitionFile` mutation.
- Six surfaces in order: authored source → projected file → proposal envelope → validate (< 250ms) → preview (< 1s) → test hooks.
- Proposal envelope: atomic unit carrying ops, placement, rationale, provenance, validation result, preview artifact.

**Reuse-vs-Build Boundary:**
- **Reuse:** GitHub Copilot (NL intent, rationale, repo edits, MCP orchestration). No workflow-domain knowledge needed.
- **Build:** Projection, semantic diffing, insertion-point resolution, safe graph transforms, preview rendering, structural validation.
- Anti-pattern: general agent inferring JSON semantics; UI-only automation; hidden mutations.

**Test Seam Architecture:**
- **Unit (C# / XUnit):** Projection determinism, shell preservation, patch apply, validate correctness/latency, semantic diff. Existing `WorkflowDefinitionInferenceTests` + `SeedFileRoundtripTests` remain green.
- **Component (Storybook + axe-core):** `<prism-workflow-graph>`, `<prism-proposal-diff>`, `<prism-proposal-panel>`, `<prism-journey-trace>` with accessibility assertions.
- **Journey (Playwright):** Core planning applicant + member + reviewer role-gated + ID&V wait state.
- **Agent-loop (Playwright + MCP):** NL → proposal → validate → preview → approve → apply → seed updated → audit log. 10 behavioural tests in `planning-workflow-agent-loop.spec.ts`.
- Planning application: canonical executable spec; every state transition mapped to Playwright test name.

### 2026-05-16T12:08:13.123+01:00 — Workflow Editor Agentic Restart

- Ship agent-facing contracts in order: authored source, projected `WorkflowDefinitionFile`, diff/provenance artifact, validate command, preview/simulate, editor/test hooks.
- Keep existing TUI automation seam; do not make agents drive UI first.
- Best responsibility split: Copilot handles NL planning, proposals, repo edits, orchestration; editor owns parsing, resolution, transforms, preview, approval diffs.
- Human/agent loop: propose → preview → validate → approve → apply. Never free-form writes to runtime JSON or live instances.
- Planning application: best executable reference spec (multi-step, conditional reveal, validation, check-answers, completion).
- Minimal guardrails: schema/roundtrip tests, inference/graph tests, one planning executable-spec demo, narrow preview/approve contract.

### 2026-05-16T11:04:11.589+01:00 — Workflow Editor Agentic Operating Model

- Treat editor as three connected contracts: human-authored model, deterministic projection, agent-facing proposal/validation surfaces.
- Best machine-facing bundle: authored definition, projected `WorkflowDefinitionFile`, structured diff/provenance, validate, preview/simulate, test hooks.
- Planning app is strongest executable reference spec (multi-step capture, conditional reveal, check-answers, waiting/review, realistic service design).
- Key repo anchors:
  - `WorkflowDefinitionFile.cs` — runtime contract
  - `SeedFileRoundtripTests.cs` — regression guard
  - `WorkflowDefinitionInferenceTests.cs` — authored-vs-inferred shell contract
  - `WorkflowTuiService.cs` — terminal/dev harness
  - `*.walkthrough.spec.ts`, `workflow-gds-journey.spec.ts` — executable journeys
- Preserve direction: editor excellent for humans, also expose agent-ready interfaces (NL generation, clarification, refinement, targeted insertion like external ID&V).

### 2026-05-04 | Recent Sessions

- **Walkthrough Coverage Audit:** Test inventory (20 active + 5 manual-only); coverage strengths (happy paths, conditional reveals, validation) and gaps (operator flows, back/edit, mobile viewports) identified.
- **CI Test-Fragility Fix:** Fixed `PrismContextTests` fragility via concrete `CancellationToken` matchers → `It.IsAny<CancellationToken>()` (Platform-dependent lazy-init pattern).
- **Coverage Hardening:** Proposed additions (back/edit flows, validation tests, mobile tests, success assertions, home-entry walkthrough).
- **Walkthrough Discovery:** Phase complete; findings merged to decisions.md.

---

**Archive:** Pre-2026-05-04 sessions archived to `.squad/agents/tangy/history-archive.md`. Covers: Downstream timeout diagnosis, transport diagnostics validation (5 contract tests), business API instrumentation, environment diagnostics, workflow 401 analysis.
