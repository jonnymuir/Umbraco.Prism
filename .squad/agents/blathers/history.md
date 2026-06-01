## 2026-06-01 — Queue-Model Slice: Runtime Queue Access & Host Boundary

**Session:** queue-model slice  
**Branch:** Queue runtime implementation  
**Commit:** 47fadab

**Task:** Add host-defined queue access and runtime projection; rework payment demo tests; establish clean shared/host boundary.

**Status:** ✅ Complete

### What Changed

- **Shared workflow runtime:** Now accepts `WorkflowAccessProfile` from host to decide which queues can be started, viewed, and transitioned.
- **Shared definitions:** Stages and gateways now carry explicit `queueName` field (set by authoring layer, read by runtime).
- **Host responsibility:** Access control at workflow boundaries (who can start, who can transition, visibility rules) stays entirely with host app.
- **MockBusinessApp:** Implements `WorkflowAccessProfile` to show business-user queue work on admin page and enable admin transitions without teaching the shared runtime about business users.
- **TestSite:** Maintains web-user-only queue profile; same runtime reused with different host rules.

### Implementation Details

- **AuthoredStage/AuthoredGateway:** Added `queueName: string` field to model.
- **Wire format:** Serialise/deserialise `queueName` from authored workflow JSON.
- **Validation:** Require non-empty `queueName` on all stages; warn if gateway routes across unrelated queues.
- **MockBusinessApp queue profile:** web-user queue for workflow instances + admin queue for manual admin page transitions.
- **Payment demo:** Reshaped to use new queue model; split fan-out from enter-details to applicant wait-at-join + payments confirmation, then join releases to payment-complete.

### Validation

- ✅ `dotnet build UmbracoPrism.sln` — clean, 0 warnings, 0 errors
- ✅ `dotnet test` — filtered Core tests pass; payment demo tests reworked and validated

### Architecture Boundary

**Shared runtime owns:**
- Queue topology (stages belong to queues, gateways connect them)
- Workflow transitions (stage → gateway → stage routing)
- Validation (no dangling routes, all stages assigned to queue)

**Host app owns:**
- Queue definition (`availableQueues()`)
- Access control (who can start, transition, view)
- Queue-aware UI (filters, pickers, visibility)

Payment demo demonstrates clean division: runtime projects workflow onto admin queue profile; MockBusinessApp enforces visibility and transition rules.

---

