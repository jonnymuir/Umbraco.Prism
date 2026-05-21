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

## 2026-05-17: Workflow Editor Foundation & Reframe

### 2026-05-17T12:32:29.455640Z

Reviewed overall CI and E2E architecture; provided architectural recommendations for faster CI

### 2026-05-17T16:56:41.297+01:00 | Workflow separation review

- The extraction branch is **partial split**: authoring in `UmbracoPrism.WorkflowEditor`, but MockBusinessApp still owns too much composition.
- Clearest remaining coupling is host/composition, not model. RuntimeBackstage abstraction is the next gap.

### 2026-05-17T17:33:13.797+01:00 | Merge-readiness review for reference split

- PR #53 not merge-ready yet: GitHub shows `core-tests` failing despite local fixes. Uncommitted worktree changes block state validation.
- Walkthrough doc is partially documented (PNGs present but not embedded); doesn't reflect new reference-shell flow.

### 2026-05-17T20:02:23.686+01:00 | Workflow editor state audit

- Main contains **foundation/reference slice**, not full V1: shell page, graph/inspector/conversation Lit components, filesystem-backed store, deterministic projector, preview/apply endpoints.
- Natural-language path is demo scaffolding (one canned prompt, local fabrication); no workflow MCP server, no Copilot wiring.
- Projection/apply only partially wired: persists authored JSON but doesn't regenerate runtime seeds.
- Backoffice presence is thin iframe host in TestSite; implementation doesn't match final V1 topology yet.
- Clear summary: editor foundation + authoring model + projector + mocked proposal loop exist; real agent loop, MCP, Copilot, runtime publish, full authoring UX not yet present.

### 2026-05-17T22:05:30.472+01:00 | Workflow editor doc reframe

- Reframed `docs/design/workflow-editor-v1/README.md` around **three product concepts only**: workflow editor, workflow engine, forms engine (editor is V1 focus).
- Demoted publishing, Umbraco hosting, Copilot/MCP to secondary seams.
- Updated integration and agentic surfaces docs to match simpler framing.

### 2026-05-17T22:05:30.472+01:00 | Design rewrite batch — Workflow editor doc restructuring

- Completed full rewrite of `docs/design/workflow-editor-v1/` to establish three-product frame as primary narrative.
- Produced three decisions merged to `.squad/decisions.md`:
  1. **tom-nook-workflow-editor-doc-reframe.md** — README reframed around editor responsibilities and action-model split.
  2. **tom-nook-workflow-editor-simplification.md** — Doc restructuring (7-section pattern) + 6 follow-on implementation phases.
  3. **tom-nook-workflow-editor-state-audit.md** — Foundation/reference slice classification; sequenced: real agent plane → Copilot → runtime publish → UX completion.
- Pattern: three products first, implementation seams (projection, MCP, Umbraco, validation) demoted. Scope guardrails documented.

## Learnings

### 2026-05-17T22:28:34.036+01:00 | Workflow backlog sequencing

- **GitHub issues are the right execution layer** for Workflow Editor V1 delivery: keep `docs/design/workflow-editor-v1/` as source-of-truth design, use one initiative plus epic issues as coordination spine, and create 2–5 day child issues from the specialist sections.
- **Backlog shape should follow dependency seams**, not org chart alone: foundation contracts first (authored schema, action catalog, publish/apply contract), then runtime publish + validation, then editor workspace completion, then Umbraco hosting, then Copilot/MCP layering.
- **Parallelism starts after contracts lock**: UX workspace, runtime handler registry, preview/simulation, and backoffice shell can run in parallel once the authored model and publish boundary are stable.
- **Best immediate starters** are the contract-setting issues that unblock multiple lanes: authored schema freeze, action catalog/handler registry contract, and deterministic apply/publish path into runtime seeds.

### 2026-05-17T22:05:30.472+01:00 | Design rewrite learnings

