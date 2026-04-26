# v1→v2 Redesign (Archived)

This document (`workflow-forms-engine-redesign.md`) was the v1→v2 transition plan proposing the shift from flat `fields[]` arrays to polymorphic component trees. It has been **fully implemented** in v2.0 and is archived here for historical context.

**For current v2 architecture,** see:
- [workflow-forms-engine.md](../design/workflow-forms-engine.md) - Current v2 architecture
- [workflow-forms-engine-backend.md](../design/workflow-forms-engine-backend.md) - Backend implementation
- [workflow-forms-engine-client.md](../design/workflow-forms-engine-client.md) - Client rendering
- [Workflow walkthroughs](../walkthroughs/) - Live examples of v2 workflows

**What changed from this proposal:**
- ✅ Polymorphic component model implemented as proposed
- ✅ Fluent builder API (`WorkflowDefinitionBuilder`) implemented
- ✅ Type discriminators via `type` property implemented
- ❌ Element Types approach not adopted — kept JSON-driven workflow definitions
- ❌ Generic `ConditionalOn`/`VisibleWhen` deferred to v2.1 — v2.0 ships with `ConditionalChildren` on Radios/Checkboxes only

**Archived:** April 2026
