# Decision: Start issue #74 with horizontal role-lane scaffolding on the existing editor shell

**Date:** 2026-05-22T19:00:07.321+01:00  
**Author:** Isabelle  
**Status:** Proposed  

## Decision

After issue cleanup, start the workflow editor UX work with the smallest frontend-first slice inside #74: replace the current front-stage/back-stage canvas framing with **horizontal role-first swim lanes (one role per row)** while keeping the existing right-hand inspector, validation rail, preview panel, simulation panel, and list fallback intact.

Treat `.squad/decisions.md` as the source of truth for lane orientation: the locked model is **horizontal stacked role bands**, not vertical columns.

## Why

- Main now has the shared authored workflow contract from PR #75, so the canvas can group stages by stable actor/role data without waiting for more schema work.
- The current editor already has working seams for selection, inspector drill-in, validation, preview, simulation, undo/redo, and list fallback; changing the canvas grouping first gives visible UX progress without reopening every editor subsystem at once.
- This slice is small but meaningful: it moves the product toward the locked role-first mental model immediately, while preserving the current accessible fallback and confidence tooling.

## Consequences

- First implementation scope should be limited to lane scaffolding, stage-card placement by role, lane headings/copy, and transition routing across lanes.
- Do **not** combine this first slice with inspector redesign, validation-tab moves, proposal UI work, or new authoring behaviours.
- Keep the current list workspace as the structural fallback and regression guard while the swim-lane canvas changes.
- When work starts, note that issue #74 text should be interpreted through the later decisions log because the issue body still mentions vertical role bands while the accepted decision rejects vertical swim lanes.
