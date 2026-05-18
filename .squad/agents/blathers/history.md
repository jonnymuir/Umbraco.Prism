# Blathers — History

Backend Developer specializing in core infrastructure and pipeline design.

**Current Focus:**
- Issue #57: Deterministic publish pipeline (COMPLETED)
- Publish/apply workflow with metadata preservation
- End-to-end quality validation

**Latest:** Completed issue #69 reference-app hosting (2026-05-18T19:41:25Z)

## Learnings

- 2026-05-18T13:17:12.103+01:00 — Reference-app hosting for the workflow editor lives in `src/UmbracoPrism.MockBusinessApp/Program.cs`; `/workflow-editor` stays a thin authoring shell and the authoring API hangs off `/api/workflow-authoring/*`.
- 2026-05-18T13:17:12.103+01:00 — Explicit editor saves must persist both the authored JSON (`workflow-authored/*.workflow.json`) and the projected runtime seed (`workflow-seeds/*.json`) so reload and runtime stay aligned.
- 2026-05-18T13:17:12.103+01:00 — The live planning authoring seed at `src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json` must stay non-empty and keyed as `planning`; otherwise the reference host and seed contract tests drift immediately.
- 2026-05-18T13:17:12.103+01:00 — Runtime action execution now hangs off `src/UmbracoPrism.MockBusinessApp/Services/WorkflowActions/`; `WorkflowActionRegistry` reuses the editor catalog provider so discovery metadata and handler resolution stay aligned in the reference app.
- 2026-05-18T13:17:12.103+01:00 — `BusinessAppWorkflowEngine` is the place to orchestrate runtime action timing (`OnExit` → `OnTransition` → `OnEntry`) around state changes without pushing business-side handlers into `UmbracoPrism.WorkflowRuntime`.

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

