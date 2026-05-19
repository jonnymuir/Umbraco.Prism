## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---

# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## 2026-05-16T13:20:33 | Workflow Editor V1 — Umbraco Integration

**Editor hosting:** Hybrid model — v17 backoffice section (`prism-workflow-editor`) wrapping Lit/Web Component
**Surface mapping:** (1) Public: unauthenticated content shells, (2) Member: `PrismMemberCookie`-protected pages, (3) Back-stage: MockBusinessApp reviewer surface
**DocType strategy:** `workflowPage` and `workflowHub` stable Core-owned; V1 adds `workflowLanding`
**Auth boundary:** Umbraco backoffice gates editor, PrismMemberCookie gates member, MockBusinessApp role gates reviewer
**Key file:** `docs/design/workflow-editor-v1/03-umbraco-integration.md`

## 2026-05-16T23:17:22 | V1 Workflow Editor Backoffice Section Scaffold

**Files shipped:**
- `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/umbraco-package.json` — v17 package manifest
- `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/web-components/prism-workflow-editor-host.js` — Lit element
- `src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs` — 4 file-shape assertions

**Manifest:** 5 extensions (section, sectionSidebarApp, menu, menuItem, dashboard)
**Dev base-URL:** Reads `window.PrismWorkflowEditorConfig?.authoringBaseUrl` → 4-second fetch probe for reachability
**No build step:** Umbraco v17 resolves `@umbraco-cms/backoffice/*` at runtime; plain .js ESM files load directly

---

**📚 Older sessions (pre-2026-05-10) archived to `history-archive.md` to keep active history under 15KB.**

## Learnings

- The strongest Umbraco reference pattern here is a **thin typed Razor wrapper** in TestSite that selects Core workflow shells and partials, while leaving nonce handling, field validation, and workflow progression in Core/Business App boundaries.
- Seeded workflow/member journeys read more like a real Umbraco site when `workflowPage` and `workflowHub` live under `Home` instead of as root nodes; route ownership stays content-driven without changing the public URLs.
- Exact workflow-key lookup is safer than alias fallback once multiple workflow pages exist; missing content should fall back to the expected route contract, not to whichever workflow page happens to be first in the tree.
- The live MockBusinessApp authored seed (`src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json`) is a separate contract from the backend fixture under `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/`; keep the live file non-empty and keyed to the shell route (`planning`) so `/workflow-editor` stays loadable.
- Long-running LiveAppHost startup belongs behind an explicit Playwright worker-fixture timeout; `src/UmbracoPrism.Client/tests/support/shared-app-host-fixture.ts` now allows 10 minutes so cold Aspire warmups do not die at the default 30-second fixture limit.
- The workflow editor browser client still expects `stageKey`/`displayName`/`kind` and `fromStage`/`toStage`/`action`, so `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts` must normalize the canonical C# API shape (`key`/`title`/`type`, `source`/`target`/`trigger`) before rendering graph and inspector components.

---

## Session 2026-05-18T12:17:12Z — Issue #57 Completion

**Outcome:** brewster completed assigned work on issue #57 publish pipeline.

**Status:** Green end-to-end

---

## Session 2026-05-18T21:48:37Z — Issue #71 Status Check

**Context:** Asked to continue work on issue #71 (Runtime: Enable workflow runtime in Umbraco surfaces).

**Finding:** All acceptance criteria already implemented and tested:
- Route hijacking controllers exist (`WorkflowPageController`, `WorkflowHubController`)
- `WorkflowRuntimeEngine` provides instance resolution, state management, transitions
- `PrismWorkflowPageController<T>` handles GET/POST/PRG pattern with nonce validation
- Member auth enforced via `PrismMemberCookie` scheme
- 782 unit tests pass (349 workflow-specific)
- 6 E2E scenarios in `workflow-gds-journey.spec.ts` verify full journey

**Action:** Closed issue #71 with completion documentation.

**Status:** Green — repo fully operational for workflow runtime in Umbraco public/member surfaces

## 2026-05-18: Issue #71 Approval

Issue #71 "Workflow Runtime in Umbraco Surfaces" has been marked **acceptance-complete** by Tangy (quality gate review). All seven acceptance criteria are satisfied:

✅ Workflow start page loads in Umbraco
✅ Forms render for first stage
✅ Submit creates instance and advances stage
✅ Back-stage visibility enforced
✅ Instance state persisted correctly
✅ Resume/dashboard works
✅ Tests for planning workflow through Umbraco

Backend test suite: 782/782 passing (no blockers). Issue ready for merge.

## Revision Handoff (2026-05-19)

Workflow editor shortcuts slice: Tangy final review complete. Blocker: admin definitions page missing 'Edit workflow' link. Isabelle assigned for revision cycle.

---

## 2026-05-19T18:16:08Z: Admin-Page Edit-Workflow Link — LOCKED OUT (Tangy Re-review Rejected)

**Status:** 🔴 Blocked

The admin-page edit-workflow slice has been re-reviewed and rejected by Tangy. The blocker is a deep-link parameter mismatch: admin card clicks do not consistently open the editor to the same workflow definition the user clicked on.

**Your next steps:** Await Blathers' resolution of the deep-link alignment. Brewster is locked out until Blathers submits a new revision with the parameter mismatch fixed.

**References:**
- `.squad/log/2026-05-19T18-16-08Z-workflow-editor-selection-mismatch.md`
- `.squad/decisions/inbox/tangy-edit-workflow-link-final.md`