- **Three-product frame is clearer** than plane-heavy story. Lead with workflow editor, workflow engine, forms engine; tuck implementation seams behind.
- **Editor owns full authored definition**: stage catalogue, graph transitions, action attachments, parameter editing, validation, preview, history, undo/redo, copy/paste, help. Raw runtime JSON stays as generated/debug artifact.
- **Action split**: design-time action descriptors vs runtime action handlers. Editor asks catalog what exists; engine resolves named handler at runtime. Reference app uses DI-registered handler registry (not lambdas) for testability and portability.
- **V1 demonstration shape**: MockBusinessApp stays as reference showing how to compose pieces; reusable logic moves to dedicated libraries. "Right tool for right job" remains explicit: Copilot CLI for NL drafting/orchestration, workflow-specific MCP tools for semantic ops, runtime never writes AI directly.

### 2026-05-17T22:21:16.980+01:00 | Conversational service-design architecture review

- **Recommended split**: Copilot is the conversational front door and orchestration shell; workflow-aware MCP tools own semantic draft/diff/validate/preview/apply operations; the workflow editor remains the human review and approval surface.
- **Trust model**: service-design-friendly AI requires proposal-first changes, semantic diffs, shared validation messages, preview/simulation before apply, and no hidden runtime mutations.
- **North-star UX**: one workflow-native workspace with graph/list + inspector + conversation/proposal pane + preview/simulation pane; no separate AI mode and no raw-JSON-first path for normal work.
- **Build order**: first ship the workflow-native editor and semantic MCP verbs; then wire Copilot/skills on top; then deepen replay/history and richer orchestration.
- **Key paths**: `docs/design/workflow-editor-v1/README.md`, `docs/design/workflow-editor-v1/04-agentic-surfaces.md`, `.squad/decisions.md`, `.squad/skills/workflow-editor-human-ai-coauthoring/SKILL.md`.

---

See history-archive.md for pre-2026-05-16 history.

### 2026-05-17T21:24:00Z | AI integration architecture evaluation

**Batch:** AI integration design  
**Decision published:** "Copilot + MCP should be the conversational service-design layer"

Evaluated overall architecture for conversational workflow/service design using Copilot + MCP + skills. Established north-star interaction model: one conversation inside the workflow editor workspace with proposal-first, reviewable, and auditable AI changes.

**Key outcomes:**
- Confirmed Copilot + MCP approach over bespoke AI stack for reusability and workflow intelligence in deterministic domain tools
- Defined build order: workflow-native editor surfaces → workflow MCP verbs → Copilot/skills integration
- Preserved editor-first trust model with no AI path bypassing editor review

**Peers:** blathers (tool surface design), scribe (orchestration)

---

## 2026-05-17T22:39:44.751+01:00 | Workflow Editor V1 GitHub issues creation

**User directive:** Scope correction — editor in reference app only, Umbraco for runtime only.

Created comprehensive GitHub issue set for Workflow Editor V1 delivery:

