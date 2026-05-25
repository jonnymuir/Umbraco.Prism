# Tom Nook — Gateway lane slice decision

- **Date:** 2026-05-25T14:17:36.055+01:00
- **Issue context:** #82 → #84
- **Branch:** `squad/82-named-lanes-editor-slice`

## Decision

After #82, the safest next behavioural slice is **editor representation only** for split and join gateways.

Gateways should become visible, selectable, lane-owned items in the editor so authors can read branch and merge intent clearly. The current executable workflow path must remain stage-to-stage until the later slices for lane-owned joins and concurrent runtime behaviour land.

## Implement next

- Render split and join gateways as distinct lane-owned items in the editor.
- Show gateway title, kind, and owning lane in the inspector.
- Make branch and merge direction readable across lanes.
- Keep current preview, simulation, publish, and runtime behaviour stage-driven.

## Defer

- Replacing waiting-stage runtime behaviour with join-gateway runtime behaviour (#84).
- Independent cursors, deterministic join release, and concurrency bookkeeping (#85).
- Any requirement that existing workflows must route through executable gateways before current end-to-end behaviour is preserved.

## Quality gate

The .NET workflow suite is green on this branch via `dotnet test UmbracoPrism.sln`.

The targeted workflow editor Playwright suite is **not fully green yet** on this branch: `workflow-editor-history.spec.ts` and `workflow-editor-simulation.spec.ts` currently fail because the expected history/simulation controls are not visible in the current editor surface. Returning those tests to green is a prerequisite for landing the gateway representation slice, and they must remain green as the UI changes.
