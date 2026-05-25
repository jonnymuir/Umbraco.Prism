## 2026-05-25T15:23:06.241+01:00 — Gateway UX clarification

### Context

The user clarified that stages remain the places where work happens, while gateways are separate transition nodes. The current #83 editor slice already makes gateways visible, but it still treats them too much like read-only ornaments attached to stage-to-stage routing.

### Decision

Treat the current #83 UI as **partial scaffolding, not the correct finished UX**.

The intended authoring model is:

- **Stages** are action-bearing nodes where forms, review steps, confirmations, and other work live.
- **Transition gateways** are distinct **diamond** routing nodes with their own **name** and **description**.
- **Transitions** may connect **stage → gateway**, **gateway → stage**, or **gateway → gateway**.
- **Join gateways** own the waiting story, including waiting copy and runtime-facing information about what is still outstanding.

### Required UX implications

For the workflow editor to feel correct, the next gateway UX must let authors:

1. create a stage or gateway directly from the canvas without awkward placeholder stages
2. connect stages and gateways with clear, readable branch and merge lines
3. inspect and edit gateway name, description, lane owner, and waiting information
4. understand which lane owns each node and which incoming paths a join is waiting on
5. do the above with keyboard-accessible creation, selection, inspection, and focus feedback

### Why

If we leave gateways as read-only markers anchored near stage-to-stage links, authors will still have to think in the old stage-only model. That is good enough for a temporary representation slice, but it is not the UX the user just described and should not be treated as the target design for #83.
