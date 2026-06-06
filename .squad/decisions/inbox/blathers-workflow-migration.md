# Decision: Workflow migration — planning, community-enquiry, information-request

**Date:** 2026-06-06  
**Author:** Blathers (Backend Dev)  
**Status:** Applied in memory; seed files not yet updated on disk

---

## Context

Three legacy seed workflows were still using the old `transitions` array format (with `fromState`, `toState`, `action` fields). The new canonical contract uses per-state `routes` (with `target`, `trigger`) and a top-level `queues` array.

Reference implementation: `payment-demo.json`

---

## Decisions made

### 1. planning.json — single-queue, linear applicant flow

- One queue: `web-user` (actor: `applicant`)
- All five states assigned to `web-user`
- `id-verification` (originally `actor: null`) assigned to `web-user` — no role gate, applicant-owned step
- Transitions converted to per-state `routes`; conditions and actions from the `check-answers → submitted` transition preserved on the route
- No gateways (sequential flow, no fan-out required)
- State-level actions (`forms.load`, `forms.save`, `forms.submit`) converted from legacy `parameters` key to correct `params` key per `WorkflowActionDefinition` schema

### 2. community-enquiry.json — two-queue applicant/reviewer handoff

- Two queues: `web-user` (applicant) and `business-user` (reviewer)
- `collecting-details` → `web-user`; `under-review` → `business-user` with `roleGates: ["reviewer"]`; `complete` → `web-user`
- `under-review` is a sequential reviewer-owned step, not a concurrent join; no gateway needed
- `save-draft` self-loop retained as a route on `collecting-details`
- `schemaVersion: "1.0"` added (was absent in original seed)

### 3. information-request.json — same two-queue pattern

- Identical two-queue structure to community-enquiry
- `collecting-info` → `web-user`; `under-review` → `business-user` with `roleGates: ["reviewer"]`; `complete` → `web-user`
- `allowedActions` field from old format dropped — not part of `StepDefinition` schema; routing now expressed solely via `routes`
- `schemaVersion: "1.0"` added

---

## Validation results

- PUT `/mockapp/workflows/planning` → 204 ✅
- PUT `/mockapp/workflows/community-enquiry` → 204 ✅
- PUT `/mockapp/workflows/information-request` → 204 ✅
- GET round-trip verified queue keys, state assignments, route counts ✅
- `dotnet test --filter UmbracoPrism.Core.Tests` → 806 passed, 0 failed ✅

---

## Follow-up recommended

The migrated definitions are in-memory only. The seed JSON files on disk still use the legacy `transitions` format. A follow-up task should update the three seed files to match the migrated definitions, so the runtime starts in the correct state from cold boot.
