# Workflow Editor Validation — Migrated Workflows

**Date:** 2026-06-06  
**Author:** Isabelle (Frontend Dev)  
**Context:** Post-migration editor rendering validation for the three Blathers-migrated workflows (planning, community-enquiry, information-request).

---

## Rendering Outcomes

### ✅ Planning Application (migrated format)
- Loaded without validation errors or error toast.
- 4 stages rendered; 3 Split gateways visible.
- Stages have distinct Y positions — top-to-bottom layout correct.
- Route paths (`.edge-path`) render from stages through gateways.
- All stages carry the correct `data-prism-lane` attribute (see fix below).

### ✅ Community Enquiry (migrated format)
- Loaded without errors.
- 2 stages rendered; 1 Split gateway (`route-submitted`) visible.
- Gateway is a pill (single-route Split) — text shows trigger label `submit`, not the displayName.
- `aria-label` correctly contains `"Route to submitted"` — accessible and correct.
- Lane band renders correctly.

### ✅ Information Request (migrated format — with Join gateway)
- Loaded without errors.
- 3 stages rendered across 2 lane bands (applicant + caseworker).
- Both Split and Join gateways visible (`caseworker-route` Split, `review-complete` Join).
- `caseworker-route` gateway carries `data-prism-lane="caseworker"` — queue attribution is correct.
- Distinct Y positions confirmed — Join DAG layout flows top-to-bottom without cycles.

---

## Fix Applied: `data-prism-lane` on Stage Button Elements

**File:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`

Stage `<button>` elements now carry `data-prism-lane=${layout.laneKey}`, consistent with how gateway buttons expose `data-prism-lane`. This enables test selectors like `[data-prism-stage][data-prism-lane="caseworker"]` to verify lane attribution without relying on DOM ancestry (which is not the canvas structure — stages are absolutely-positioned siblings, not children, of lane `<section>` elements).

---

## Key Structural Insight (Test Architecture Decision)

**Stages are NOT DOM children of lane bands.** The canvas renders lane `<section data-prism-role-lane>` elements and stage nodes as siblings in a flat absolutely-positioned canvas. Any test that queries `laneBand.locator('[data-prism-stage]')` will always return 0. Tests must verify lane membership via the `data-prism-lane` attribute on stage/gateway buttons, not DOM ancestry.

---

## Test Results

**Spec:** `tests/workflow-editor/workflow-migrated-workflows.spec.ts`  
**15 new tests, all green.**

| Workflow | Tests |
|----------|-------|
| Planning | 5 tests ✅ |
| Community Enquiry | 4 tests ✅ |
| Information Request | 6 tests ✅ |

Full suite: **90 passed**, 8 pre-existing flaky (network/localhost-dependent), 71 skipped (auth session tests). No regressions introduced.

---

## Decisions

1. **`data-prism-lane` is now a first-class attribute on stage buttons** — treat it as a stable Playwright selector contract alongside `data-prism-stage` and `data-prism-gateway`.

2. **Single-route pill gateways show the trigger label as visible text, not the displayName.** The displayName is in `aria-label`. Tests must check `aria-label` (not `textContent`) to verify gateway title hydration for pill gateways.

3. **DOM ancestry cannot verify lane membership.** Always use `data-prism-lane` attribute on stage/gateway nodes for lane placement assertions.
