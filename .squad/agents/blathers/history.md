# Blathers — History

Backend Developer specializing in core infrastructure and pipeline design.

**Current Focus:**
- Issue #72: Planning workflow alignment (COMPLETED 2026-05-18T22:14:30.041+01:00)
- Fixed workflow definition mismatch between editor and runtime
- Backend tests passing (782/782)

**Latest:** Completed issue #72 planning workflow alignment (2026-05-18T22:14:30.041+01:00)

## Learnings

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
