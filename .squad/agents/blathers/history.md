## 2026-06-06: Cycle Fix — Join Gateway Pattern for Backward Loops

**Branch:** current working branch

### Problem
community-enquiry.json (and information-request.json) had backward-edge cycles that broke Kahn's longest-path algorithm. Split gateways routing back to the initial state meant no node had in-degree 0, collapsing all nodes to rank 0 and creating horizontal sprawl.

### Fix (two-part)

**Part A — JSON restructure:**
- Added `join-return-to-form` (Join) gateway to both community-enquiry.json and information-request.json
- `route-save-draft` and `route-from-under-review` now route to this Join gateway instead of directly back to the initial state
- Join gateway's route resolves back to initial state (for runtime correctness)

**Part B — Layout algorithm fix (`prism-workflow-graph.ts`):**
- After building the adjacency graph, detect backward edges from Join gateways using BFS reachability
- Remove such edges from the ranking graph (so Kahn's sees a DAG and ranks correctly)
- Backward edges remain in the transitions list for visual rendering as upward curving rails

**Result:** dotnet build ✅, dotnet test 809 passed ✅

Decision documented in `.squad/decisions/inbox/blathers-cycle-fix-community-enquiry.md`.

---

## 2026-06-06: Gateway Routing Validation + Three Workflow Fixes

**Branch:** current working branch

### Part A — Gateway Routing Validation

Added `ValidateGatewayRouting()` instance method to `WorkflowDefinitionFile` in `UmbracoPrism.Shared`. The method:
- Collects all gateway keys from `Gateways`
- Checks every state route target: if it matches a state key (not a gateway key), it's a violation
- Returns one error string per violation; empty list = valid

Wired into `WorkflowSourceSaveRequestParser.ParseAsync()` in MockBusinessApp: called after successful deserialization, returns HTTP 400 `application/problem+json` with `errorCode: "workflow-gateway-routing-invalid"` on violations.

**Rule:** State routes must always target a gateway. Gateway routes may target states or other gateways (gateway→gateway is allowed, e.g. Split→Join in payment-demo).

### Part B — Three Workflow Fixes

Fixed direct state→state routes in three seed files by inserting Split gateways:

- **planning.json**: 4 gateways added (`route-from-declaration`, `route-from-application-form`, `route-from-check-answers`, `route-from-id-verification`). Conditions and actions preserved on gateway routes.
- **community-enquiry.json**: 3 gateways (`route-from-collecting-details`, `route-save-draft`, `route-from-under-review`). Save-draft loop now goes via `route-save-draft` gateway.
- **information-request.json**: Same pattern as community-enquiry (3 gateways).
- **payment-demo.json**: Already correct, unchanged.

### Tests

Added `WorkflowGatewayRoutingValidationTests.cs` (6 cases):
- Valid: state → gateway → state
- Invalid: state → state (one error per violation)
- Valid: gateway → state
- Valid: gateway → gateway (Split → Join)
- Multiple violations returns one error per route
- Workflow with no routes returns no errors

**Result:** `dotnet build` ✅, `dotnet test` 809 passed ✅

---



**Branch:** `fix/workflow-editor-save-and-layout` (continued)

**Tasks completed:**

### Part A — Disk persistence
Added disk write to `PUT /mockapp/workflows/{key}` in `Program.cs`. After `store.Save()` and `engine.UpdateDefinition()`, the handler now serialises the workflow with `WriteIndented = true` to `{ContentRootPath}/workflow-seeds/{key}.json` using an atomic write (temp → `File.Move`). File-write failures log a warning and do not surface as HTTP errors.

### Part B — Workflow migration (3 files)
- `planning.json` → v3: 1 queue (`applicant`), 5 states with `queueKey`, `routes` on every state, `gateways: []`. Old-format `transitions`, `handoffs`, `actions` removed.
- `community-enquiry.json` → v2: 2 queues (`applicant`, `reviewer`), 3 states, sequential handoff, no gateways.
- `information-request.json` → v2: same pattern as community-enquiry.

### Tests
- Added `WorkflowDiskPersistenceTests` (3 cases): file-write verification, restart-recovery simulation, error-resilience.
- Updated `MockBusinessAppPlanningWorkflowSeedTests.PlanningSeed_HasExpectedStructure` to check `Queues` instead of `Transitions`.
- **All 806 core tests pass** (802 baseline + 4 new).

---



**Branch:** `fix/workflow-editor-save-and-layout`
**Commit:** 4cd7f60

**Bug:** Edits made in the payment demo editor (e.g. changing `cardholderName` label) were not reflected in the runtime. User reported "Changes in the payment demo are not picked up in the runtime."

**Root cause:** `PUT /mockapp/workflows/{key}` called `store.Save(key, workflow)` (updating `ReferenceWorkflowSourceStore`) but never called `engine.UpdateDefinition(key, workflow)`. The `BusinessAppWorkflowEngine` holds a separate `_definitions` dictionary populated at startup from seed files; it was never told about the save.

**Fix:** Added `IWorkflowRuntimeEngine engine` to the PUT handler parameter list and called `engine.UpdateDefinition(key, workflow)` after `store.Save()`. The method already existed on `IWorkflowRuntimeEngine` and `WorkflowRuntimeEngine`.

**Test added:** `WorkflowDefinitionUpdateTests` (4 cases):
- `UpdateDefinition_ReturnsTrue_ForKnownKey`
- `UpdateDefinition_ReturnsFalse_ForUnknownKey`
- `UpdateDefinition_TextInputLabel_IsReflectedByGetDefinition` (the regression case — verifies `cardholderName` label change propagates to runtime)
- `UpdateDefinition_DoesNotAffectOriginalSeedLabel_BeforeUpdate`

**Validation:** `dotnet build` ✅, `dotnet test` 802 passed ✅

---

## 2026-06-05: Queue-only model implementation completed

- Tom Nook: Contract definition and implementation plan locked
- Tangy: Behavioural test specification and validation gates defined
- Isabelle: Editor refactor completed (build ✅, Playwright suite ✅)
- Blathers: Runtime refactor completed (core test suite ✅)
- Team coordination: All decisions merged to .squad/decisions.md
- Next: Full integration validation and cross-stack testing

---

## Learnings

### 2026-06-06: AllowOutOfOrderMetadataProperties is on JsonSerializerOptions, not JsonPolymorphicAttribute

When fixing the "Invalid workflow payload: Every workflow component must include a supported 'type' value" save error caused by `sortKeys()` moving the `type` discriminator away from first position:

- `AllowOutOfOrderMetadataProperties` does **not** exist on `[JsonPolymorphicAttribute]` in .NET 10 — only `TypeDiscriminatorPropertyName`, `IgnoreUnrecognizedTypeDiscriminators`, and `UnknownDerivedTypeHandling` are present.
- `AllowOutOfOrderMetadataProperties` is a property of `JsonSerializerOptions` (available since .NET 9).
- The correct fix is `new JsonSerializerOptions { AllowOutOfOrderMetadataProperties = true, ... }` at the point of deserialization.
- Enabling this causes the deserialiser to buffer the entire JSON object in memory before committing to a strategy — acceptable overhead for an authoring-time save API.
- Any future `JsonSerializerOptions` instance used to deserialise `WorkflowDefinitionFile` (which contains polymorphic `PrismComponent` children) must also set this flag.

**Commit:** 74c52c5 on branch `fix/workflow-editor-save-and-layout`



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

## 2026-06-06: Commit 74c52c5 — AllowOutOfOrderMetadataProperties fix

Fixed PrismComponent JSON polymorphic discriminator order issue by setting `AllowOutOfOrderMetadataProperties = true` on `mockWorkflowJsonOptions` in `MockBusinessApp/Program.cs`. Allows alphabetically-sorted TypeScript keys to work with .NET's `JsonPolymorphic` deserializer.

Decision documented and validated by Tangy.

