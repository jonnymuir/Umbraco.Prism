# mabel History (Summary)

## Latest Updates

**2026-05-30 | Slice 8b — Docs sweep + delete obsolete manifest tests (scope-reset closeout)**
- Closed the workflow editor scope-reset arc (Slices 1–8).
- Banners added to historical design docs (04-agentic-surfaces, 03-umbraco-integration, README) and a narrower status note on 01-authoring-ux.
- Rewrote `docs/walkthroughs/authoring-a-workflow.md` as an Umbraco integrator recipe: packages → DI + WorkflowAuthor policy → doctypes → route-hijack `PrismWorkflowPageController<T>` → Razor templates → **where to host the editor** (MockBusinessApp; not the backoffice; not TestSite) → open the editor.
- Updated `planning-workflow-editor.md` to lead with vertical-lanes/slot-matrix language, explicitly state there is no in-editor chat/proposal-diff, and added a Definition tab step (Slice 6).
- Extended `docs/guides/workflow-editor-composition.md` with: rewritten top callout (editor lives in your business app, not in the backoffice or TestSite), a **Read-only public viewer** subsection with a one-line Razor `@Html.Raw(workflowJson)` example for `<prism-workflow-graph read-only>`, a **Definition tab** pointer, and a **Visual testing** pointer.
- Grep sweep across `docs/` + `README.md` for retired symbols (conversation pane, proposal diff, MockDrafter, prism-proposal-diff, IWorkflowPreviewService, StageKind.Waiting/StatusTimeline, App_Plugins/PrismWorkflowEditor, `/save`, body-side approver) — only stale survivor was the editor blurb in `docs/walkthroughs/README.md`, fixed. The lone `"waiting"` survivor in `workflow-setup.md` is the runtime forms-engine step type, not the retired editor stage kind — left alone.
- Deleted `src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs` — six tests asserting deleted `App_Plugins/PrismWorkflowEditor/*` files. Core tests now **860/860** (was 860/866 at Slice 8a baseline). `dotnet build` clean; `npm run build` clean.
- Decision file: `.squad/decisions/inbox/mabel-slice8b-docs-sweep.md`. Scope-reset arc explicitly confirmed complete.
- Learning: when scope reset retires whole features, banner the docs in place rather than delete — kept the design archaeology for 04-agentic-surfaces and 03-umbraco-integration so future decisions can see what was tried. Keep banners short and point at the new canonical doc.

## Older Updates

See history-archive.md for full history.

**2026-05-17 | Workflow Editor V1 Doc Polish for Clarity & Terminology**
- Reviewed all five design docs (README, 01-authoring-ux, 02-runtime-projection, 03-umbraco-integration, 04-agentic-surfaces) for consistency and clarity per user directive: "Keep it simple — workflow editor, workflow engine, forms engine."
- Fixed critical terminology errors:
  - 01-authoring-ux.md: "nodes = states" → "nodes = authored stages" with runtime clarification; "state's properties" → "stage's properties" throughout
  - 02-runtime-projection.md: Added opening "Key distinction" callout to reinforce Authored Model (stages) ≠ Runtime (states)
  - README.md: Rewrote TL;DR to explicitly name the three operational products (workflow editor, workflow engine, forms engine) aligned with user's mental model; added runtime counterpart column to repo mapping table
  - 03-umbraco-integration.md: Added clarifying note about Forms Engine vs Workflow Engine responsibilities across surfaces
- All changes preserve substantive architecture; only clarity and terminology consistency improved.
- No contradictions found requiring resolution; docs are architecturally sound.

**2026-05-17 | Design & Execution Artifact Structure Recommendation**
- Analyzed portfolio structure: docs, issues, decisions.md bridging pattern.
- Recommended complementary use of `/docs/design/` (narrative spine) + GitHub issues (execution units) + decisions.md (bridge).
- Key finding: Current Workflow Editor V1 spine is exemplary; scale horizontally without new structure.
- Lightweight maintenance: issue body copies doc snippet (5 min), decisions.md bumped at PR merge (2–3 min).
- Deliverable: `.squad/decisions/inbox/mabel-design-artifact-structure.md` — full recommendation with cross-linking template and issue granularity guidance.
- No file changes; recommendations only. Awaiting squad approval before extraction as team skill.

**2026-05-08 | v1.9.1 Release Completion**
- Published release v1.9.1 with marketplace-generated NuGet package readme
- Tagged commit 8b78831 (chore: release v1.9.1 with marketplace packaging)
- UmbracoPrism.1.9.1.nupkg successfully published to NuGet.org
- MARKETPLACE.md configured as PackageReadmeFile in UmbracoPrism.Core.csproj
- marketplace sync endpoint triggered at https://marketplace.umbraco.com/sync/umbracoprism
- GitHub Release v1.9.1 published with NuGet package asset
- Notes: Tag required re-push due to GitHub Actions timing issue with tag propagation

**Recommendations (No Changes Yet):**
- Crop homepage screenshot to ~2200–2400px (tooling)
- Create workflow-administration.md walkthrough (6+ admin panel screenshots)
- Capture missing backoffice screenshots (5 total across 5 walkthroughs)
- Verify/hide mobile helper if visible in walkthrough sessions (tooling)
- Document shared screenshot dependencies in skills

**Full audit report:** `.squad/agents/mabel/audit-walkthrough-docs-2026-05-04.md`

