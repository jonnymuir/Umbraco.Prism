# Decision: Planning localhost-auth flow must resolve by host workflow key

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Blathers  
**Status:** Proposed  

Keep the planning localhost-auth/runtime contract keyed by the **host workflow key** (`planning`), not the authored workflow's projected `definitionKey` (`planning-application`).

## Decision

1. Runtime definition stores must preserve the lookup key supplied by the host surface instead of re-keying everything to `WorkflowDefinitionFile.DefinitionKey`.
2. The reference planning workflow in `ReferenceWorkflowRepository` must stay aligned with the authored four-stage planning contract (Declaration → Application Form → Check your answers → Application submitted).
3. Localhost-auth planning tests should follow that live contract and only assert persisted data after the workflow crosses a persistence boundary (for planning, the Application Form `OnExit` save).

## Why

- The Umbraco TestSite, dashboard links, and workflow pages route by host key (`planning`). Re-keying the runtime to `planning-application` made the workflow unreachable even though authoring metadata still loaded.
- A skeletal two-stage in-memory planning definition silently diverged from the authored planning contract, so the runtime page no longer matched editor/admin expectations.
- The continuation walkthrough had also started assuming unsaved mid-stage field edits would survive a round-trip, but planning only persists when leaving the application-form stage.

## Consequences

- Reference/demo workflow stores may support distinct host keys and authored definition keys without breaking runtime lookup.
- The planning runtime flow now matches the authored/editor contract again, so localhost-auth and walkthrough assertions can stay honest without reviving the retired `planning-notification` flow.
- Future continuation tests should assert resumed state only after a stage transition or another explicit persistence point has occurred.
