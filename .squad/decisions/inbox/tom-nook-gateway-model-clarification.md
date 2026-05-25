## 2026-05-25T15:23:06.241+01:00 — Gateway model clarification

### Context

User clarification tightened the intended multi-lane workflow model. The existing direction was already moving from waiting stages to split/join gateways, but the authored shape needed to be stated more plainly so implementation work does not drift.

### Decision

Treat the authored workflow as two distinct node types:

- **Stages** carry user-facing work and actions such as forms, reviews, confirmations, and system steps.
- **Transition gateways** are diamond-shaped routing nodes with a name and description. They own branch, merge, and wait semantics.

For transition routing:

- Transitions may target a **stage** or a **gateway**
- Gateways may route to **stages** or to other **gateways**
- Simple stage-to-stage paths remain valid for straightforward flows

For joins and waiting:

- Join-style waiting happens at the **join gateway**, not at a fake waiting stage
- The join gateway owns the waiting copy and runtime waiting information
- If the join is waiting for multiple arriving cursors, the user should see that waiting state in the same way earlier waiting-state UX did, but sourced from the gateway

### Issue-sequence impact

No reorder is needed. Keep the existing sequence, but interpret the next issues like this:

1. **#83** — lock the editor UX for diamond gateways, stage/gateway links, and readable join waiting intent
2. **#84** — replace waiting-stage authoring/runtime semantics with join-gateway waiting semantics
3. **#85** — implement multi-cursor join release rules and deterministic runtime behaviour

### Why

This is mostly a clarification and correction of under-specified modelling language, not a change of product direction. The architecture was already converging on gateways and joins; the user has now made explicit that stages remain action-bearing work nodes while gateways are the branch/wait control points.