- **Umbrella issue #54:** Workflow Editor V1 Initiative (19 child issues)
- **Phase 1: Contracts & Foundation** (#55–#57)
  - #55: Workflow shape & data model
  - #56: Action catalog & parameter system
  - #57: Deterministic publish pipeline
- **Phase 2: Core Workspace** (#58–#62)
  - #58: Graph workspace (visual editor)
  - #59: List workspace (accessible editor)
  - #60: Stage editor
  - #61: Transition editor
  - #62: Action editor & forms-backed actions
- **Phase 3: Editor Affordances** (#63–#66)
  - #63: Undo/redo
  - #64: Copy/paste
  - #65: Validation system
  - #66: Help & keyboard shortcuts
- **Phase 4: Confidence Tools** (#67–#68)
  - #67: Preview panel
  - #68: Simulation/walkthrough
- **Phase 5: Hosting & Runtime** (#69–#71)
  - #69: Reference app hosting (editor runs here)
  - #70: Runtime action-handler registry (Umbraco)
  - #71: Workflow engine surfaces (Umbraco public/member)
- **Phase 6: QA & AI** (#72–#73)
  - #72: End-to-end tests & planning walkthrough
  - #73: AI-assisted editing (V1+, later)

**Key scope correction implemented:**
- Editor is standalone in reference app (MockBusinessApp host), not embedded in Umbraco
- Umbraco is runtime hosting only (public/member surfaces)
- All child issues use plain English (no jargon: "authoring surface" not "authoring plane", "reference app hosting" not "shell integration")
- Each issue includes explicit acceptance criteria and dependency tracking
- Squads assigned per charter: isabelle (UI/frontend), blathers (backend/infrastructure), brewster (Umbraco platform), tangy (QA/testing)

**Architecture & pattern decisions embedded:**
- Action model split: design-time catalog vs runtime handlers (issue #56, #70)
- Dual-surface model for accessibility: graph + list workspace (issues #58–#59)
- Deterministic projection: authored workflow → runtime format (issue #57)
- Editor-first workflow with validation, preview, simulation, undo/redo before publish (issues #57, #65, #67–#68, #63)

**Next steps:** Assign baseline issues to Isabelle and Blathers; evaluate V1+ AI work (#73) timing after baseline ships.

---

## 2026-05-17 | Backlog Sequencing: Workflow Editor V1 Execution Plan

**Status:** Completed  
**Timestamp:** 2026-05-17T22:28:34+01:00  

Transformed workflow-editor design into dependency-ordered execution backlog:

- **Initiative:** Workflow Editor V1 delivery
- **6 Epics:** Authoring contracts, runtime execution, editor workspace, Umbraco integration, AI/MCP support, QA hardening
- **Sequencing rule:** Lock authored schema + action catalog + publish/apply contracts first; then parallel runtime/workspace/backoffice lanes; Copilot/skills after MCP/CLI foundation; finish with QA
- **Immediate starters:** Freeze authored workflow schema, define action catalog contract, complete deterministic apply/publish path

**Artifact:** Decision merged to decisions.md: "Workflow Editor V1 — Execution backlog sequencing"  
**Handoff:** To Mabel for GitHub issue structure mapping; to squad for execution assignment per routing rules.


### 2026-05-17T22:34:01.015+01:00 | Plain-English workflow backlog framing

- Previous backlog wording was too abstract for delivery tracking. Terms like "projection foundation", "authoring contracts", and "backoffice shell" hid the actual user-facing work.
- Better backlog pattern for Workflow Editor V1: name epics and issues in product language first, then keep runtime, AI, and hosting as supporting work behind those labels.
- Must-have editor features need to be named explicitly in the backlog, not implied: copy/paste, undo/redo, stage editing, transition linking, action editing, validation, help, preview, and simulation.
- Key paths used for this reframe: `docs/design/workflow-editor-v1/README.md`, `docs/design/workflow-editor-v1/01-authoring-ux.md`, `docs/design/workflow-editor-v1/02-runtime-projection.md`, `.squad/decisions.md`.

### 2026-05-17T22:34:01.015+01:00 | Plain-English workflow backlog reframe

- Reframed Workflow Editor V1 backlog with plain product language (not architecture jargon).
- Renamed epics to user-facing concepts: "Ship Workflow Editor V1" → "Define what a workflow can contain" → "Save editor changes as a runnable workflow" → etc.
- Placed everyday features explicitly: copy/paste and undo/redo in affordances epic; linking transitions and stage editing in workspace epic.

### 2026-05-17T22:39:44.751+01:00 | Workflow Editor V1 GitHub Issue Set creation

- Created 20 GitHub issues (#54–#73) to move from design-doc planning to executable work units.
- Applied user scope correction: workflow editor stays in reference app (MockBusinessApp), Umbraco is runtime hosting only.
- Issue set structure: umbrella (#54) + 19 child issues across 6 phases (foundation, workspace, affordances, confidence, hosting, QA+AI).
- Squad routing: Isabelle (UI), Blathers (foundation), Brewster (Umbraco), Tangy (QA), Copilot (foundation contracts).
- All issues use plain-English product language; dependencies explicit ("Depends on" links); dual-surface accessibility built into acceptance criteria.
- GitHub issues now the execution spine; design docs remain architecture source of truth.


## Scribe Consolidation (2026-05-19T21:41:48.843Z)

Decisions consolidated into team decisions log. Orchestration recorded.

## 2026-05-19: Swim-Lane UX Parent Issue

### 2026-05-19T22:54:23.812+01:00 | Issue #74 created: Locked workflow editor swim-lane UX direction

Orchestrated parent GitHub issue #74 capturing integrated UX decision: role-first swim lanes as main editing model, tabs as supporting views, accessibility baseline, atomic undo/redo from first usable slice.

Decision routing: Future work on #58, #59, #60, #61, #63, #65, #67, #68 treats #74 as UX source of truth.

**Scribe update:** Decision inbox merged 2026-05-19T22:00:07Z.

## 2026-05-19 — Branch Hygiene Assessment: squad/55-workflow-schema-foundation

**Status:** ⚠️ Not ready to merge. Too broad. Recommend split.

**Assessment Scope:** Branch carries 10 commits (squad/scribe orchestration, green) but 62 uncommitted files (3 distinct engineering clusters) and 35 untracked files.

**Finding:** Three independent work streams are tangled:
1. Reference Workflow Repository (Backend: Blathers + Tangy)
2. Editor UX & Components (Frontend: Isabelle + Tangy)
3. Design/Docs/CI (Mabel + cleanup)

**Recommendation:** Split this into 3 focused branches immediately before check-in. Each branch should be independently testable and mergeable.

**Deliverable:** Full assessment written to `.squad/decisions.md` via inbox merge.

**Basis:** Tom Nook background agent (branch hygiene specialist).

## 2026-05-21 — Merge-Readiness Assessment: squad/55-workflow-schema-foundation (Final)

**Status:** 🟡 **Logically ready, not procedurally clean**

**Timestamp:** 2026-05-21T21:54:07.868+01:00

### Summary

The branch is **logically fit to land** once the working-tree is committed (169 uncommitted files). All green blockers are cleared:

- ✅ **Build:** Passes cleanly (6 pre-existing warnings)
- ✅ **Four-workflow contract:** All 6 tests passing
- ✅ **Workflow story coherence:** Code/docs/tests use consistent names for all four workflows
- ✅ **Reference implementation:** ReferenceWorkflowRepository provides all 4 as C# code fallbacks

### Workflow story verification

The four workflows (planning, community-enquiry, information-request, payment-demo) are:
- Defined in `ReferenceWorkflowRepository.cs`
- Documented in `docs/guides/reference-workflow-contract.md`
- Tested by `FourWorkflowReferenceContractTests.cs` (all passing)
- Consistent across authoring API, admin screen, and runtime engine

No contradictions found. The product story is coherent: the reference app seeds exactly four workflows through a canonical contract, enforced by tests.

### Recommendations

The branch needs three procedures before merge:

1. **Stage 169 uncommitted files** into logical commits:
   - Backend (Reference Workflow Repository + tests)
   - Frontend (Editor components + Playwright tests)
   - Architecture (Docs + design updates)
   
2. **Verify each commit** — build and test after each stage
3. **Merge with assessment context** — document why the four-workflow contract matters

The **"too broad" concern from 2026-05-19** is not a blocker; the three clusters belong together logically as issue #55 foundation work. Splitting would create artificial boundaries.

### Quality bar met

- ✅ Simple, durable seams — four-workflow contract is explicit and enforced
- ✅ No accidental complexity — ReferenceWorkflowRepository is straightforward
- ✅ Product story coherent — workflow definitions consistent across all surfaces

**Decision:** Written to `.squad/decisions/inbox/tom-nook-merge-readiness.md`

## 2026-05-21T21:54:07.868+01:00 — Merge-readiness verdict (tom-nook-5)

**Assessment:** squad/55-workflow-schema-foundation branch is logically ready for merge

**Key verdict:** Four-workflow contract satisfied, build green, story coherent. Working tree has 169 uncommitted files across three logical clusters. Recommendation: stage into focused commits before merge.

**Evidence:**
- ✅ Four-workflow contract: all 6 tests passing
- ✅ Build: green with only pre-existing warnings
- ✅ Story consistency: planning-application, community-enquiry, information-request, payment-demo defined in code, docs, tests
- ✅ Zero merge conflicts

**Staging procedure:** Organize into three commits (backend refs, frontend UX, docs), verify each commit, land with context about the four-workflow canonical contract.

**Decision doc:** `.squad/decisions.md` (merged from inbox/tom-nook-merge-readiness.md)
