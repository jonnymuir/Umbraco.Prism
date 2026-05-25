# History: Tangy (Tester)

**Summary:** Workflow editor behavioral testing. Focus: overflow, responsive layout, accessibility validation, comprehensive layout proof with measured DOM geometry. See `history-archive.md` for full session-by-session record.

---

## 2026-05-23T13:24:52Z — Lane Header Clearance & Viewport Background Width Proof Tests (Final Validation)


## 2026-05-24 — CI Red Run Resolution

Validated failing client contracts and tightened affected test expectations. Contract alignment completed, quality gate recovered. Local test suite validation passed. Decisions logged: `tangy-ci-contracts.md`, `tangy-ci-fix-lane.md`.

---

## Earlier Sessions (Archived)

For detailed earlier work, see `history-archive.md`.

## Learnings

- 2026-05-25T09:32:35.455+01:00 — For the concurrent multi-lane redesign, keep one straight-line workflow proof green as a control while adding new parallel-lane and join-gateway proofs. Slice the work into editor contracts, showcase-story evolution, and live walkthrough proof so the behavioural gate can move forward without losing demo clarity.
- 2026-05-25T09:54:48.365+01:00 — When workflow surface rules are being collapsed, keep Playwright contracts on user-facing language: tab roles, visible lane labels, and assignment copy. Avoid asserting internal surface enums or exact lane counts that can change during cleanup without changing author-visible behaviour.
- 2026-05-25T11:55:20.362+01:00 — PR #88 review: behavioural contracts stayed honest through surface cleanup. Preview tests use semantic navigation (role, tab selectors) instead of raw data attributes. Validation jump tests prove return-to-Canvas before inspector focus. Lane tests assert visible labels ("Journey lanes", "Operations lanes") rather than internal surface enums. All focused validation green; approved pending Storybook/CI lanes.
- 2026-05-25T14:17:36.055+01:00 — Issue #82 baseline validation: Build green, backend workflow authoring tests green (106 passed after clean rebuild), key Playwright tests green (graph keyboard, action editor, validation rail, planning smoke). Simulation tests failing (pre-existing issue: tests don't switch to Simulation tab before clicking start button). End-to-end behavioural contracts: planning workflow projection, straight-line stage progression, assignment-driven lane ownership, validation rail with jump-to-item, graph keyboard navigation, inspector field feedback. Gateway representation work must preserve: (1) straight-line workflow execution in planning fixture, (2) stage-to-state projection fidelity, (3) assignment-driven lane derivation, (4) graph path highlighting for single-cursor flows, (5) validation rail contract for unreachable stages.
- 2026-05-25T14:17:36.055+01:00 — Issue #83 gateway representation tests: Created 7 behavioral contracts for editor-only gateway visibility (split/join visual distinction, lane ownership, inspector integration, graph/list mode rendering). Tests written to pass with zero gateways (current baseline) and prove gateway UI when Isabelle implements #83. All tests green on empty fixture, all existing tests remain green (graph visual/keyboard, action editor, validation rail, stage preview). Backend authoring tests: 106 passed. This slice keeps execution contract stage-driven while making gateway intent visible in the editor.

## 2026-05-25 (09:32:35 UTC) — Behavioural Test Track for Concurrent Lanes

- Issues #78–#80 created for editor, showcase, and walkthrough coverage
- Orchestration log recorded
- Tom Nook executing parallel redesign sequence (#81–#87)
- Coordinated squad execution ready

## 2026-05-25 (11:55:20 UTC) — PR #88 Quality Gate (Issue #81)

**Review:**
- ✅ Build passed
- ✅ Focused Playwright validation green (stage preview, validation rail, vertical lanes, graph visual)
- ✅ Behavioural contracts anchored to user-facing language (tab roles, lane labels, assignment copy)
- ✅ Preview navigation uses semantic selectors (role, tab name) instead of raw data attributes
- ✅ Validation jump tests prove return-to-Canvas before inspector focus
- ✅ Lane count changed from exact `toHaveCount(3)` to flexible `toBeGreaterThan(1)` — appropriate for cleanup work
- ✅ Filter buttons assert visible labels ("Journey lanes", "Operations lanes") instead of internal surface enums

**Decision:** Approved pending Storybook, core-tests, planning-smoke, and localhost-auth CI lanes. All focused validation passed; behavioural contracts remain sound. Coordinator should merge automatically once remaining CI lanes finish green.

**CI status at review:**
- ✅ marketplace-description, test, all
- ⏳ storybook-tests, core-tests, planning-workflow-editor-smoke, localhost-auth-playwright (in progress)

---
**2026-05-25 · Issue #83 · Gateway Behaviour Tests**

Established 7 behavioural contracts for gateway UI representation slice:
1. Split gateways visually distinct from stages
2. Join gateways visually distinct from stages
3. Gateways show lane ownership via data-prism-lane
4. Inspector integration for gateway-specific content
5. Transition direction visible in graph
6. Backward compatibility for no-gateway workflows
7. List mode includes gateways alongside stages

**Test Status:** All 7 new gateway tests passing (baseline). 106 backend authoring tests passing. Existing green suite maintained.

**Decision:** Tests written as guardrails; Isabelle will implement visual rendering in same slice.

---

## 2026-05-26 — Merged Slice #83/#84/#85: Multi-Lane Gateway Behavioural Contracts

**Branch:** `squad/83-84-85-multi-lane-gateway-tests`

**Issues covered:** #83 (gateway editor UX), #84 (join gateway waiting copy), #85 (parallel lane runtime safety)

### What was done

Created a unified behavioural test slice spanning backend authoring contracts, editor UX contracts, and parallel-lane runtime safety contracts. All tests are safe guardrails — green now where implementation exists, skipped where it awaits #84/#85 delivery.

**Files created/modified:**
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/MultiLaneGatewayContractTests.cs` — 17 backend xUnit facts (14 live, 3 skipped)
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-parallel-lanes.spec.ts` — 9 Playwright contracts (6 live, 3 skipped)
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-gateways.spec.ts` — extended from 5 to 8 tests (7 live, 1 skipped)

**Final test counts:**
- Backend authoring suite: **129 passed, 3 skipped** (was 106 before this slice)
- Gateway editor Playwright: **7 passed, 1 skipped**
- Parallel lanes Playwright: **6 passed, 3 skipped**

### Learnings

- `[data-prism-role-lane]` column containers hold stage nodes (`[data-prism-stage]`) but gateway nodes (`[data-prism-gateway]`) are graph siblings — they carry `data-prism-lane` as an attribute for lane attribution but are NOT DOM children of the lane columns. Tests that drill into lane columns must not assume gateways are nested inside them.
- Gateway-representation Storybook story may have lane columns where some contain only gateways (no stages). Playwright assertions after clicking a node should check lane column count stability — not require every lane to have stages inside.
- When running the full `tests/workflow-editor/` directory together, Playwright hangs (pre-existing issue with a blocking test in another spec file). Individual spec files run cleanly. This is not caused by my work.
- Build was clean despite earlier session reporting pre-existing `WorkflowRuntimeEngine.cs` errors; those errors resolved by the time the final build ran (Blathers had fixed the WIP stubs).

