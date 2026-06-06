# Gateway Routing Validation — Backend Decision

**Date:** 2026-06-06  
**Author:** Blathers (Backend Dev)

---

## Decision

Enforce strict gateway routing rules across the workflow definition contract. These rules apply to the persisted `WorkflowDefinitionFile` format used by seed files, the save endpoint, and the runtime.

### Rules

1. **State → gateway only:** Routes on a state (`StepDefinition.Routes`) must always target a gateway key. Direct state-to-state routes are forbidden.
2. **Gateway → state or gateway:** Routes on a gateway may freely target states or other gateways. No restriction.
3. The ONLY path out of a state is via a gateway. This is an architectural invariant.

### Where Implemented

- **Model method:** `WorkflowDefinitionFile.ValidateGatewayRouting()` — returns one `string` error per violation; empty list means valid. Added to `UmbracoPrism.Shared`.
- **Save endpoint guard:** `WorkflowSourceSaveRequestParser.ParseAsync()` in `UmbracoPrism.MockBusinessApp` calls `ValidateGatewayRouting()` after deserialization and returns HTTP 400 `application/problem+json` with `errorCode: "workflow-gateway-routing-invalid"` if any violations are found.

### Note on Gateway → Gateway

Gateway-to-gateway routing (e.g., Split → Join) is explicitly allowed and required for parallel fan-out patterns like the payment demo. The rule does NOT prohibit this.

---

## Workflows Fixed

Three seed files in `UmbracoPrism.MockBusinessApp/workflow-seeds/` were updated to comply:

### planning.json
Added four Split gateways:
- `route-from-declaration` → `application-form`
- `route-from-application-form` → `check-answers`
- `route-from-check-answers` → `id-verification` (continue) or `submitted` (submit, with conditions & actions preserved)
- `route-from-id-verification` → `submitted`

### community-enquiry.json
Added three Split gateways:
- `route-from-collecting-details` → `under-review` (submit)
- `route-save-draft` → `collecting-details` (save-draft loop)
- `route-from-under-review` → `complete` (approve) or `collecting-details` (request-changes)

### information-request.json
Same pattern as community-enquiry:
- `route-from-collecting-info` → `under-review` (submit)
- `route-save-draft` → `collecting-info` (save-draft loop)
- `route-from-under-review` → `complete` (approve) or `collecting-info` (request-changes)

**payment-demo.json was correct and unchanged.**

---

## Tests Added

`WorkflowGatewayRoutingValidationTests.cs` (6 cases):
- ✅ State → gateway → state: no errors
- ❌ State → state: one error per violation
- ✅ Gateway → state: no errors
- ✅ Gateway → gateway (Split → Join): no errors
- ❌ Multiple state → state violations: one error per route
- ✅ Workflow with no outgoing routes: no errors

**Result:** 809 core tests pass (all green).
