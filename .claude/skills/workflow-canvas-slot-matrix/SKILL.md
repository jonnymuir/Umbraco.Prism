---
name: "workflow-canvas-slot-matrix"
description: "Lay out gateway-first workflow canvases with lane row-bands, local slots, and corridor routing"
domain: "frontend"
confidence: "high"
source: "observed (2026-05-26T19:40:31.679+01:00 horizontal lane-column canvas critique)"
---

## Context

Use this when a workflow editor needs to show stages and gateways across lanes without overlapping nodes or turning the canvas into freeform spaghetti.

## Pattern

1. Keep lanes as the primary columns.
2. Compute vertical **row bands** from graph structure, not authored array order.
3. Within each lane and row band, allocate one or more **local slots**.
4. Put ordinary single-path nodes in the centre slot.
5. When one node fans out to multiple same-lane gateways, place those gateways as sibling slots in the next row band.
6. When branches cross lanes, place targets in their destination lane but keep them in the same next row band when they are part of the same fan-out.
7. Route edges through reserved corridors: vertical out of source, horizontal between lanes/bands, vertical into target.
8. Put join gateways in the convergence row band and place released downstream nodes in the next row band below.
9. Keep the Canvas visually light: validation details stay in the Validation surface; the canvas should show only compact status cues.
10. Size each lane from its widest row band instead of a fixed column width so sibling gateway slots and same-lane branch stages never collide.
11. In gateway-first mode, draw **unique node-to-node rails** from the adjacency graph (stage → gateway, gateway → stage) instead of redrawing the full authored transition for every branch.
12. Offset sibling exit and entry slots across the stage/gateway face so same-lane choices leave through separate corridors instead of stacking on one vertical stem.
13. Stop incoming join branches at the join boundary, then draw one downstream trunk from the join to the released stage; never run every incoming branch through the join body.
14. Do not duplicate lane ownership inside every node card when the lane header already states the role; keep lane context in the header and ARIA labels instead.
15. Keep stage cards and gateway diamonds visually quiet by default; validation detail belongs in the Validation surface, not on the canvas.
16. Use **contextual ghost create placeholders** instead of permanent empty-slot controls:
    - show them only for the selected node's next valid slot, the focused empty slot, or the active branch endpoint
    - keep the default state low-contrast and dashed
    - use a ghost card silhouette for stage insertion and a ghost diamond silhouette for gateway insertion
    - never light up every empty slot in the lane at once

## Why it helps

This gives authors one stable reading order: stage, gateway row, branch rows, join row, next stage row. It scales from a simple stage → gateway handoff to multi-lane split/join flows without relying on brittle post-placement nudges.

## Behavioural proof

When you need to validate this layout, use measured DOM geometry instead of screenshot-only checks:

1. Add one same-lane fan-out fixture with sibling routing choices anchored to the same stage.
2. Add one cross-lane fan-out fixture with branch work in separate lanes converging on a join.
3. Assert that Canvas does not repeat Validation detail copy.
4. Assert that sibling routing choices do not overlap and sit in the same next row band.
5. Assert that branch stages sit above the join and the downstream stage stays below the join row.
6. Assert that same-source route rails leave on distinct x-coordinates when multiple same-lane choices exist.
7. Assert that stage → join rails terminate at the join edge and that the join emits a separate downstream trunk.
8. Assert that duplicate lane-role copy does not appear inside stage/gateway cards when the lane header is already visible.
9. Assert that ghost create placeholders only appear for the active context, not for all empty slots in view.
