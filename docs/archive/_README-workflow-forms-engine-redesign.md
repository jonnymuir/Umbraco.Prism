# v1→v2 Redesign (Archived)

> This is a frozen historical snapshot from before the [Service Design vocabulary rename](../../CLAUDE.md) — it still uses the original "Workflow" terminology throughout ("workflow definitions", `WorkflowDefinitionFile`, etc.), which has since been renamed to the Service Blueprint / Service Request / Stage vocabulary described in CLAUDE.md. Left as-written to preserve what was actually true when it was written; it is not being kept in sync going forward.

This document (`workflow-forms-engine-redesign.md`) was the v1→v2 transition plan proposing the shift from flat `fields[]` arrays to polymorphic component trees. It has been **fully implemented** in v2.0 and is archived here for historical context.

**For current v2 architecture,** see:
- [service-request-forms-engine.md](../design/service-request-forms-engine.md) - Current v2 architecture
- [service-request-forms-engine-backend.md](../design/service-request-forms-engine-backend.md) - Backend implementation
- [service-request-forms-engine-client.md](../design/service-request-forms-engine-client.md) - Client rendering
- [Walkthroughs](../walkthroughs/) - Live examples of service blueprints

**What changed from this proposal:**
- ✅ Polymorphic component model implemented as proposed
- ✅ Fluent builder API (`WorkflowDefinitionBuilder`) implemented
- ✅ Type discriminators via `type` property implemented
- ❌ Element Types approach not adopted — kept JSON-driven workflow definitions
- ❌ Generic `ConditionalOn`/`VisibleWhen` deferred to v2.1 — v2.0 ships with `ConditionalChildren` on Radios/Checkboxes only

**Archived:** April 2026
