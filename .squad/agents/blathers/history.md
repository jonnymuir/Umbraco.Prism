# Blathers — History

Backend Developer specializing in core infrastructure and pipeline design.

**Current Focus:**
- Vinyl/Core notification boundary refactor COMPLETED
- Core notification infrastructure remains reusable and stable
- TestSite vinyl behavior now opt-in (configuration-driven)

**Status:** All 815 backend tests passing; 0 build warnings

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

