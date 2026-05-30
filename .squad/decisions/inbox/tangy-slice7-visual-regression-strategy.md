# Slice 7 — Visual regression strategy + opening suite

**By:** Tangy
**Date:** 2026-05-30
**Branch:** `squad/82-named-lanes-editor-slice`
**Scope:** Visual test strategy for the workflow editor canvas + opening
  implementation set.

## Summary

Landed the visual regression test strategy doc and the opening implementation
set the user mandated on 2026-05-30 (`copilot-directive-20260530T132645Z.md`,
concern 2). The suite covers the five user-named concerns with deliberately
few, sharp tests — DOM geometry first, screenshots only where a human eye
genuinely catches things geometry doesn't.

## Deliverables landed

- **Strategy doc:** `docs/testing/workflow-editor-visual-tests.md` — names
  the five concerns, what is explicitly out of scope (cross-browser, pixel
  styling), tooling, baseline management, flake budget (0%), the four
  canonical scenarios, and the data-attribute contract the suite leans on.
- **Implementation:** six new spec files under
  `src/UmbracoPrism.Client/tests/workflow-editor/`:
  - `workflow-canvas-lane-fit.spec.ts` (4 tests — one per scenario)
  - `workflow-canvas-no-overlap.spec.ts` (4 tests)
  - `workflow-canvas-text-fits.spec.ts` (4 tests)
  - `workflow-canvas-scroll.spec.ts` (4 tests, one fixme — see below)
  - `workflow-canvas-arrows.spec.ts` (4 DOM endpoint tests + 3 screenshot
    baselines covering SINGLE_LANE_LINEAR, MULTI_LANE_FAN_OUT,
    SAME_LANE_FAN_OUT — LARGE_WORKFLOW is covered by DOM scroll specs)
  - `workflow-editor-ergonomics.spec.ts` (3 tests)
- **Shared helpers:** `tests/workflow-editor/support/canvas-helpers.ts`
  with the `CANONICAL_SCENARIOS` registry, `measureGraph()`, and
  `gotoCanonicalScenario()`. Pinned `viewport: 1440x900` for all visual
  specs.
- **New canonical scenario:** `LargeWorkflow` story
  (`workflow-editor-workflow-graph--large-workflow`) — synthetic
  5-lane × 8-stage workflow used by scroll + invariant specs.
- **Screenshot baselines:**
  `tests/__screenshots__/workflow-editor/workflow-canvas-arrows.spec.ts/{SINGLE-LANE-LINEAR,MULTI-LANE-FAN-OUT,SAME-LANE-FAN-OUT}.png`,
  each at 1440×900 with `animations: 'disabled'` and
  `maxDiffPixelRatio: 0.02`.
- **README:** `src/UmbracoPrism.Client/src/workflow-editor/README.md`
  gained a Visual testing section pointing at the strategy doc and
  listing the data-attribute contract.

## Test count delta

| Surface | Before | After | Delta |
|---|---|---|---|
| Visual specs (this slice) | 0 | 26 (25 passing + 1 fixme) | +26 |
| Pre-existing workflow-editor specs (sampled) | green | green | 0 |

The suite passes twice in a row with no flake. All screenshot specs use
`animations: 'disabled'` and wait for `networkidle` before snapping.

## Visual bugs flagged for follow-up

These were discovered by running the new suite against current `HEAD`
(3ca28a4) on `squad/82-named-lanes-editor-slice`. None of them blocks
landing this slice; all should be fixed by **Isabelle** before Slice 8
ships, because they directly contradict the user's mandate language.

### 🟥 BUG-VR-1 — Lane headers are not sticky during vertical scroll

**Where:** `prism-workflow-graph.ts`, `.lane-header` selector
(`[data-prism-lane-header]`).

**Evidence:** `workflow-canvas-scroll.spec.ts` →
`LARGE_WORKFLOW: lane header strip stays sticky during vertical scroll`
(currently `test.fixme`). Computed style is `position: static`; after
a 250 px vertical scroll inside `.graph-canvas` the lane header drifts
exactly 250 px out of view.

**Why it matters:** The user explicitly called out scroll behaviour
("horizontal and vertical scrolling works well"). Without sticky lane
headers, an author scrolling a tall workflow loses track of which lane
owns the work currently in view — that breaks the *primary* reason lanes
exist as a reading device.

