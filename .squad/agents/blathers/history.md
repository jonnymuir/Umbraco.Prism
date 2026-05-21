# Blathers — History

Backend Developer specializing in core infrastructure and pipeline design.

**Current Focus:**
- Issue #72: Planning workflow alignment (COMPLETED 2026-05-18T22:14:30.041+01:00)
- Fixed workflow definition mismatch between editor and runtime
- Backend tests passing (803/803)

**Latest:** Implemented startup workflow publishing for authored → runtime alignment (2026-05-19T22:50:10.335+01:00)

## Learnings

- 2026-05-19T22:50:10.335+01:00 — Startup workflow publishing: At application startup, load authored workflows from `IAuthoredWorkflowStore`, project through `IWorkflowPublishService`, and publish to runtime store. This establishes authored definitions as the single source of truth while preserving the authored → projector → runtime boundary. Runtime seed files remain as fallback for workflows without authored sources.
- 2026-05-19T22:50:10.335+01:00 — Projection error handling: Startup publishing must check `PublishResult.HasErrors` and log projection diagnostics with severity filtering (`DiagnosticSeverity.Error`). Failed projections should log errors but not block startup for other workflows.
- 2026-05-19T22:50:10.335+01:00 — Test engine construction: `BusinessAppWorkflowEngine` requires `IWebHostEnvironment` (can be mocked), `IWorkflowContentSanitizer` (test-only passthrough implementation), and `IWorkflowDefinitionStore`. For testing startup publishing, use `InMemoryRuntimePublishedWorkflowStore` as the published workflow target.
- 2026-05-19T21:15:20.177+01:00 — Aspire debugger cleanup: VS Code's .NET debugger does not automatically clean up child processes spawned by Aspire DCP (Distributed Application Runtime) or Docker containers. Use `postDebugTask` in `.vscode/launch.json` to wire an automated cleanup script that terminates orphaned processes and stops Aspire-labeled containers on debugger stop.
- 2026-05-19T21:15:20.177+01:00 — Process cleanup safety: Cleanup scripts must use specific PIDs (`kill $PID`) rather than name-based killing (`pkill`, `killall`) per security guidelines. Pattern: find PIDs via `ps aux | grep pattern`, validate with `kill -0 $PID`, terminate gracefully (`kill`), then force kill (`kill -9`) after a brief wait.
- 2026-05-19T21:15:20.177+01:00 — Aspire container identification: Docker containers spawned by Aspire carry the label `aspire.resource.name`, making them queryable via `docker ps --filter "label=aspire.resource.name"`. This enables targeted cleanup of Aspire-managed containers without affecting other developer containers.
- 2026-05-18T22:14:30.041+01:00 — Planning workflow alignment: The TestSite's `PlanningWorkflowKey` must match the authored workflow's `definitionKey` to enable honest end-to-end validation. Changed from `"planning-notification"` to `"planning"` so editor and runtime serve the same workflow structure.
- 2026-05-18T22:14:30.041+01:00 — Workflow routing contract: The TestSite seed uses `TestSiteSeedContract.cs` constants to wire Umbraco content nodes to workflow definitions. Mismatched keys block E2E testing because the runtime serves a different workflow than the editor authors.
- 2026-05-18T22:14:30.041+01:00 — Fixture preservation: Keep legacy workflow seeds (like `planning-notification.json`) even when changing primary routes, as other tests may reference them for validation coverage.
- 2026-05-18T13:17:12.103+01:00 — Reference-app hosting for the workflow editor lives in `src/UmbracoPrism.MockBusinessApp/Program.cs`; `/workflow-editor` stays a thin authoring shell and the authoring API hangs off `/api/workflow-authoring/*`.
- 2026-05-18T13:17:12.103+01:00 — Explicit editor saves must persist both the authored JSON (`workflow-authored/*.workflow.json`) and the projected runtime seed (`workflow-seeds/*.json`) so reload and runtime stay aligned.
- 2026-05-18T13:17:12.103+01:00 — The live planning authoring seed at `src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json` must stay non-empty and keyed as `planning`; otherwise the reference host and seed contract tests drift immediately.
- 2026-05-18T13:17:12.103+01:00 — Runtime action execution now hangs off `src/UmbracoPrism.MockBusinessApp/Services/WorkflowActions/`; `WorkflowActionRegistry` reuses the editor catalog provider so discovery metadata and handler resolution stay aligned in the reference app.
- 2026-05-18T13:17:12.103+01:00 — `BusinessAppWorkflowEngine` is the place to orchestrate runtime action timing (`OnExit` → `OnTransition` → `OnEntry`) around state changes without pushing business-side handlers into `UmbracoPrism.WorkflowRuntime`.

