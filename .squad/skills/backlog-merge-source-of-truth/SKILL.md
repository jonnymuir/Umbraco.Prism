---
name: "backlog-merge-source-of-truth"
description: "Collapse adjacent backlog slices into one active track and make the earliest surviving issue plus design doc the explicit source of truth"
domain: "planning"
confidence: "high"
source: "observed (2026-05-25T15:34:44.680+01:00 merged workflow gateway/runtime track)"
---

## Context

Use this when separate backlog issues looked reasonable at planning time, but a later product decision makes them too interdependent to execute safely as standalone slices.

## Patterns

- Keep the earliest surviving issue open as the active track unless there is a stronger existing umbrella.
- Edit that surviving issue so it explicitly absorbs the other issue numbers and carries the full merged contract.
- Close the absorbed issues with comments that point back to the surviving track so the backlog no longer implies parallel independent execution.
- Update the canonical design doc in the same session so issue text and design text tell the same story.
- Write the internal sequence and team boundaries inside the merged track instead of pretending the work is now unordered.
- Keep one shared list of green gates for the whole merged slice.

## Anti-Patterns

- Leaving absorbed issues open with their old titles and owners after the merge decision.
- Updating only the issue tracker or only the design doc and forcing the team to reconcile the difference by hand.
- Merging slices without stating which agent owns visual model work, runtime work, and proof work.
