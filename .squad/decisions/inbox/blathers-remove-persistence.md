# Decision: Workflow saves are memory-only during demo/testing phase

**Date:** 2026-06-06  
**Author:** Blathers (Backend Dev)  
**Status:** Accepted

## Context

A previous session added disk persistence to `PUT /mockapp/workflows/{key}` so that workflow edits survived application restarts. The implementation wrote to `workflow-seeds/{key}.json` using an atomic temp-file pattern and injected `IHostEnvironment` into the handler.

## Decision

Remove disk persistence from the save endpoint. Workflow saves remain **memory-only** during the demo and testing phase:

- `store.Save(key, workflow)` ✅ kept  
- `engine.UpdateDefinition(key, workflow)` ✅ kept  
- File write to `workflow-seeds/{key}.json` ❌ removed  

## Rationale

Disk writes risk overwriting seed files that automated tests depend on. The memory-only approach is safe and sufficient for the current demo/testing phase.

## Consequences

- `IHostEnvironment env` removed from the PUT handler parameter list  
- `WorkflowDiskPersistenceTests` deleted (all three tests covered disk-write behaviour that no longer exists)  
- Test count: 806 → 803 (the 3 deleted disk-persistence tests)  
- `dotnet build` ✅, `dotnet test` 803 passed ✅  

## Commit

`0402f36` on branch `fix/workflow-editor-save-and-layout`
