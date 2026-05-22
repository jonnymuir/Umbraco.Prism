# Blathers — History Archive

[Archived from history.md due to size exceeding 15KB threshold on 2026-05-18T12:17:12Z]

**Summary:** Blathers completed deterministic publish pipeline implementation for issue #57, including preview/apply support, authored metadata preservation, and workflow endpoint testing. Previous work spans workflow engine redesign, Aspire integration, GDS implementation, and core backend infrastructure.
 Issue #70 Runtime Handler Registry Decision

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

