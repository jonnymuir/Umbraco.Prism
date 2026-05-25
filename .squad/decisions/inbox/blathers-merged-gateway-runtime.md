# Decision: Multi-Cursor Split/Join Gateway Runtime (Issues #83–#85)

**Date:** 2026-05-26  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented, all 851 tests passing

## Context

Issues #83, #84, and #85 were merged into one implementation slice covering:
- #83: Split/join gateway routing in the runtime engine
- #84: Join-gateway-owned waiting info (not a fake stage)
- #85: Independent multi-lane cursor execution

## Decisions Made

### 1. Backward-compatible cursor model

`WorkflowInstanceState.Cursors = []` is treated as "legacy single-cursor mode". All existing engine paths remain unchanged. Multi-cursor mode activates only when at least one cursor is present. `CurrentState` on `WorkflowInstanceState` always mirrors the key returned by `FirstActiveStageCursorKey(Cursors)` so that callers written before multi-cursor support see no regression.

### 2. Split gateway auto-follow

The engine follows **all** outgoing transitions from a split gateway automatically (no user action required). The `Action` value on split transitions is by convention `"split-auto"`, but the engine fans out on any outgoing transition from a split gateway regardless of action value. This keeps the authored model expressive without requiring runtime special-casing of specific action strings.

### 3. Join gateway waiting envelope sourced from gateway definition

The join waiting envelope (`ResponseState = "defer"`, `StepType = "status-timeline"`) is built from `WorkflowGatewayDefinition.WaitingContent` / `WaitingExpectedSeconds` / `WaitingPollIntervalMs`. No fake stage is created. This was the key contract from issue #84 — the join gateway is the source of truth for its own waiting UX.

### 4. JoinArrivals not surfaced in runtime contract

`WorkflowInstanceState.JoinArrivals` is an internal bookkeeping dictionary (gateway key → list of arrived cursor IDs). It is intentionally not included in the public `IWorkflowRuntimeEngine` interface return types and is not shown to callers. It is persisted as part of instance state so that join convergence survives round-trips.

### 5. Schema validation codes

Three new codes enforce join gateway completeness at authoring time:
- **PROJ137** — join gateway must define `waitingInfo`
- **PROJ138** — join gateway must have at least one `requiredIncomingLane`
- **PROJ139** — each `requiredIncomingLane` must reference a defined lane key

These are validated by `AuthoredWorkflowSchemaValidator` before projection, meaning invalid join gateways never reach the runtime.

### 6. RequiredIncomingLanes emitted in sorted order

The projector emits `RequiredIncomingLanes` in ordinal sort order to preserve the determinism invariant that a given authored workflow always produces the same published JSON byte-for-byte.

## Files Changed

- `AuthoredGateway.cs` — added `Description`, `WaitingInfo`, `RequiredIncomingLanes`
- `WorkflowDefinitionFile.cs` — `WorkflowGatewayDefinition` extended with matching published fields
- `WorkflowProjector.cs` — gateway-targeted transitions accepted; new fields emitted
- `AuthoredWorkflowSchemaValidator.cs` — PROJ137/138/139 added
- `WorkflowCursor.cs` — NEW: per-lane cursor record
- `WorkflowInstanceState.cs` — `Cursors` + `JoinArrivals` added
- `WorkflowRuntimeEngine.cs` — split/join gateway dispatch, multi-cursor advance, join waiting envelope
- `WorkflowGatewayProjectionTests.cs` — NEW: 10 projection tests
- `WorkflowJoinGatewayEngineTests.cs` — NEW: 7 engine behaviour tests
