# Isabelle — Slice 5: canvas slot-matrix layout

**Date:** 2026-05-30
**Branch:** `squad/82-named-lanes-editor-slice`
**Owner:** Isabelle (frontend / a11y)

## What changed

The workflow canvas now lays nodes out as a **slot matrix** instead of the
ad-hoc per-lane stack the editor inherited from the linear-mode era.

### Layout primitives (`prism-workflow-graph.ts`)

- `ROW_BAND_PITCH = 152` — vertical pitch between adjacent rank bands
- `LANE_INSET = 28` — left/right inset inside a lane column
- `SLOT_GAP = 56` — horizontal gap between sibling slots inside a lane
- `GATEWAY_TRUNK = 36` — vertical trunk above/below a gateway diamond

### Node ranking

A pure adjacency graph is built from the authored gateway+transition
metadata, then a Kahn topological sort assigns row-ranks. A parity step
keeps stages on **even** ranks and gateways on **odd** ranks so the canvas
always reads `stage → gateway → stage` top-to-bottom. Lane width
auto-widens to the widest row band so siblings sit in distinct slot
columns rather than stacking.

### Routing

Routes are now orthogonal Manhattan rails rendered as
`[data-prism-route-path]` SVG paths (new Slice 7 hook), with sibling
outgoing rails leaving on distinct x-corridors via `_slotOffset`.
Transition chip paths still carry the existing
`data-prism-transition-from/-to/-path` selectors so the chip-label
interaction model is unchanged.

## Invariants enforced (Playwright)

`tests/workflow-editor/workflow-graph-layout-proof.spec.ts`:

1. Lanes render as **separate vertical columns** (right < next.left,
   height > width).
2. **Same-lane fan-out** widens the lane and gives each branch its own
   slot column — sibling routes do not stack.
3. **Cross-lane fan-out** keeps the branch row aligned (≤24px y-delta)
   between lanes, with the join gateway sitting strictly below all
   branch stages and above the next downstream stage.
4. **No overlap** between any pair of nodes across both gateway-rep and
   same-lane-fan-out stories.
5. **Every node sits inside its lane** (within ±2px tolerance) — no
   bleeding over lane boundaries.

## Other changes

- `LEAVE_REQUEST_STARTER_WORKFLOW` + `cloneAuthoredWorkflow()` added to
  `fixtures/index.ts` and reused by the gateway story in both the graph
  and editor-host story files. New `SAME_LANE_FAN_OUT_WORKFLOW` story
  feeds the slot-matrix proof.
- `[data-prism-canvas-health-hint]` strip lives below the editor
  statusbar; surfaces validation issue counts and an
  `[data-prism-open-validation]` button that switches the confidence
  tab to Validation. (Required by the validation rail spec.)
- Empty-state copy now includes "Add the next stage before you branch"
  in the tips list.
- Retired the orphan `list mode displays stages in editable table…` test
  and the screenshot-baseline tests (Slice 4 retired list mode; visual
  regression is owned by Slice 7).

## Deferred

- **Slice 7 visual regression** — full screenshot baselines.
- **JSON twin-pane editor** — outside scope.
- **Outline `Move up/down` / `Alt+Arrow`** — already preserved in
  `prism-workflow-editor.ts` outline rail; untouched by this slice.

## Recommendation

`stash@{0}` (`slice-5-canvas-slot-matrix`) was used as a design
reference and is now superseded by this commit. **Recommend dropping
the stash** at next session start (`git stash drop stash@{0}`) once a
human confirms the Slice 5 work is merged.

— Isabelle