**Suggested fix:** `position: sticky; top: 0; z-index: 2;` on
`.lane-header` (inside `.graph-canvas`'s overflow context). When the
fix lands, flip `test.fixme` → `test` in the scroll spec.

### 🟧 BUG-VR-2 — Stale "transitions" language in the canvas instruction caption

**Where:** Canvas help caption above the graph scene (visible in every
canonical screenshot). Reads:

> "Tab through role bands, stage cards, transition chips, and transition
> handles. Enter selects, T opens transition creation, E opens the
> inspector, and Shift+F10 opens the context menu."

**Why it matters:** Slices 3a/3b/3c collapsed the editor to
**stages + gateways** and explicitly retired user-facing "transitions"
language. "T opens transition creation" is a keyboard hint that no
longer matches what the editor does (gateways are the routing primitive
now). This is a label-leak regression visible to every author who opens
the canvas.

**Suggested fix:** Update the caption to talk about *stages* and
*gateways*. Cross-check `workflow-shortcuts.ts` for any remaining
"T = transition" binding and either retire it or rename it to "G = new
gateway" if a single-key shortcut for routing is still wanted.

### 🟨 BUG-VR-3 — `MULTI_LANE_FAN_OUT` canonical layout starts below the fold in a 560 px story

**Where:** `prism-workflow-graph.stories.ts` story height
(`height:560px`) vs the `LEAVE_REQUEST_STARTER_WORKFLOW` shape.

**Evidence:** `MULTI-LANE-FAN-OUT.png` baseline shows only the
`start-request` stage and the top half of the `review-split` gateway —
the reviewer lane is empty in the visible viewport because the
reviewer-assessment stage sits below the fold.

**Why it matters:** Authors opening the canonical "real workflow" story
see only one stage on initial render. Not a runtime bug, but it makes
both the demo and the screenshot baseline less informative.

**Suggested fix (Isabelle or Tom Nook to route):** either bump the
graph stories' default `height` to ~800 px, or rearrange the fixture so
the first row of every lane is visible at 560 px. I deliberately did
**not** edit the story height in this slice — it would invalidate
every existing layout-proof baseline outside the new suite.

## Data-attribute contract the visual suite now depends on

| Attribute | Purpose |
|---|---|
| `data-prism-component="workflow-graph"` | Graph root marker |
| `data-prism-mode="graph"` | Workspace mode |
| `data-prism-read-only="true|false"` | Read-only viewer |
| `data-prism-lane-container=<laneKey>` | Lane bounding box |
| `data-prism-lane-header=<laneKey>` | Sticky-header scroll spec |
| `data-prism-stage-card=<stageKey>` | Stage bounding box |
| `data-prism-stage=<stageKey>` | Stage click target / label container |
| `data-prism-gateway-node=<gatewayKey>` | Gateway bounding box |
| `data-prism-gateway=<gatewayKey>` | Gateway click target / label container |
| `data-prism-route-path=<key>` | SVG route path (endpoint assertion) |
| `data-prism-route-from=<key>` / `data-prism-route-to=<key>` | Route endpoint mapping |

Listed for the Scribe so the contract makes it into `decisions.md`.

## What's intentionally *not* in this slice

- Cross-browser (Firefox/WebKit) snapshots — Chromium only.
- A screenshot baseline for `LARGE_WORKFLOW` — covered by DOM scroll
  specs; a long thin scrollable image would dominate the baseline
  budget for low signal.
- Re-introduction of the retired Umbraco backoffice editor.
- Any backend changes.
- Fixes for BUG-VR-1/2/3 — those are flagged for Isabelle (or the
  coordinator to route) before Slice 8.

## Suggested coordinator routing

1. Route **BUG-VR-1** (sticky lane headers) and **BUG-VR-2** (stale
   transitions caption) to Isabelle as a small Slice 7.5 / pre-Slice 8
   fix. Both are small, both directly improve author trust in the canvas.
2. Route **BUG-VR-3** at the same time if you want the canonical
   screenshot baseline to be more representative; otherwise it can wait.
3. Once BUG-VR-1 lands, flip `test.fixme` → `test` in
   `workflow-canvas-scroll.spec.ts`.
