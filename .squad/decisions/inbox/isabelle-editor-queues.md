---
date: 2026-06-01
author: Isabelle
---

# Decision: Editor host wiring is queue-first

## What changed

- The shared workflow editor and shell now accept `availableQueues` from the host setup.
- Queue labels and picker options now come from that host-supplied queue catalog first, with authored workflow data only as fallback.
- Author-facing editor copy now talks about queues instead of lanes where the editor surface or host-facing API exposed that concept.

## Why

Jonny asked for the editor slice to treat stage and gateway ownership as queue-based without baking TestSite or MockBusinessApp assumptions into shared code. This keeps the editor generic while letting reference hosts demonstrate their own queue wiring.

## Follow-up

- Internal helper/type names still use some `lane*` identifiers where that does not leak through the host or authoring surface.
- Runtime authorization and queue access rules remain out of scope for this slice.
