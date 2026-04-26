# Session Log — Workflow JSON Schema Design Review

**Date:** 2026-04-25  
**Orchestration Timestamp:** 2026-04-25T23:03:57Z  
**Scope:** Workflow schema cleanup proposal  
**Participants:** Tom Nook (Lead/Architect)  

## Scope

Diagnose JSON bloat in workflow envelope output; propose cleanup options.

## Outcomes

- **Diagnosis:** 3 sources of null bloat confirmed: `StepType`, `PrismComponentDefinition` nullables, state-level `WaitingConfig`
- **Option 1:** Minimal cleanup (delete properties + `WhenWritingNull` config) — ~1 day
- **Option 2:** Polymorphic hierarchy — ~1 sprint, deferred to v2.0
- **Recommendation:** Option 1 now; Option 2 deferred

## Artifacts

- Proposal: `.squad/decisions/inbox/tom-nook-workflow-schema-cleanup.md`
- Orchestration log: `.squad/orchestration-log/2026-04-25T23-03-57Z-tom-nook.md`

## Next Steps

Awaiting Jonny's confirmation on Option 1 recommendation before commissioning implementation work.
