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
- 2026-05-25T16:48:28.029+01:00 — Gateway-only authoring means canonical transitions use node-level source/target/trigger across stages and gateways; direct stage-to-stage links and stage-level waiting are invalid, and join gateways own waiting/defer metadata.

## 2026-05-25T16:48:28Z — Gateway-Only Redo: Runtime & Authored Model Alignment

**Task:** Rebuild gateway-only runtime model; realign backend/runtime contract  
**Status:** ✅ Complete

### Decision: Gateway-only authored routing and runtime alignment

- Canonical transitions now use `source`, `target`, `trigger` (uniform for stage and gateway nodes)
- Backend validation rejects direct stage-to-stage transitions (`PROJ141`) and stage-level waiting (`PROJ140`)
- Join gateway metadata carries full waiting contract + defer affordance
- Reference workflows and fixtures now route through explicit gateways (including pass-through gateways for linear flows)

### Frontend Coordination Gap (Deferred)

Current workflow editor client still carries hybrid assumptions:
- `types.ts` models transitions as `fromStage`/`toStage` with shims; still allows stage-level waiting
- `workflow-authoring-client.ts` normalizes gateway waiting incorrectly
- `workflow-gateway-representation.ts` infers gateway visuals heuristically (should use first-class authored gateways)

→ Documented for Isabelle/dedicated frontend alignment slice

### Orchestration Log

Written to `.squad/orchestration-log/2026-05-25T15-48-28-blathers.md`

### Backend Validation Status

All backend publishing and validation now aligned to gateway-only contract. Ready for frontend UX layer.

---

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


## 2026-05-30T11:15:00+01:00 — Slice 1 (backend): proposal preview removal

**Task:** Delete preview path (`IWorkflowPreviewService`, `WorkflowPreviewService`, `PreviewResult`, `SemanticDiff`) and the `/workflows/{key}/preview` endpoint. Keep `PublishPreviewResult` / `PublishResult` / `ProposalEnvelope` — those are the save/apply protocol, not the diff preview.
**Status:** ✅ Complete — commit 1e8bbcf on `squad/82-named-lanes-editor-slice`. Build 0W/0E; 842 Core tests green.

### Learnings

- The naming overlap between `PreviewResult` (semantic-diff preview, deleted) and `PublishPreviewResult` (publish dry-run, kept) is a real footgun. The directive's explicit "DO NOT DELETE" list was essential — a naive grep for `Preview` would have wiped the publish dry-run too.
- The endpoint was the only consumer of `IWorkflowPreviewService` in production code; once the endpoint went, the DI registration in `WorkflowEditorServiceExtensions` (line 39, plus a docstring mention) was the only other backend touchpoint.
- The `/preview` endpoint composed both services: `previewService.Preview(...) with { PublishPreview = publishService.PreviewAsync(...) }`. Worth noting for any future "what does a save dry-run look like?" question — the answer post-slice is "call `IWorkflowPublishService.PreviewAsync` directly via /apply, no separate endpoint."
- Pattern to watch: working tree arrived with ~50 unrelated pre-staged changes (Isabelle's TS work, Tom-Nook's docs, mock app, runtime engine). Had to use `git add` only on my four target paths and `git restore --staged` on three `prism-proposal-diff*` / `workflow-authoring-mock-drafter.ts` deletions that someone had pre-staged. The commit ended up cleanly 8 files / +1 / -431, exactly the backend slice.
- Test count: 842 (was 851 in the gateway-runtime slice; the diff is preview tests + skipped deferred-semantics tests being removed elsewhere in the working tree). No regressions in my scope.

---

## 2026-05-30 — Scope-Reset Session: Slice 1 Backend Complete

**Session:** workflow-editor-scope-reset  
**Role:** Implementation (backend deletions)

**Outcomes:**
- ✅ Slice 1 backend deletions (5 files deleted, 3 files edited, commit 1e8bbcf)
- ✅ Preview service removal (PreviewService.cs, endpoints, DTOs)
- ✅ Dependency injection cleanup (ServiceRegistration.cs)
- ✅ Full test suite green (842 tests pass, 0 warnings, 0 errors)

**Key Notes:**
- Slice 1 backend deletions complete and safe
- All build/test checks passed
- Frontend deletion by isabelle followed (Slice 1 frontend deletions)
- 3 git stashes preserved on branch (untouched, pending Slice 3/5)
