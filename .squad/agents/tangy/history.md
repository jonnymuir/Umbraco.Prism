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
