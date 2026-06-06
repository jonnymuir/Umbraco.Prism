## 2026-06-05: Queue-only model implementation completed

- Tom Nook: Contract definition and implementation plan locked
- Tangy: Behavioural test specification and validation gates defined
- Isabelle: Editor refactor completed (build ✅, Playwright suite ✅)
- Blathers: Runtime refactor completed (core test suite ✅)
- Team coordination: All decisions merged to .squad/decisions.md
- Next: Full integration validation and cross-stack testing

---

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


## 2026-06-04: Flattened Workflow Model Session

**Agents:** Tom Nook, Tangy, Isabelle, Blathers  
**Session:** Queue-first architecture consolidation  
**Decision:** Single `WorkflowDefinition` contract approved

**Key outcomes:**
- AuthoredWorkflow + WorkflowDefinitionFile + ProjectedWorkflow → single canonical schema
- Lanes renamed to queues; laneKey + queueName merged to single identifier
- Gateways elevated from metadata to first-class definition elements
- Editor and runtime both use persisted contract directly

**Team coordination:** Decisions merged from 4 agents into `.squad/decisions.md`

**Blathers contribution:** Flattened backend/runtime onto single workflow definition; removed _instanceLookup cached map; dotnet build and filtered core tests passed.

## Session: 2026-06-06 Save Error Orchestration

**Status:** ✅ Complete

**Blathers contribution:** Fixed MockBusinessApp workflow save endpoint to validate nested components and return structured Problem Details (application/problem+json) with errorCode, traceId, and per-error details instead of raw exceptions. Enables safe client-side error handling.

**Team outcomes:**
- Blathers: Backend save validation and structured errors
- Isabelle: Persistent, copyable, sanitised error UI
- Tangy: 4-contract regression coverage

**Integration:** All decisions merged to .squad/decisions.md. Orchestration logged in .squad/orchestration-log/. Session log at .squad/log/2026-06-06T10-27-53Z-save-error-fix.md
