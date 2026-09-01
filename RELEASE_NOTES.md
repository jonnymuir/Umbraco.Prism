# Release Notes

## v1.10.0: 2026-06-06

### Summary
This release delivers a collection of workflow editor fixes and backend validation improvements focused on correctness of the workflow graph, reliable save behaviour, and gateway routing integrity.

### New Features

- **Gateway routing validation**: The backend now validates that every state route targets a gateway (not another state directly). Violations are reported as structured HTTP 400 errors with a clear `errorCode: "workflow-gateway-routing-invalid"` and per-violation detail. This prevents malformed workflow graphs from being persisted.

- **Cycle-breaking Join gateways**: Backward loops in workflow graphs (e.g. "save draft" or "return to form" routes) are now modelled via dedicated Join gateways. This allows the layout engine to produce correct vertical rankings without horizontal sprawl.

### Fixes

- **Runtime sync on save**: Editing a workflow in the authoring UI now immediately updates the running workflow engine. Previously, changes were stored but the runtime continued using the original seed definition until restart.

- **Y-axis layout algorithm**: The workflow graph layout engine now correctly detects and excludes backward edges from Join gateways when computing node ranks, producing clean top-to-bottom layouts for workflows with cycles.

- **AllowOutOfOrderMetadataProperties**: The workflow save endpoint now correctly handles JSON payloads where the `type` discriminator field is not in the first position (e.g. when the editor sorts keys alphabetically).

### Seed Workflow Updates

- `planning.json`, `community-enquiry.json`, and `information-request.json` have been updated to follow the gateway-first routing rule, all direct state-to-state routes now route via Split gateways.

### Breaking Changes

None. All changes are backward compatible.

---