**Work Product:**
- Comprehensive audit identifying 6 issues (1 high, 3 medium, 2 low priority)
- Clear decision matrix: what's tooling vs. documentation vs. product
- Verification checklist for implementation
- No production files modified; recommendations only


## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.


**2026-05-08T05:26:48.026Z — Squad Sync:** v1.9.1 release recorded in decisions.md.
### 2026-05-17T22:05:30.472+01:00 | Design rewrite batch — Terminology polish and docs/issues coordination

- Polished terminology and cross-links across all five workflow-editor-v1 design documents for consistency (stage vs. state, three operational products named explicitly, engine roles clarified).
- Produced two decisions merged to `.squad/decisions.md`:
  1. **mabel-workflow-editor-doc-polish.md** — Terminology corrections (authored stages ≠ runtime states; explicit naming: workflow editor, workflow engine, forms engine).
  2. **mabel-design-artifact-structure.md** — Defined docs/issues/decisions coordination pattern: docs are source of truth (non-time-bound narrative), issues are execution units (2–5 day tasks), decisions.md bridges them. Lightweight cross-linking rules (5 min per issue, 2 min per doc update) and hygiene rules documented.
- All five design docs now internally coherent with consistent terminology. Structure pattern documented for future teams (see `.squad/skills/docs-issues-structure/SKILL.md` recommendation).

---

## 2026-05-17 | GitHub Issue Structure & Milestone Mapping: Workflow Editor V1 Backlog

**Status:** In Progress  
**Timestamp:** 2026-05-17T22:28:34+01:00  

Defined practical GitHub issue structure, naming conventions, labels, milestones, and routing rules aligned to Tom Nook's 6-epic sequencing and dependency constraints.

**Inputs:** Tom Nook's backlog sequencing decision  
**Deliverables:** Issue templates, label taxonomy, milestone structure, reviewer routing  
**Next:** Create GitHub issues and sequence per dependency requirements


## 2026-05-19 — Reference Workflow Contract Documentation Decision

**Status:** Proposed; decision merged to `.squad/decisions.md`.

**Deliverable:** `mabel-reference-workflow-docs.md` — Product-facing documentation for 4-workflow reference contract.

**Documentation Updates:**
- **Created:** `docs/guides/reference-workflow-contract.md` (200+ lines) — Comprehensive contract explanation, architecture narrative, verification checklist, quick reference
- **Updated:** `docs/walkthroughs/README.md` — Explicit four-workflow listing, removed planning-notification, reference updated
- **Updated:** `docs/walkthroughs/workflow-administration.md` — Admin panel workflow list now shows 4 workflows only
- **Updated:** `README.md` — Walkthrough references updated to planning-workflow-complete

**Clarity Achieved:** Removed confusion about the 5th workflow (planning-notification); established four-workflow contract as explicit product claim.

**Consequence:** Documentation now anchors E2E tests as enforcement mechanism; downstream developers see seam for plugging custom repository.

**Basis:** Mabel background agent submission to Scribe inbox.

---

## 2026-05-23T07:42:49Z — Session Orchestration & Decisions Integration

**Scribe orchestration completed:** All decisions merged to `.squad/decisions.md`; orchestration logs created.

**Documentation delivered:**
- `docs/guides/workflow-editor-composition.md` — Host composition patterns, simplest way, why hosts stay thin, configuration philosophy, building custom hosts
- `docs/guides/README.md` — Guide navigation and index
- `mabel-host-guidance-docs.md` — Decision merged to `.squad/decisions.md`

**Team status:**
- Isabelle: Tabbed layout redesign delivered; Canvas tab primary, confidence tools secondary
- Tangy: Layout professionalization behavioral proof delivered (22 tests); awaiting shell implementation
- Mabel: Host philosophy fully documented; reference shell now minimal and focused

**Philosophy established:**
- Documentation is source of truth for host patterns
- Reference shell is implementation example, not tutorial
- Thin shells, thick business logic (Prism principle)
- Easier to keep in sync: philosophy updates docs, shell design stays stable

**Decision:** Host is now production-ready for simple, focused editor mounting.

---

## Session: Vinyl/Core Boundary Integration (2026-05-23T13:04:58.778000+00:00)

All squad members deployed together to complete the vinyl/core boundary work. Architecture split successful:
- Core remains reusable notification infrastructure
- TestSite vinyl behavior is now opt-in
- All 815 tests passing
- 0 warnings in build/test lane

---

## 2026-05-25T11:48:05.065+01:00 | Issue #81 Documentation Updates

**Task:** Audit and update design docs to reflect issue #81's assignment-driven lane logic (removed duplicate front-stage/back-stage surface enum; lanes now derived from `actor` and `roleGates`).

**Changes Made:**
1. **01-authoring-ux.md § 7.4** — Clarified that front/back-stage placement is **derived** from actor and role gates, not a separate authored field
2. **README.md § 4.1** — Added explicit paragraph documenting lane-derivation logic (actor + roleGates → visual lane placement)
3. **02-runtime-projection.md § 7** — Documented that UI-only fields (editorSurface) are stripped before projection, leaving only assignment data

**Verification:**
- Confirmed code stripping editorSurface before serialization (`workflow-authoring-client.ts`)
- No contradictions between design docs and shipped behaviour
- Walkthrough and integration docs already use correct runtime surface terminology

**Deliverable:** `.squad/decisions/inbox/mabel-issue-81-docs.md` — Complete record of changes and rationale.

**Key Finding:** Assignment-driven lane logic is a clean seam for future lane redesigns (new actors, role gates) without mutating published workflows.

