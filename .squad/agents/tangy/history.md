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