## 2026-05-18T22:14:30.041+01:00 — Issue #72 completed

Fixed planning workflow definition mismatch between editor and runtime:
- **Problem**: Editor loaded `planning.workflow.json` (Declaration → Application Form → Check Answers → Submitted) but runtime served `planning-notification.json` (Describe your project → Type of work → etc.), blocking honest E2E validation
- **Root cause**: `TestSiteSeedContract.PlanningWorkflowKey` was hardcoded to `"planning-notification"` instead of `"planning"`
- **Solution**: Changed `TestSiteSeedContract.cs` to use `"planning"` workflow key, aligning editor and runtime
- **Impact**: E2E test infrastructure now ready for complete flow validation; all 782 backend tests passing
- **Preserved**: Legacy `planning-notification.json` seed remains for existing test coverage
- **Decision doc**: `.squad/decisions/inbox/blathers-issue-72-alignment.md`

## 2026-05-18T19:41:25Z — Issue #69 completed

Hosted the workflow editor inside MockBusinessApp with full authored persistence and save/publish round-tripping:
- `/workflow-editor` endpoint serves as thin reference authoring shell.
- `/api/workflow-authoring/workflows/{key}` handles load/save/validate/preview/apply/simulate.
- Authored workflows persist separately from runtime seed; deterministic republishing keeps them aligned.
- Endpoint contract tests (77/77) passing, including live authored-seed coverage.
- Designer can reload and retain last explicit save state; runtime projection still driven by seed.
- Reference host remains thin; authoring API owns persistence and republish logic.

## 2026-05-18: Issue #70 Runtime Handler Registry Decision

Decision formalized: **keep runtime handler registration in the reference app boundary**.

### Key Points
- Register workflow runtime handlers in `src/UmbracoPrism.MockBusinessApp/Services/WorkflowActions/`
- Keep `BusinessAppWorkflowEngine` responsible for invoking in `OnExit` → `OnTransition` → `OnEntry` order
- Reuse `BuiltInActionCatalogProvider` as registry catalog source
- Avoid duplicating lists of action types and parameter schemas

### Rationale
- Generic `UmbracoPrism.WorkflowRuntime` package stays orchestration-focused
- Handler implementations are host-specific business behaviour
- Authoring catalog + runtime registry alignment prevents drift

### Quality Gate Status
Tangy has established quality gate for #70 covering runtime contracts, DI registration, catalog endpoint, and .NET tests. Ready for implementation phase.


## 2026-05-19T18:16:08Z: Admin-Page Edit-Workflow Link — NEXT REVISION ASSIGNED

**Issue:** Admin workflow definitions page shows "Edit workflow" link, but clicking the link does not reliably open the editor for that specific workflow definition.

### Current Blocker
Deep-link parameter mismatch between admin card URL (`workflow=planning-notification`) and editor shell initialization (`workflow-key=planning`).

### Acceptance Criteria for Next Revision
1. Admin card deep-link parameter must match editor shell's loaded workflow key exactly.
2. Live test passes: `tests/workflow-gds-journey.spec.ts` (admin card click → correct editor session)
3. File-shape test passes: `src/UmbracoPrism.Core.Tests/WorkflowShowcaseShortcutTests.cs`
4. Client build: green

**Scribe Note:** Tangy has evidence; Blathers owns implementation. When fixed, submit for re-review.

**References:**
- `.squad/log/2026-05-19T18-16-08Z-workflow-editor-selection-mismatch.md`
- `.squad/decisions/inbox/tangy-edit-workflow-link-final.md`

## Scribe Consolidation (2026-05-19T21:41:48.843Z)

Decisions consolidated into team decisions log. Orchestration recorded.

## 2026-05-19: Workflow Publishing Implementation

