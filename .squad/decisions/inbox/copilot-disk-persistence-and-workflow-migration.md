# Disk persistence for workflow editor saves + 3-workflow format migration

**Date:** 2026-06-06  
**Author:** Blathers (Backend Dev)

## Decision

### Part A — Disk persistence

`PUT /mockapp/workflows/{key}` now writes the saved workflow to
`{ContentRootPath}/workflow-seeds/{key}.json` after updating both
`ReferenceWorkflowSourceStore` (in-memory source) and `WorkflowRuntimeEngine`
(in-memory runtime). Writes are atomic (write to `{key}.json.tmp` then
`File.Move` with `overwrite: true`). File-write failures log a warning but do
not surface as HTTP errors — the in-memory update always wins.

In development mode `ContentRootPath` is the project directory, so saves update
the checked-in seed files directly. A `dotnet build` (which runs
`CopyToOutputDirectory`) propagates the change to the bin folder that the
`FilesystemWorkflowDefinitionStore` and `ReferenceWorkflowRepository` read at
startup.

### Part B — 3-workflow format migration

`planning.json` (→ v3), `community-enquiry.json` (→ v2), and
`information-request.json` (→ v2) have been rewritten to match the
`payment-demo.json` schema:

| Field | Old format | New format |
|---|---|---|
| Transitions | Top-level `transitions[]` with `fromState`/`action` | Per-state `routes[]` with `target`/`trigger` |
| Queues | Absent | Top-level `queues[]` |
| Queue assignment | Absent | Per-state `queueKey` |
| Gateways | Absent | Top-level `gateways[]` (empty for linear workflows) |

**planning:** 1 queue (`applicant`), 5 states, 0 gateways — single-lane linear flow.  
**community-enquiry:** 2 queues (`applicant`, `reviewer`), 3 states, 0 gateways — sequential handoff (no parallel split needed).  
**information-request:** 2 queues (`applicant`, `reviewer`), 3 states, 0 gateways — same pattern.

Old-format fields (`transitions`, `handoffs`, `authoredWorkflowId`, `actions`
on states, `allowedActions`) are removed. `stageType`, `actor`, `queueKey` are
now present on every state.

## Test coverage

- `WorkflowDiskPersistenceTests` (3 cases):
  - `Put_WritesWorkflowToDisk` — verifies file is written to `{ContentRoot}/workflow-seeds/payment-demo.json`
  - `Put_PersistedFile_IsLoadedByFreshEngine_SimulatingRestart` — creates a fresh `BusinessAppWorkflowEngine` from the temp seed dir and verifies the modified definition is present
  - `Put_FileWrite_DoesNotBlockSuccessfulResponse_WhenSeedDirIsReadOnly` — verifies PUT returns 204 regardless
- `MockBusinessAppPlanningWorkflowSeedTests.PlanningSeed_HasExpectedStructure` updated to assert `Queues` (not `Transitions`)
- All 806 core tests pass ✅

## Impact on other agents

- **Isabelle:** Editor loads these three workflows via `GET /mockapp/workflows/{key}`. The new format uses `routes` on states rather than top-level `transitions`; verify the editor canvas renders the route arrows correctly.
- **Tangy:** Regression contract tests (`FourWorkflowReferenceContractTests`) still pass. If behavioural tests drive the planning/community/information-request workflows at the engine level, they will now need `queueKey` on all states (required by `FilesystemWorkflowDefinitionStore`-loaded definitions).
