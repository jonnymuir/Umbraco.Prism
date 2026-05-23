# Decision: Lane Header Clearance & Viewport Background Width — Proof Tests

**Author:** Tangy (Tester)  
**Date:** 2026-05-28  
**Status:** ✅ PROOFS WRITTEN AND VALIDATED

---

## Context

A screenshot was provided showing two distinct visual regressions in the workflow editor:

1. **Stage cards crashing into the lane heading / copy text area** — stage node buttons overlapping the role heading and descriptive copy at the top of each lane column.
2. **The bordered `.graph-viewport` background not expanding far enough right** — the visual border and background of the graph viewport ended before the rightmost "Reviewer" lane, leaving it visually orphaned from the styled surface.

Both regressions required **measured DOM geometry** proof tests rather than pixel snapshots, consistent with the established testing methodology for this editor.

---

## Proof 1: Lane Header Clearance

**Describe block:** `"Graph layout proof: lane header clearance (stage must not intrude into heading/copy)"`  
**File:** `tests/workflow-editor/workflow-graph-layout-proof.spec.ts`  
**Story:** `workflow-editor-workflow-graph--workspace-canvas` (2-lane WORKSPACE_WORKFLOW)

### Layout geometry (measured at test time)

| Element | Position from scene origin |
|---------|---------------------------|
| Lane top | 64px (`TOP_PADDING`) |
| Lane heading bottom | ~104px |
| Lane copy bottom | ~124px |
| First stage top | 144px (`TOP_PADDING + LANE_HEADER_OFFSET = 64 + 80`) |
| **Breathing gap** | **20px** |

### Assertions

- Test 1: `firstStageTop >= laneHeaderBottom` AND `firstStageTop >= laneCopyBottom` (per lane)
- Test 2: Gap = `firstStageTop - copyBottom >= 4px` minimum breathing room

### Result: ✅ PASS (regression appears fixed)

The screenshot was taken against an older version where `LANE_HEADER_OFFSET = 44` (stage at 108px, copy bottom at ~124px → 16px **overlap**). Isabelle has since updated `LANE_HEADER_OFFSET` to **80** (stage at 144px → 20px clear). The proof tests now pass, confirming the fix is correct, and will act as a regression guard going forward.

---

## Proof 2: Viewport Background Encompasses Rightmost Lane (Shell Context)

**Describe block:** `"Graph layout proof: viewport background extends to encompass rightmost lane (shell context)"`  
**File:** `tests/workflow-editor/workflow-graph-layout-proof.spec.ts`  
**Story:** `workflow-editor-editor-shell--reference-shell` switched to `information-request` (3 lanes)

### Why the shell context matters

The standalone graph story has no outer `overflow: hidden` constraint, so the canvas expands freely to match the scene-frame width. The bug only manifests in the **shell**, where a CSS grid (`outline + 1fr + inspector`) with `overflow: hidden` constrains the graph area.

At 1440px viewport with both panels open:
- Shell graph column = 1440 − 240 (outline) − 380 (inspector) = **820px**
- 3-lane scene-frame width = 56×2 + 3×280 + 2×36 = **1024px**
- Theoretical shortfall: 1024 − 820 = **204px** of rightmost lane uncovered

### Assertions

- PROOF 1: `viewport.clientWidth >= sceneFrame.offsetWidth` — painted background must cover full scene-frame width
- PROOF 2: `canvas.scrollWidth >= sceneFrame.offsetWidth` — user must be able to scroll to rightmost lane

### Result: ✅ PASS (regression appears fixed or not manifesting as theorised)

Measured values in shell with `information-request` (3-lane):
- `sceneFrame.offsetWidth = 1024px`
- `viewport.clientWidth = 1024px` ← background covers full scene
- `canvas.clientWidth = 832px` ← shell column is indeed constrained
- `canvas.scrollWidth = 1058px` ← scrollable to rightmost lane content

The `.graph-viewport` (with `overflow: visible`) appears to resolve its `width: 100%` against the scroll content width rather than the canvas's visible area in Chromium — meaning the background IS painted at 1024px even when the canvas is only 832px. The user CAN scroll right to reach hidden lanes (`scrollWidth > sceneFrame`). The proof tests now pass, and serve as a regression guard against any future change that breaks either invariant.

---

## Testing Methodology Note

Both proofs use measured DOM geometry (`.clientWidth`, `.offsetWidth`, `.scrollWidth`, `getBoundingClientRect()`), not pixel snapshots. This correctly handles zoom, scroll, and layout boxes that visual screenshots cannot reliably measure. The shell context is required for the viewport proof — the standalone graph story does not reproduce the overflow constraint.

---

## Semantic hooks for Isabelle (if needed in future)

If either proof starts failing:

1. **Lane header clearance fails:** Check `LANE_HEADER_OFFSET` in `prism-workflow-graph.ts`. The stage Y = `TOP_PADDING + LANE_HEADER_OFFSET`. Must satisfy `TOP_PADDING + LANE_HEADER_OFFSET > TOP_PADDING + laneInternalPadding + headingHeight + marginTop + copyHeight`.

2. **Viewport background fails:** Check `.graph-viewport` CSS. It must either:
   - Use `min-width: max-content` so its box expands to scene-frame content, or
   - Use `display: inline-block` or similar to size to content width, or
   - Be absolutely positioned with explicit width matching scene-frame — whatever mechanism currently allows `viewport.clientWidth = sceneFrame.offsetWidth` in the scroll container context must be preserved.
