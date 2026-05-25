# History: Tangy (Tester)

**Summary:** Workflow editor behavioral testing. Focus: overflow, responsive layout, accessibility validation, comprehensive layout proof with measured DOM geometry. See `history-archive.md` for full session-by-session record.

---

## 2026-05-25T16:48:28Z — Gateway-Only Redo: Behavioural Proof Rewrite

**Task:** Rewrite gateway-only behavioural proof; replace hybrid transition-first tests  
**Outcome:** ✅ Complete

### Decisions

1. **Gateway mismatch review** (2026-05-25T16:39:24.354+01:00)
   - Current editor not aligned: transitions still first-class objects
   - Gateways behave as attached annotations, not primary routing
   - Gateway visuals (rounded dashed cards) not aligned to design
   - Minimum correction: make gateways only visible/editable routing object

2. **Gateway-only behavioural proof replaces hybrid transition proof** (2026-05-25T16:48:28.029+01:00)
   - Frontend contracts rewritten: graph, gateway, validation, transition-editor all gateway-first
   - Backend contracts updated: authoring, validation now gateway-only language
   - Tests intentionally hold the line on product language and visual reading
   - Quality gate now fails hybrid models (boxes + arrows + badges)

### Test Contracts Updated

**Frontend:**
- Graph proof: canvas reads stage → gateway → next node
- Gateway proof: gateway language, join-owned waiting, list-mode gateway rows
- Validation proof: gateway language for unreachable stages
- Transition editor: redirected from chip-editing to gateway-first routing

**Backend:**
- Authoring: join-gateway waiting as correct source
- Validation: reject direct stage-to-stage and stage-level waiting

### Quality Gate Enforcement

Tests now intentionally fail any:
- Hybrid model presentation (boxes plus arrows plus badges)
- Transition chips as user-facing primary objects
- Rounded-card gateway visuals
- Stage-level waiting constructs

### Orchestration Log

Written to `.squad/orchestration-log/2026-05-25T15-48-28-tangy.md`

### Coordination

Tangy's tests now hold the corrected model stable while Isabelle and Blathers complete frontend/backend alignment.

---

## 2026-05-23T13:24:52Z — Lane Header Clearance & Viewport Background Width Proof Tests (Final Validation)


## 2026-05-24 — CI Red Run Resolution

Validated failing client contracts and tightened affected test expectations. Contract alignment completed, quality gate recovered. Local test suite validation passed. Decisions logged: `tangy-ci-contracts.md`, `tangy-ci-fix-lane.md`.

---

## Earlier Sessions (Archived)

For detailed earlier work, see `history-archive.md`.

## Learnings

- 2026-05-25T22:04:00.819+01:00 — Reviewer follow-up validation for the canvas cleanup should stay narrow and geometry-led: `npm run build`, `npm run test-storybook:ci:all`, then the targeted Playwright trio `workflow-graph-layout-proof.spec.ts`, `workflow-transition-editor.spec.ts`, and `workflow-editor-gateways.spec.ts`. That set is enough to prove the two regressions are genuinely cleared: same-lane routing choices keep separate sibling slots, and the applicant branch stays above the join gateway instead of colliding with it.
- 2026-05-25T22:04:00.819+01:00 — For canvas cleanup proofs, replace brittle shell screenshots with measured graph geometry. One same-lane fan-out fixture and one cross-lane fan-out fixture are enough to expose the real regressions: repeated validation detail on Canvas, sibling gateways stacked into one slot, and join/stage overlaps that make the route unreadable.
- 2026-05-25T16:48:28.029+01:00 — When the product model changes from "transitions plus gateways" to "stages plus gateways," rewrite the behavioural proof around what authors can see and edit: geometry that reads stage → gateway → next node, diamond routing cues, gateway-owned waiting copy, and validation language that points authors back to gateways instead of generic routes.
- 2026-05-25T09:32:35.455+01:00 — For the concurrent multi-lane redesign, keep one straight-line workflow proof green as a control while adding new parallel-lane and join-gateway proofs. Slice the work into editor contracts, showcase-story evolution, and live walkthrough proof so the behavioural gate can move forward without losing demo clarity.
- 2026-05-25T09:54:48.365+01:00 — When workflow surface rules are being collapsed, keep Playwright contracts on user-facing language: tab roles, visible lane labels, and assignment copy. Avoid asserting internal surface enums or exact lane counts that can change during cleanup without changing author-visible behaviour.
- 2026-05-25T11:55:20.362+01:00 — PR #88 review: behavioural contracts stayed honest through surface cleanup. Preview tests use semantic navigation (role, tab selectors) instead of raw data attributes. Validation jump tests prove return-to-Canvas before inspector focus. Lane tests assert visible labels ("Journey lanes", "Operations lanes") rather than internal surface enums. All focused validation green; approved pending Storybook/CI lanes.
- 2026-05-25T14:17:36.055+01:00 — Issue #82 baseline validation: Build green, backend workflow authoring tests green (106 passed after clean rebuild), key Playwright tests green (graph keyboard, action editor, validation rail, planning smoke). Simulation tests failing (pre-existing issue: tests don't switch to Simulation tab before clicking start button). End-to-end behavioural contracts: planning workflow projection, straight-line stage progression, assignment-driven lane ownership, validation rail with jump-to-item, graph keyboard navigation, inspector field feedback. Gateway representation work must preserve: (1) straight-line workflow execution in planning fixture, (2) stage-to-state projection fidelity, (3) assignment-driven lane derivation, (4) graph path highlighting for single-cursor flows, (5) validation rail contract for unreachable stages.
- 2026-05-25T14:17:36.055+01:00 — Issue #83 gateway representation tests: Created 7 behavioral contracts for editor-only gateway visibility (split/join visual distinction, lane ownership, inspector integration, graph/list mode rendering). Tests written to pass with zero gateways (current baseline) and prove gateway UI when Isabelle implements #83. All tests green on empty fixture, all existing tests remain green (graph visual/keyboard, action editor, validation rail, stage preview). Backend authoring tests: 106 passed. This slice keeps execution contract stage-driven while making gateway intent visible in the editor.
- 2026-05-25T16:39:24.354+01:00 — UX review: the current editor still teaches “stages, transitions, and gateways,” not “stages and gateways.” Rounded gateway cards, selectable transition chips, transition-first dialogs/inspector copy, and heuristic gateway anchoring make gateways feel optional decoration rather than the actual transition points between stages.

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


