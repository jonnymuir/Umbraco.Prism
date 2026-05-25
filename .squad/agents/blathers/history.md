# Blathers — History

Backend Developer specializing in core infrastructure and pipeline design.

**Current Focus:**
- Vinyl/Core notification boundary refactor COMPLETED
- Core notification infrastructure remains reusable and stable
- TestSite vinyl behavior now opt-in (configuration-driven)

**Status:** All 851 backend tests passing; 0 build warnings

## Key Learnings

- Fixture ordering safety: Use `WorkflowAuthoringFixtureLocator.GetFixturesPath()` for shared fixtures, not direct Assembly.Location paths. xUnit test collection scheduling creates races with concurrent fixture resets.
- Projection error handling: Startup publishing must check `PublishResult.HasErrors` and log diagnostics selectively by severity.
- Workflow routing: Editor and runtime serve the same workflow when `PlanningWorkflowKey` matches the authored `DefinitionKey`.
- Process cleanup: Use specific PIDs (`kill $PID`), not name-based (`pkill`, `killall`) per security guidelines.
- Aspire cleanup: Wire `postDebugTask` in `.vscode/launch.json` to clean up child processes spawned by DCP on debugger stop.
- **Filesystem durability (2026-05-24):** Always call `stream.FlushAsync()` explicitly before closing file streams in write operations that are immediately followed by read verification. Linux CI environments with virtualized/networked filesystems cache directory metadata; relying only on `await using` disposal isn't sufficient to guarantee File.Exists() sees the new file. This manifested as intermittent HTTP 500 failures in `PostApply_WithExistingWorkflow_PublishesRuntimeDefinition` where `PublishAsync` couldn't reload the just-saved workflow JSON for round-trip verification.

## 2026-05-23T13:04:58.778000+00:00 — Session: Vinyl/Core Boundary Integration

All squad members deployed together to complete the vinyl/core boundary work. Architecture split successful:
- Core remains reusable notification infrastructure
- TestSite vinyl behavior is now opt-in
- All 815 tests passing
- 0 warnings in build/test lane

Decision doc merged to decisions.md; full session log at `.squad/log/2026-05-23T13:04:58.778000+00:00-vinyl-core-boundary-integration.md`

## 2026-05-24 — CI Red Run Resolution

Fixed Linux CI apply/publish flush race condition in WorkflowAuthoringEndpoints.PostApply causing backend 500 errors. Local backend validation passed. Decision logged: `blathers-ci-apply-regression.md`.

## Learnings

- 2026-05-25T12:49:20.153+01:00 — For multi-lane workflow slices, land workflow-level lane and gateway metadata first, then project effective actor/role assignment back onto published state metadata so current runtime behaviour stays stable while later issues add split/join execution.
- Multi-cursor join pattern: `Cursors = []` means single-cursor legacy mode; `Cursors` populated means multi-cursor. Keep `CurrentState` in sync with `FirstActiveStageCursorKey()` on every save so legacy callers never see a cursor-only state key.
- FluentAssertions `ContainInOrder` overload: pass expected values as `IEnumerable<T>`, and reason string as the second argument. Passing the reason as a trailing string in the params-array treats it as an additional expected element.
- PROJ137/138/139: any pre-existing test using a Join gateway must now also provide `WaitingInfo` and `RequiredIncomingLanes`; the schema validator enforces these from the authoring layer upward.

## [2026-05-25T12:00:03Z] Scribe: Spawn Manifest Processing

**Activity:**
- Orchestration log written
- Decisions inbox merged (9 files processed)
- Cross-agent updates logged
- Session log recorded

**Status:** ✓ Manifest processed, ready for next cycle


## 2026-05-25T14:34:44.680Z — Merged Gateway Runtime Slice Implementation

**Spawn:** blathers background agent  
**Task:** Build merged gateway runtime slice (#83/#84/#85)  
**Outcome:** ✅ Complete (PR #89 open)

### Deliverables

- `WorkflowCursor.cs` — Per-lane cursor records
- Extended `WorkflowInstanceState.cs` with `Cursors` and `JoinArrivals` bookkeeping
- Split/join gateway dispatch in `WorkflowRuntimeEngine.cs`
- Schema validation codes PROJ137, PROJ138, PROJ139 for join gateway completeness
- Join waiting envelope sourced from `WorkflowGatewayDefinition.WaitingContent` (not fake stages)
- Backward-compatible cursor model: legacy single-cursor workflows show no regression
- `RequiredIncomingLanes` emitted in sorted order for deterministic publish output

### Quality Gate

✅ All 851 tests passing  
✅ Backend authoring: 129 passed, 3 skipped (deferred semantics)  
✅ Workflow serialization/schema/publish: green  
✅ `dotnet test UmbracoPrism.sln`: green  
✅ Branch clean; PR #89 ready for review  

### Files Modified

- `AuthoredGateway.cs` — `Description`, `WaitingInfo`, `RequiredIncomingLanes`
- `WorkflowDefinitionFile.cs` — published gateway fields
- `WorkflowProjector.cs` — gateway-targeted transitions
- `AuthoredWorkflowSchemaValidator.cs` — PROJ137/138/139
- `WorkflowCursor.cs`, `WorkflowInstanceState.cs`, `WorkflowRuntimeEngine.cs` — NEW/extended
- Test files: 17 new tests (gateway projection + engine behavior)

### Cross-Layer Coordination

- Isabelle's editor-only fields NOT yet in C# model (deferred for later alignment)
- Backend publish pipeline decision deferred (strip or preserve on publish)

**Orchestration log:** `.squad/orchestration-log/2026-05-25T14-34-44-blathers.md`
