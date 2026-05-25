# Decision: Merged Slice #83/#84/#85 — Multi-Lane Gateway Behavioural Contracts

**Author:** Tangy (Tester)  
**Date:** 2026-05-26  
**Issues:** #83, #84, #85

---

## Context

Issues #83 (gateway editor UX), #84 (join gateway waiting copy), and #85 (parallel lane runtime safety) were merged into one behavioural test slice at Jonny's request. The goal was to pin the behavioural contracts across all three surfaces before #84 and #85 implementation is complete.

## Decisions

### 1. Skipped tests document future contracts explicitly

Where the model doesn't yet support a behaviour (#84 WaitingCopy on gateway, #85 RequiredLanes/deterministic release), tests are written with `[Fact(Skip = "...")]` / `test.skip(...)` with an explicit reason. This keeps the contract visible and runnable once implementation lands — it doesn't remove the expectation, just defers the assertion.

### 2. Lane column selectors use `[data-prism-role-lane]` as semantic unit

Parallel-lane Playwright tests treat lane column containers as the semantic unit. Stage nodes are DOM children of these containers; gateway nodes are graph siblings (they carry `data-prism-lane` attribute but are NOT nested inside `[data-prism-role-lane]`). Tests assert lane column count stability after interactions, not node nesting.

### 3. Each gateway is owned by exactly one lane

The invariant "a gateway has exactly one `data-prism-lane` value, never a comma-separated list" is enshrined as a live test in both the gateway spec and the parallel-lanes spec. This pins the single-owner contract regardless of rendering changes.

### 4. Stage/gateway node separation is a hard invariant

The compound selector `[data-prism-stage][data-prism-gateway]` must always return 0 elements. This is tested as a live assertion in `workflow-parallel-lanes.spec.ts`. Authors must be able to distinguish stage nodes (action-bearing) from gateway nodes (routing) at a glance.

### 5. Pre-existing full-suite Playwright hang is not addressed here

Running the full `tests/workflow-editor/` directory together causes Playwright to hang (pre-existing issue in another spec). Individual spec files run cleanly and consistently. No action taken — this is Tangy's test suite issue to investigate separately.

---

## Test counts (post-merge)

| Surface | Passed | Skipped |
|---------|--------|---------|
| Backend authoring (xUnit) | 129 | 3 |
| Gateway editor (Playwright) | 7 | 1 |
| Parallel lanes (Playwright) | 6 | 3 |

All live tests green. All skips have explicit rationale pointing to #84 or #85.