### 2026-05-19T22:50:10.335+01:00 | Startup workflow publishing pipeline wired

Implemented startup publishing to establish authored workflows as single source of truth. Added Program.cs startup block to load and project all authored workflows at boot. Created StartupWorkflowPublishingTests.cs with 3 tests. All 803 backend tests pass.

Decision merged into decisions.md by Scribe 2026-05-19T22:00:07Z.

## 2026-05-19 — Reference Workflow Repository Implementation Decision

**Status:** Implemented; decision merged to `.squad/decisions.md`.

**Deliverable:** `blathers-reference-workflow-repo.md` — Pattern and implementation for in-memory demo seeding.

**Implementation:**
- **New:** `ReferenceWorkflowRepository` (C# static methods defining 4 authored workflows as code)
- **New:** `ReferenceWorkflowDefinitionStore` (in-memory `IWorkflowDefinitionStore` projecting workflows)
- **Updated:** `Program.cs` — Wiring changes for unified Authored → Projector → Runtime flow
- **Removed:** Filesystem loading from `workflow-authored/`, startup publishing loop
- **Removed:** Legacy `planning-notification.json` (no longer needed)

**Benefit:** Single source of truth for reference workflows (C# code, not scattered JSON); extension point for downstream apps.

**Consequence:** Reference repository becomes pattern for downstream consumption; tests must verify in-memory approach vs. filesystem approach.

**Basis:** Blathers background agent submission to Scribe inbox.

## 2026-05-21T21:54:07.868+01:00 — Workflow proof case stabilization

- Switched generic `WorkflowPatchServiceTests` and `WorkflowPreviewServiceTests` from the planning fixture to the shared `community-enquiry` reference workflow.
- Kept planning-specific behaviour in richer publish and fixture coverage where actions, conditions, and handoffs are the point of the test.
- Confirmed the backend/client four-workflow slice stays green enough to land without deleting required Playwright screenshot baselines.

## 2026-05-21T21:54:07.868+01:00 — NU1510 cleanup

- Removed `System.Security.Cryptography.Xml` from `UmbracoPrism.Core.Tests` and `UmbracoPrism.Shared` after confirming neither project uses XML crypto types directly.
- Tightened two backend tests to clear nullability warnings that surfaced during a full solution rebuild.
- Verified `dotnet build UmbracoPrism.sln` succeeds without the `NU1510` warning, and the focused backend auth/refresh regression tests stay green.

## 2026-05-21T21:54:07.868+01:00 — Workflow proof case decision + NU1510 cleanup (blathers-4 & blathers-5)

**Decisions made:**

### blathers-4: Canonical proof workflow for patch/preview tests
Use community-enquiry instead of planning fixture for generic `WorkflowPatchService` and `WorkflowPreviewService` tests. Rationale: planning is overspecified (domain-rich, multi-stage complexity adds noise without proving generic contracts). community-enquiry is minimal (2 stages, 1 transition) and domain-agnostic.

**Consequence:** Patch/preview tests become self-documenting. Product changes to planning won't ripple into authoring service test suite.

### blathers-5: Remove NU1510 warning
- Removed direct `System.Security.Cryptography.Xml` refs from `UmbracoPrism.Core.Tests` and `UmbracoPrism.Shared`
- Tightened two focused backend tests for nullability
- Full build and test suite passes
- Warning eliminated without dependency graph widening

**Impact:** Landing gate now clean. All seams green. Branch ready for merge.

**Decision docs:** Merged to `.squad/decisions.md` (from inbox/blathers-workflow-proof-case.md, inbox/blathers-nu1510-cleanup.md)

## 2026-05-21T21:54:07.868+01:00 — Core-tests CI warning cleanup

- Replaced `ForwardedHeadersOptions.KnownNetworks` usage in `PrismComposer` with `KnownIPNetworks` to match the current ASP.NET deprecation guidance without changing trust behaviour.
- Tightened bearer header extraction in `PrismAuthExtensions` so the authentication-failure diagnostics keep working without a nullable dereference warning.
- Verified `dotnet build UmbracoPrism.sln -c Release`, focused auth regression tests, and the full core-tests lane command all pass cleanly after the fix.

**Decision doc:** `.squad/decisions/inbox/blathers-ci-core-tests.md`
