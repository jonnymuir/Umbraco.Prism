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

## 2026-05-23T13:04:58.778000+00:00 — Session: Vinyl/Core Boundary Integration

All squad members deployed together to complete the vinyl/core boundary work. Architecture split successful:
- Core remains reusable notification infrastructure
- TestSite vinyl behavior is now opt-in
- All 815 tests passing
- 0 warnings in build/test lane

Decision doc merged to decisions.md; full session log at `.squad/log/2026-05-23T13:04:58.778000+00:00-vinyl-core-boundary-integration.md`
