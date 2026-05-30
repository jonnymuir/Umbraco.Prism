# Slice 7.5 — Clear the three visual bugs Tangy flagged in Slice 7

**By:** Isabelle
**Date:** 2026-05-30
**Branch:** `squad/82-named-lanes-editor-slice`
**Scope:** Three small visual regressions Tangy filed against Slice 7's
canonical baselines. Pure frontend; no backend changes.

## Summary

Resolved the three visual regressions Tangy flagged in
`.squad/decisions/inbox/tangy-slice7-visual-regression-strategy.md`
(BUG-VR-1/2/3) before Slice 8 ships, and un-fixme'd the one Playwright
spec that was held back as the canary for sticky lane headers.

## Fixes landed

### BUG-VR-2 — Stale "transitions" caption + dead `T` shortcut entry

- **Where:** `src/workflow-editor/prism-workflow-graph.ts` (`.graph-hint`),
  `src/workflow-editor/workflow-shortcuts.ts` (`add-transition`, `paste`).
- **Change:** Replaced the caption with gateway-first author language:
  *"Tab through role bands, stage cards, and gateway nodes. Enter selects
  a node, E opens the inspector to edit it (including a gateway's
  outgoing routes), and Shift+F10 opens the context menu."* No `T`
  shortcut is mentioned because there isn't one any more.
- **Dead-code cleanup:** removed the `add-transition` (`T = Create a
  route`) entry from `WORKFLOW_SHORTCUT_GROUPS`. It was un-wired since
  Slice 3b.1 retired transition creation; it only surfaced (misleadingly)
  in the help dialog. Also retired "Selected stage or transition" → "…or
  route" on the paste shortcut context. `grep` confirms no production
  code or test references `add-transition`.

### BUG-VR-3 — `MULTI_LANE_FAN_OUT` story height clipped the baseline

- **Where:** `src/workflow-editor/prism-workflow-graph.stories.ts` →
  `GatewayRepresentation`.
- **Change:** Overrode `render` for this single story to set
  `height: 1080px` (default from `makeElement` is 560px). The full
  fan-out (start → split → 3-stage branch row → join → decision-confirmed)
  now renders inside frame.
- **Why per-story override:** bumping `makeElement` globally would
  invalidate every layout-proof baseline outside Slice 7's suite — Tangy
  explicitly avoided that path in Slice 7. The visual-suite specs that
  share this story (`workflow-graph-layout-proof.spec.ts`) only assert
  numeric DOM geometry — no screenshots — and pass unchanged.
- **Baseline regen:** ran
  `npx playwright test tests/workflow-editor/workflow-canvas-arrows.spec.ts
  --update-snapshots`. Only `MULTI-LANE-FAN-OUT.png` updated;
  `SINGLE-LANE-LINEAR` and `SAME-LANE-FAN-OUT` were byte-identical and
  not rewritten. The new baseline was reviewed visually before commit.

### BUG-VR-1 — Sticky lane headers

- **Where:** `src/workflow-editor/prism-workflow-graph.ts` → `.lane-header`
  CSS, plus `tests/workflow-editor/workflow-canvas-scroll.spec.ts` to
  un-fixme the spec.
- **Change:** `position: sticky; top: ${TOP_PADDING + 18}px; z-index: 5;
  background: inherit;`. The `+ 18` matches the lane's `padding-top` so
  the header's viewport position is **invariant** through scrolling
  (`bbox.top` before == `bbox.top` after; measured drift: 0px after a
  250px vertical scroll, well inside Tangy's 4px tolerance).
- **z-index 5** keeps the sticky strip above stage cards and the
  `<svg class="graph-edges">` sibling, neither of which set z-index.
- **`background: inherit`** keeps the strip visually merged with its
  parent lane variant (primary vs supporting) without redeclaring
  colours.

## Why "sticky `top: TOP_PADDING + 18px`" and not "sticky `top: 0`"

The lane is `position: absolute; top: 64px` inside `.graph-viewport`,
with `padding: 18px 20px`. The header's natural offset from the scrolling
ancestor (`.graph-canvas`) is therefore 82px. If sticky were `top: 0`,
the header would *jump 82px up* on first scroll — visually jarring and
breaks any "header position unchanged" assertion. Setting `top: 82px`
keeps the header anchored at its own initial position, so scrolling
content slides under a header that doesn't move. This is the UX the user
called out ("horizontal and vertical scrolling works well") and the
contract Tangy's spec measures.

## Verification

- `tests/workflow-editor/` Playwright sweep (Chromium, viewport 1440×900):
  **88 passed, 11 skipped** (was 87/12; the un-fixme'd
  `LARGE_WORKFLOW: lane header strip stays sticky during vertical scroll`
  now passes). 0 unexpected failures.
- `npm run build` ✅, `npm run build-storybook` ✅,
  `dotnet build UmbracoPrism.sln` ✅ (0 warnings, 0 errors).
- All three new baselines from Slice 7 still hold; only
  `MULTI-LANE-FAN-OUT.png` was regenerated (intentional, BUG-VR-3).

## Out of scope (deliberately not touched)

- Slice 8 — docs / write-surface consolidation.
- Any backend changes.
- The 11 remaining `test.fixme` markers across `workflow-editor-shell`,
  `workflow-overflow-responsive`, etc. — they target separate behavioural
  hooks Isabelle has not yet built and are not part of Slice 7's contract.
- Implementation-level `'transition'` identifiers inside `prism-step-inspector`
  / wire-fields — already parked under Slice 3b.2 (`WorkflowSelection`
  union collapse).