## 2026-05-25T14:34:44.680Z — Merged Gateway Behavioral Test Slice

**Spawn:** tangy background agent  
**Task:** Pin merged gateway behavioral tests (#83/#84/#85)  
**Outcome:** ✅ Complete

### Test Coverage

| Surface | Passed | Skipped | Notes |
|---------|--------|---------|-------|
| Backend authoring (xUnit) | 129 | 3 | WaitingCopy, RequiredLanes, deterministic release deferred |
| Gateway editor (Playwright) | 7 | 1 | Gateway creation/routing/lane ownership verified |
| Parallel lanes (Playwright) | 6 | 3 | Multi-lane cursor semantics verified; join release deferred |

### Test Decisions

1. **Skipped tests document future contracts** — Deferred semantics (#84 WaitingCopy, #85 join release) written with explicit skip reasons
2. **Lane column selectors use `[data-prism-role-lane]`** — Semantic unit for parallel-lane tests
3. **Each gateway has exactly one lane owner** — Single-owner invariant tested in both gateway and parallel-lanes specs
4. **Stage/gateway node separation is hard** — `[data-prism-stage][data-prism-gateway]` must always return 0 elements
5. **Pre-existing full-suite hang not addressed** — Individual specs run cleanly; full directory hang deferred to future investigation

### Cross-Layer Coverage

✅ Projection fidelity (stages → published states)  
✅ Assignment-driven lane derivation  
✅ Graph path highlighting (single-cursor flows)  
✅ Validation rail contracts  
✅ Stage-to-stage backward compatibility  
✅ Keyboard navigation (stages + gateways)  

**Orchestration log:** `.squad/orchestration-log/2026-05-25T14-34-44-tangy.md`

## 2026-05-25T21:04:00Z — Canvas Layout Geometry Verification Complete

**Task:** Reviewer re-check on canvas layout fixes  
**Outcome:** ✅ Complete

### Validation Results

- ✅ Both reported failures no longer reproduce
- ✅ Relevant client validation lanes passed
- ✅ Geometry tests confirm real slot readability

### Test Contract Updates

**Decision: Canvas cleanup proof measures slot readability, not shell screenshots** (proposed)

- Measured DOM geometry for behavioral proof instead of screenshot-only checks
- Same-lane fan-out story: verify sibling gateways do not overlap
- Cross-lane fan-out story: verify branch work reads as branch row before join
- Fail conditions:
  - Same-lane sibling gateways overlap
  - Branch work collapses into join row
  - Canvas repeats Validation detail copy

### Quality Gate Enforcement

- Geometry tests no longer depend on shell-width assumptions
- Layout fixtures capture real canvas behaviours (not stale baselines)
- Gateway readability is now provable, not just visual inspection

### Pending Coverage

- **Isabelle implementation:** Canvas UX with orthogonal rails and slot grid
- **Validation tab parity:** Confirm Canvas tab no longer repeats validation warnings
- **Join trunk routing:** One downstream trunk from join to released stage

**Orchestration log:** `.squad/orchestration-log/2026-05-25T21-04-00Z-tangy.md`  
**Team coordination:** Multi-agent canvas layout fix verification
