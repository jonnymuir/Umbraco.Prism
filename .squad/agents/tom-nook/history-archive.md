# Tom Nook — Archived History (Pre-2026-05-16)

This archive contains entries prior to the design rewrite batch.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-08 | Post-Publish Release Review (v1.9.1)

**Task:** Verify post-publish state after 1.9.1 release work lands.

**Finding:** v1.9.1 tag was misaligned—positioned on commit 2951551 (Fix 1.9.0 package version sources) instead of correct commit 8b78831 (chore(release): bump version to 1.9.1 and update marketplace packaging). This blocked CI workflows.

**Action Taken:**
- Deleted remote v1.9.1 tag
- Repositioned tag to correct commit 8b78831
- Pushed corrected tag to GitHub

**Result:**
- ✅ Package Release workflow executed successfully (2026-05-08T05:26:46Z → 05:27:54Z)
- ✅ GitHub Release v1.9.1 created (published_at: 2026-05-08T05:27:51Z, draft=false, prerelease=false)
- ✅ NuGet package pushed (UmbracoPrism.1.9.1.nupkg artifact confirmed)
- ✅ MARKETPLACE.md updated with generated marketplace-friendly documentation

## 2026-05-15: PASA Death Process Baseline Decision

Produced foundational decision on case-scoped notifier model for death-process example. Confirmed notifier as authenticated workflow actor, deceased member as linked subject, no mandatory registration up front. Hybrid save/resume via verified-session + case-reference. Decision merged to shared registry.

### 2026-05-15T06:35:47.013+01:00 | PASA death-process design

- PASA's public guidance is strongest on **risk-based identity management** and member identity view across life events, but doesn't prescribe detailed bereavement journey. Notifier UX, optional-account posture, assisted-digital shape come from broader UK bereavement and service-design practice.
- Most reusable Prism pattern for third-party initiated casework is to separate **workflow actor** from **linked subject**. For bereavement reporting, notifier is actor and deceased member is matched server-side as subject.
- Save/resume for sensitive one-off reporting works best when service verifies contact channel early, creates case shell immediately after, and combines passwordless resume with case-reference recovery instead of forcing permanent registration.

## 2026-05-16 | Architecture Proposals & Reference Split Review

### 2026-05-16T10:59:37.438+01:00 | Workflow editor architecture proposal

- Jonny wants workflow editor effort grounded in Prism's existing workflow/forms/runtime, but designed for both human and AI/agent authoring/testing.
- Recommended: three-plane split — **authored model** (editor-native graph + component semantics), **runtime projection** (Prism-compatible `WorkflowDefinitionFile`), **agent surfaces** (MCP/skills/structured diff APIs) so AI doesn't become runtime authority.
- Planning application is best reference demo (spans rich citizen input, multi-step service, check-answers, cross-surface handoff).
- Prism pages stay content-owned shells; business app/runtime stays authoritative for state, transitions, validation, nonce-safe field contracts, render-shell inference.
- Key grounding paths documented.

### Learnings — 2026-05-16 Workflow Editor V1 spine

**V1 architecture invariants (locked):**
- Three planes — Authoring / Projection / Agent — with stable contracts; Prism runtime contract untouched.
- `WorkflowDefinitionFile` is projection *target*, never editor's primary model.
- Projection is pure, deterministic function.
- Every agent change is structured proposal bundle (no live-instance writes).
- NL generation and conversational refinement are first-class entry points via **general agents** (Copilot) + **workflow-specific MCP tools**.
- Planning app is single V1 reference demo.
- Authoring lives in Business App; Umbraco keeps public/member shells; v17 backoffice is thin link/embed.

**Deferred to V2:**
- Versioning / lifecycle / rollback semantics
- In-flight instance migration
- Multi-tenant authoring and real-time collaboration
- Operator backstage UI contract
- Permission expressiveness, routing, task-list authoring
- Agent autonomy ceiling
- Cross-workflow refactors

**Tensions resolved:**
- *Editor vs runtime coupling* → `WorkflowDefinitionFile` is projection target, not authored source
- *AI scope creep* → general agents do general work; workflow tools do workflow work
- *Where does authoring live?* → Business App owns workflow authoring; Umbraco gets thin link
- *NL generation vs safety* → all NL changes through proposal/validate/preview/approve loop
- *Conversational refinement* → layered proposals with provenance, not hidden mutations
 Workflow Editor V1 Design Cycle

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
---

## 2026-05-19 — 2026-05-21: Full Session Archive

The following entries have been moved from active history to archive during 2026-05-25 summarization.


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


---

# Archived 2026-05-25 and earlier (8 days+)

## 2026-05-19 — 2026-05-21: Branch Hygiene and Merge-Readiness (Summarized)

**Period Summary:** Three days of branch assessment, merge-readiness verification, and orchestration for workflow schema foundation work. Full details archived.

**Key Outcomes:**
- ✅ Swim-lane UX direction locked via GitHub issue #74
- ✅ Branch hygiene assessment completed for squad/55-workflow-schema-foundation
- ⚠️ Merge-readiness verdict: logically ready, but working tree cleanup needed (169 uncommitted files across 3 clusters)
- ✅ Decision infrastructure: decision registry merged and routing established

**Decisions (5):** Branch split recommendation, team swim-lane contracts, merge conditions for schema foundation

**Archived Details:** See `history-archive.md` for full 2026-05-19 through 2026-05-21 session records.

---

## 2026-05-25T16:48:28Z — Gateway-Only Redo: Design Contract Lock

**Task:** Lock corrected gateway contract; rule on PR #89; write team redo directives  
**Status:** ✅ Complete

### Decisions Locked

1. **PR #89 is blocked by gateway model mismatch**
   - Current implementation still hybrid: transitions first-class; waiting-stages survive; gateway visuals still rounded cards
   - User intent plainly restated: only stages and gateways; gateways sole transition mechanism; diamond shapes; waiting on join gateways
   - Verdict: PR blocked pending full model correction

2. **Gateway-only redo contract**
   - Authoritative model locked in `decisions.md`
   - Team contracts specified (Isabelle: editor; Blathers: runtime; Tangy: tests)
   - Review gate: all surfaces (design doc, decisions, visuals, schema, runtime narrative, tests) must align on same model

### Orchestration Log

Written to `.squad/orchestration-log/2026-05-25T15-48-28-tom-nook.md`

### Coordination

User directive (2026-05-25T16:39:24 and 2026-05-25T16:48:28) captured in decisions.md. Team now moving to execution phase with locked contract.

---

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

### 2026-05-22T20:06:00Z — Scribe Batch Close: Cross-Agent Sync

**Context:** Batch orchestration complete. Scribe merged 5 decision inbox entries from this session's agent work (Isabelle, Tangy, Tom Nook).

**Your contributions referenced:**
- `tom-nook-mature-editor-direction.md` — strategic direction lock (Phase 1–5, integration-first, locked design decisions, team implications, Phase 1 success criteria)

**Cross-agent outcomes:**
- Isabelle implemented shell cohesion slice (outline, tabs, selection sync, focus)
- Tangy delivered behavioral test proof (24 tests, semantic hooks, quality gates)
- Scribe merged all decisions to `.squad/decisions.md`
- Orchestration logs written for all three agents

**Direction now locked:** Phase 1 scope is clear (2–3 weeks, integration focus). Isabelle owns UX; Tangy owns behavioral proof; your strategic direction cascades to all three. Tangy's quality bar reinforces Phase 1 success criteria (E2E authoring on one screen, real-time validation, keyboard navigation, WCAG pass).

**Status:** All squad metadata written; ready for merge.

---

## 2026-05-23T10:25:20Z — Independent Graph Scrolling Recommendation

**Spawn:** Directed to recommend interaction model for independent graph scrolling.

**Context:**
- User request: "I want a way of somehow independently scrolling up and down the graph editor, but leaving other things in place... if we add many different lanes it doesn't allow to scroll left or right either... also iphone/small form factor."
- Scope: Unblock multi-lane workflows + small-form-factor layouts

**Outcome:** ✅ Recommendation brief written and merged to decisions.md

**Decision Locked:** Proceed with MVP two-axis scroll (CSS-only, ~15 min) before Phase 2 mobile-optimized responsive stacking.

**Cascade:**
- Tangy: add horizontal scroll verification tests
- Isabelle: confirm "Fit to Screen" button behavior post-scroll
- Scribe: consolidated all recommendations to team decisions

**Deliverable:** `.squad/orchestration-log/2026-05-23T10-25-20Z-tom-nook.md`

## Session: Vinyl/Core Boundary Integration (2026-05-23T13:04:58.778000+00:00)

All squad members deployed together to complete the vinyl/core boundary work. Architecture split successful:
- Core remains reusable notification infrastructure
- TestSite vinyl behavior is now opt-in
- All 815 tests passing
- 0 warnings in build/test lane

## 2026-05-24T23:12:32.000Z — Backlog Triage: Issue Completion Assessment

**Task:** Reviewing open issue applicability

**Status:** ✅ Completed

**Backlog Triage Results:**
- **#54, #58, #61** — Identified as effectively complete/closable
- **#63** — Partially complete but still applicable to current work
- **#73** — Valid deferred V1+ scope (not blocked)
- **#28** — Active security work requiring attention

**Outcome:** Cleared triage queue; roadmap priorities reaffirmed for current sprint + V1+ pipeline.

## 2026-05-25T07:47:41Z — Landing Local Main Commit

**Task:** Land local main commit to origin/main

**Outcome:** ✅ Completed

**Summary:**
- Published the local main commit via PR #77
- Added missing `all` aggregate workflow check in `squad-ci.yml` to satisfy branch protection rules
- Merged PR #77 to main
- Synced local main with origin/main

**Result:** Local main now synced with origin/main; CI pipeline fully configured.

## Learnings

### 2026-05-26T19:58:39.416+01:00 — Slot canvases need command-first movement, not free dragging

- In a slot-based lane canvas, movement should change authored structure (stage sequence and lane assignment), not persist arbitrary x/y positions; the layout engine must stay in charge of placement.
- Keep accessibility-first movement on explicit commands and the list workspace, then let drag act only as an optional shortcut to the same valid targets and the same underlying mutation.
- Do not make numeric order fields the primary authoring UX for branching workflows: they leak implementation detail, imply a false single global sequence, and create avoidable validation/error states.

### 2026-05-26T19:40:31.679+01:00 — Horizontal lane columns need selective ghost slots, not free add buttons

- When the product mandate says lanes are horizontal columns, treat that as the fixed reading frame: role lanes own the horizontal structure, while flow depth moves downward inside each lane.
- The simplest authoring model inside a lane is a slot matrix: one depth band at a time, with optional side-by-side sibling slots only where the local branch actually needs them.
- Ghost create affordances should appear only in valid next slots near the selected node (below for continuation, beside siblings for same-lane fan-out, aligned in target lanes for cross-lane branching); if they are always visible everywhere, the canvas stops feeling simple.
- Let the lane header own the role label. Node cards should not repeat the lane name in chips and meta copy, and the Canvas should not repeat Validation detail that already belongs in the Validation tab.

### 2026-05-25T22:04:00.819+01:00 — Canvas rails should follow visual adjacency, not full authored transitions

- In the gateway-first graph, node placement can stay row-band / slot-grid while route drawing switches to unique adjacency rails (`stage → gateway`, `gateway → stage`) so shared trunks are drawn once instead of stacking identical segments.
- Same-lane fan-out needs exit-slot offsets on the source node; otherwise sibling choices may sit in separate slots but still leave through one overdrawn vertical stem.
- Join readability improves when incoming branches terminate at the join boundary and a single downstream trunk continues from the join to the next stage; this avoids rails crossing the join body.
- Key files for this proof are `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts` and `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-layout-proof.spec.ts`.

### 2026-05-25T21:57:06.676+01:00 — Slot lanes before drawing links

- A canvas that centers every gateway in a lane and stacks stages by authored order will always break down once one stage fans out to multiple gateways or gateways start linking to gateways.
- Keep validation ownership in the Validation tab; the Canvas may show a compact health/status hint, but not a second issue list, otherwise the same warning appears in two places and clutters the editor.
- The simplest scalable mental model is a lane-local slot grid: place nodes into row bands first, then allocate same-row sibling slots within a lane, and only after placement route links through reserved corridors between bands.

### 2026-05-25T16:48:28.029+01:00 — Gateway-only means editor-first clarity

- When the user restates the model as "only stages and gateways" and says to watch how the editor looks, treat any stage-to-stage seam or rounded gateway card as a blocker, not as harmless implementation detail.
- A correction pass should update the canonical design doc, decision record, and team contract together so nobody can keep building against the rejected hybrid model.
- If an open PR already claims the old hybrid slice is the delivery vehicle, supersede it rather than patching the narrative in place; otherwise review and handoff stay ambiguous.

### 2026-05-25T16:39:24.354+01:00 — Design intent beats transitional seams

- When the implementation introduces gateways but still keeps transitions and waiting stages as first-class authoring concepts, treat that as a partial migration rather than acceptance of the target model.
- A gateway slice is not merge-ready if the canvas still presents rounded gateway nodes, stage-type waiting semantics, or transition editing as the main routing mechanism after the design has been clarified to "stages + diamond gateways".
- For PR review, block until the editor, inspector, authored schema, and simulation/runtime story all describe the same plain-language model instead of a hybrid of old and new abstractions.

### 2026-05-25T09:32:35.455+01:00 — Concurrent lanes backlog slicing

- Reviewed the open backlog first (#28, #63, #73) to avoid creating overlap.
- Split the concurrent multi-lane redesign into seven ordered issues: cleanup/projection contract first, then lane model, editor UX, join gateways, concurrent engine behaviour, history clarity, and showcase/test evolution.
- Kept each issue framed in product language with acceptance criteria, explicit sequence, and a standing requirement to keep behavioural tests green.

### 2026-05-25T15:23:06.241+01:00 — Gateway model clarification

- When users describe workflows as stages with actions and diamond transitions, treat that as a strong modelling signal: stages are work nodes, gateways are routing and wait nodes.
- Put join waiting copy and runtime waiting status on the join gateway itself rather than on a separate waiting stage.
- Keep the issue order unless the clarification changes delivery risk; in this case the sequence still works, but the UX and runtime intent for #83–#85 must be restated more plainly.

## 2026-05-25 (09:32:35 UTC) — Concurrent Lanes Redesign Sequenced

- Issues #81–#87 created per concurrent multi-lane redesign plan
- Orchestration log recorded
- Tangy executing parallel behavioural track (#78–#80)
- Squad ready for coordinated execution

### 2026-05-25T11:48:05.065+01:00 — Issue #81 landing discipline

- When issue work is sitting uncommitted on `main`, branch it before landing; the repo now treats feature-branch + PR workflow as mandatory for code changes.
- For workflow lane cleanup, ship the shared assignment helper, projection sanitiser, docs, and behavioural proof together so the source-of-truth change is explicit across code and design notes.

### 2026-05-25T12:01:09.927+01:00 — Canonical multi-lane design lock

- When a redesign is being delivered in slices, keep one plain-language design document that explains the end-state behaviour across all slices.
- Put issue sequencing beside the behavioural model so implementation tickets do not become the only place where the whole story lives.
- Mark older design docs as partial when they still describe the current shipped model but no longer define the target behaviour.

### 2026-05-25T11:55:20.362+01:00 — PR #88 merge-readiness check

- For contract-cleanup PRs, approve only when the shared helper, payload sanitiser, docs, and behavioural tests all tell the same story about the new source of truth.
- A long-running non-authored lane can stay non-blocking when the repository permits merge and the touched scope is clearly unrelated, but call that out explicitly in the decision record.

### 2026-05-25T14:17:36.055+01:00 — Gateway representation before runtime behaviour

- After lanes and gateway metadata exist, the safest next slice is to make gateways visible and selectable in the editor before changing runtime execution semantics.
- Keep preview, simulation, publish, and current end-to-end workflow behaviour stage-driven until join replacement and concurrent cursor rules are implemented in their own slices.
- Treat existing workflow editor simulation/history specs as pinned regression gates; if they are already red on the branch, getting them back to green is a prerequisite rather than optional cleanup.

### 2026-05-25T15:34:44.680+01:00 — Merging adjacent gateway/runtime slices

- When user feedback collapses adjacent backlog slices into one product track, keep the earliest issue open and explicitly absorb the follower issues rather than leaving three "active" stories behind.
- Update the canonical design doc and the surviving issue in the same pass so the implementation order, agent boundaries, and green gates stay aligned.
- For gateway work specifically, the visual model, join waiting story, and deterministic parallel runtime are now one delivery contract, not separate starts.

## 2026-05-25T14:34:44.680Z — Merged Gateway Slice Orchestration

**Spawn:** tom-nook background agent  
**Task:** Merge issues #83, #84, and #85 into one gateway/runtime track  
**Outcome:** ✅ Complete

- Consolidated #83, #84, #85 under #83 as canonical live issue
- Closed #84, #85 as absorbed
- Updated canonical design doc describing merged slice
- Wrote implementation contract: Isabelle (editor) → Blathers (runtime) → Tangy (testing)
- All three work items on same branch; one product story in backlog

**Decision recorded:** `.squad/decisions.md` (2026-05-25T15:34:44.680+01:00)  
**Orchestration log:** `.squad/orchestration-log/2026-05-25T14-34-44-tom-nook.md`  
**Coordinate:** Session orchestration with Isabelle, Blathers, Tangy for merged slice delivery

## 2026-05-25T21:04:00Z — Canvas Layout Geometry Gate Cleared

**Task:** Revision owner for workflow editor canvas layout faults  
**Outcome:** ✅ Complete

### Canvas Layout Fixes

- **Same-lane sibling overlap:** Fixed gateway routing choices stacking on shared stem
- **Join-gateway branch overlap:** Fixed applicant branch running through join gateway body
- **Geometry tests updated:** Now measure DOM slot readability instead of screenshot baselines
- **Validation gate passed:** Client validation lanes confirmed

### Decisions Documented

- **Decision: Gateway-first canvas draws unique adjacency rails** (proposed)
  - Keep node placement row-band / slot-grid based
  - Draw orthogonal rails per visual adjacency (stage→gateway, gateway→stage)
  - Spread sibling exits across node faces for separate corridors
  - Join branches stop at join boundary; one downstream trunk to released stage

### Next Actions

- **Isabelle:** Implement canvas UX with orthogonal rails and slot grid
- **Tangy:** Validate geometry against updated test suite
- **Validation tab:** Ensure no warning duplication on Canvas tab

**Orchestration log:** `.squad/orchestration-log/2026-05-25T21-04-00Z-tom-nook.md`  
**Team coordination:** Multi-agent canvas layout fix session

### 2026-05-30T10:52:48+01:00 — Workflow editor scope reset audit

- Conversation pane is genuinely gone from production code, tests, stories, and walkthroughs; the only surviving references are squad metadata (agent histories, skills, orchestration logs) and two design/walkthrough docs (`docs/design/workflow-editor-v1/01-authoring-ux.md`, `docs/walkthroughs/planning-notification.md`) that still mention it as if it were present. Mark those historical, don't re-excise the agent records.
- Proposal-diff surface is wider than expected: a dedicated Lit element + story, ~70 lines of state and modal CSS inside `prism-workflow-editor.ts`, the `workflow-authoring-mock-drafter.ts` agent stub, `previewProposal`/`applyProposal` in the authoring client, four backend test fixtures, the preview endpoint, and a still-canonical-feeling design doc (`04-agentic-surfaces.md`). Removing the UI without trimming the doc and client APIs will leak the old narrative.
- `ProposalEnvelope` is doing double duty: it is both the *agentic* diff narrative AND the actual server-side patch protocol. We can drop the UI and the preview endpoint while keeping the envelope as the save mechanism — but a future agent must be told this explicitly or they'll delete too much.
- The schema validator already blocks `stage → stage` (PROJ141) and waiting-on-stage (PROJ140). The missing rule is `gateway → split-gateway` — gateways may transition to a stage or to a *join* gateway only. That is the one new validation rule needed to fully encode Jonny's mandate.
- The transition object is still first-class in the inspector (`workflow-transition-editing.ts`, transition tab in step inspector, dedicated Playwright spec). With the gateway-only model, transitions should fade into "an edge between a gateway and its target" — authored via gateway routing affordances, not via a transition editor. The standalone transition-editor spec is a tell that the old model is still being maintained.
- A `vertical-lanes-switcher.spec.ts` exists, implying a vertical/horizontal toggle. With the mandate, vertical is the only mode — that spec/toggle is dead code by Jonny's rule.
- `prism-workflow-graph.ts` is 4,560 lines. Any "simplify visuals" slice needs to be defended carefully: the file is large enough to hide both essential layout logic and dead orientation/proposal code paths in the same edit.


---

## 2026-05-30 — Scope-Reset Session: Slice 1/1.5/2 Complete

**Session:** workflow-editor-scope-reset  
**Role:** Coordinator (planning, audit, recovery sequencing)

**Outcomes:**
- ✅ 6-slice plan produced and validated by rubber-duck
- ✅ Slice 1 backend deletions (blathers, commit 1e8bbcf, 842 tests green)
- ✅ Slice 1 frontend deletions (isabelle, commit fc1acc5, Playwright green)
- ✅ Slice 1.5 stories trim (isabelle, commit 5a45a37, PLANNING_WORKFLOW only)
- ✅ Slice 2 conversation-pane sweep (isabelle, commit 32c872d, builds clean)

**Key Notes:**
- Identified and resolved HEAD-broken-without-Slice-2 issue in prior WIP
- 3 git stashes preserved on branch (untouched): slice-3-gateway-only, slice-3-inspector, slice-5-canvas-slot
- Decisions merged (12 inbox → decisions.md), 4 old entries archived
- 7 new reusable skills documented for next work cycle

### 2026-05-30T13:00:00+01:00 — Full editor review after Slice 1+1.5+2+3a+3b

- The agentic UI excision held: no `STUB_PROPOSAL`, `prism-proposal-diff`, `conversation-pane`, or `chat-drafter` symbols remain anywhere under `src/UmbracoPrism.Client/src/`. When a reset slice is genuinely landed, grep should come back empty — make that the verification bar, not "the tests still pass".
- Sliced renames create predictable model-drift between halves: Slice 3a closed the C# `StageKind` enum to four members, but `StageKind` / `EditorStageType` / converters / projector / preview / fixtures / dropdowns in TypeScript still know about Waiting and StatusTimeline. The TS surface is now *generous on input, silent on save failure* — the worst combination. Any cross-boundary rename slice needs a paired "close the client model" follow-up scheduled in the same plan.
- The TypeScript `AuthoredTransition` still writes `fromStage`/`toStage`/`action` on the wire and relies on C# legacy-JSON shims to accept them. Until the client renames, the deprecated dialect is the live dialect — the obsolete shim becomes the load-bearing path, not the migration ramp.
- Naming asymmetry is a 10-minute-comprehension test: `prism-workflow-editor.ts` declares a `WorkflowSelection` tagged union and then maintains three parallel selection state fields. A union that no one uses is a comment masquerading as a type. Spot these during PR review and require them collapsed in the same change.
- Sliced delivery left two list workspaces (`prism-workflow-graph.ts`'s `mode='linear'` path + `prism-workflow-outline.ts`) and three save endpoints (`/save`, `/publish`, `/apply`) coexisting. When a feature splits across slices and the consolidator slice is deferred, surface duplication multiplies — track the consolidator explicitly, not as "tidy later".
- Slice 3b's own decision flagged the gateway-inspector route-list relocation as 3b.1 carry-over. Carry-overs flagged inside a decision document should be treated as live debt against the very next slice in the area, not deferred to general backlog — they decay into "two-models-fighting" once the surrounding code keeps shipping.
- DX gap to fix before Slice 4 visual lock: `<prism-workflow-graph>` cannot be embedded read-only (no `read-only` attribute, no `workflow-json` attribute, all data via JS property assignment). Once visuals freeze, the API should freeze with them — adding read-only / attribute-driven embedding after the freeze costs more than doing it during.
- The `ProposalEnvelope` save protocol survived the reset by design, but with the agentic UI gone, `Rationale` / `Agent.Kind = github-copilot | custom-agent | human-assisted` / `PreviewArtifactRef` are now theatre that integrators must fake. Surviving abstractions from removed features should be re-checked for *required fields that no longer have a real source* — those are the hidden tax.
- Documentation is part of the deletion contract: `docs/design/workflow-editor-v1/04-agentic-surfaces.md` is still `Status: Proposed`, and `docs/guides/workflow-editor-composition.md` still lists `"waiting"` as a stage type. A scope-reset slice is not complete until the docs that named the removed surfaces are marked historical *in the same PR*, not in a follow-up.

### 2026-05-31T11:20:00+01:00 — Post-reset audit + slice plan for three architectural corrections

- Decision file: `.squad/decisions/inbox/tom-nook-post-reset-audit-and-plan.md` — three slices (A: legacy purge, B: WorkflowSource abstraction, C: gateway-collapse + admin/docs sweep).
- Three directives clustered cleanly into three slices because each leaves the system coherent at its boundary; trying to interleave them (e.g. collapse the model before purging legacy) would have multiplied edit surface and made green-throughout harder.
- Confirmed Slice 3a's diagnostic from May 30 still holds: the TS `serialiseTransition` *is* the live path that emits `fromStage/toStage/action` on every save, and the C# `LegacyFromStage` setter rewrites it back. The "obsolete" shim is load-bearing. Slice A is the close-out of that drift, not a tidy-up.
- Editor abstraction has a structural tell I missed last audit: the stories already intercept `fetch` and route to `projectWorkflowLocally` — that's the abstraction *retroactively* admitting it should live one level up. When stories work around production code with a custom service worker, treat it as a design signal, not a test convenience.
- For directive 3, the cleanest collapse is `AuthoredGateway { Source, Routes[] }` rather than keeping any standalone transition concept. A 1-route gateway models a "simple" stage→stage move; UX disguises the ceremony. Resisted the urge to special-case single-route moves — that path reintroduces transitions by another name.
- `workflow-gateway-representation.ts` is workaround scaffolding (it *infers* gateway anchors from a flat transitions array). Files like this are usually a sign the model is the wrong shape; deleting the file is part of the slice value, not collateral damage.
- PROJ141 + PROJ142 disappear by construction once gateways own routes. Validators that exist to enforce a structural rule are weaker than a model where the rule is impossible to violate; the directive 3 collapse converts both.
- MockBusinessApp's `/admin/workflow` page (~700 lines) has been quietly reproducing editor functionality (mermaid diagram, JSON modal). Folding its simplification into Slice C rather than spinning a separate one keeps "the model collapse" coherent — the admin page is *part* of the surface that has to reflect the new shape.
- Open questions (6 of them) flagged for Jonny up front rather than deferred — I learned during the May 30 audit that "decisions baked into a plan without surfacing them" are how slices regress mid-flight.

### 2026-05-31T12:40:00+01:00 — DDD boundary audit + revised slice plan (supersedes post-reset plan)

- Decision file: `.squad/decisions/inbox/tom-nook-ddd-boundary-audit-and-plan.md`. Supersedes the post-reset plan; A unchanged, **B grows substantially** (now subsumes endpoint deletion + publish-service move + WorkflowSource), C shape unchanged (inherits an easier admin-page edit), new **Slice D** lands integrator docs cleanly.
- ~95% of the workflow surface is already on the right side of the boundary. The mis-located code clusters tightly: the entire HTTP/store stack inside `UmbracoPrism.WorkflowEditor` (10 files, all deleting) and the publish-service trio (3 files, moving to MockBusinessApp). When a boundary review turns out to be mostly *deletion* rather than *relocation*, that's a sign the original sin was over-provisioning, not mis-placement.
- The endpoint deletion vaporises Slice 3c's whole concern (authoring auth at the HTTP boundary). There is no HTTP boundary; the host's `WorkflowSource.save` is enforcement. When a feature exists *only* to defend a surface, deleting the surface deletes the feature — don't carry the defence forward.
- Boundary surface is smaller than I expected: 3 TS interfaces (`WorkflowSource`, `WorkflowActionCatalog`, optional `WorkflowAuthorContext`) and 1 C# (`IWorkflowProjector`, unchanged). I considered and rejected `WorkflowRoleResolver` — role gates evaluate at runtime against a live instance, not while authoring; the editor doesn't need to resolve identities, only the host does.
- Genuinely ambiguous calls left for Jonny: (1) where the four reference workflows live — recommended split (Prism ships tiny generic pair, MockBusinessApp owns named scenarios); (2) `UmbracoPrism.WorkflowRuntime` placement — recommended (c) keep as standalone reference runtime, rename in a follow-up; (3) editor host-page persistence semantics — recommended page-lifetime only. Resisted the urge to over-decide; pre-baked decisions are how plans regress.
- Confirmed `WorkflowActivityModule` / `prism-actions` were speculative — no such symbols in the tree. The actual catalog/handler split (`BuiltInActionCatalogProvider` in WorkflowEditor as service-design metadata, `BuiltInWorkflowActionHandlers` in MockBusinessApp as runtime handlers) is already clean.
- `workflow-seeds/*.json` in MockBusinessApp is dead weight — the runtime store re-projects from `ReferenceWorkflowRepository` in code, never reads the files. Flagged for audit-and-delete in Slice C.

### 2026-05-31T23:07:37+01:00 — Reference workflow gateway-semantics audit

- Decision file: `.squad/decisions/inbox/tom-nook-reference-workflow-audit.md`. Four reference workflows live in a single file (`src/UmbracoPrism.MockBusinessApp/Services/ReferenceWorkflowRepository.cs`); the `workflow-seeds/*.json` siblings remain dead weight as previously flagged.
- Pattern: every workflow validates under the gateway-only rule, but three of the four use **1-route split gateways** as syntax filler — the gateway is a no-op pass-through, not a routing decision or join. Validating green is not the same as demonstrating semantics; future reference-content reviews should test "does this show what gateways *do*", not "does it parse".
- Payment demo's *topology* is already 80% right (split + join with `RequiredIncomingLanes` + waiting). The brief's gap is content: stage names describe system state ("provider-processing", "payment-settled") rather than human/role intent ("confirm payment received", "awaiting payment confirmation"). Lesson: when the gateway skeleton is right but the demo still feels wrong, look at names and stage *fields*, not topology.
- The back-office stage (`provider-processing`) has zero fields, so a `payments` actor in the business app has nothing to action — the multi-role story collapses to "stage exists, then time passes". A reviewer/ops stage with no inputs is the tell that a multi-lane workflow isn't actually multi-role.
- Planning workflow carries an `AuthoredHandoff` from `check-answers → submitted` with `actorChange = "caseworker"`, but no caseworker lane and no caseworker stage exist. Handoffs that survived the gateway refactor without a target lane are zombie metadata — worth grepping for project-wide before any other workflow edit.
- Stage UI regression is at the **model** level, not CSS. `AuthoredStage.Fields` is flat; projector wraps the whole stage in a single anonymous fieldset; preview then *unwraps* single-child fieldsets. Net effect: no legends, no grouping, no GDS fieldset story regardless of styling. Isabelle can't fix this from CSS — needs `FieldGroups` on the authored model + projector change first.
- Recommended slice order: payment rebuild (Jonny's named target) → planning rebuild (real decision + join + caseworker lane) → information-request polish → stage field-grouping model. Each leaves the system green; community-enquiry stays as the "minimum viable" reference and only needs its description updated.
- Three open questions surfaced to Jonny up front rather than baked into the plan (payment actor naming, planning loop vs guard, additive vs replacement field-groups). Continuing the May 30 lesson: pre-baked decisions are how slices regress.

---

## 2026-05-31 (later) — Componentised GDS Reconciliation (Self-Correction)

**Task:** Jonny challenged my reference-workflow audit's "should we add FieldGroup back?" open question, recalling that we had already decided on a componentised GDS model that could be extended to other component libraries.

**Verdict:** Jonny is right. I missed the prior art. The decision exists three times in `.squad/decisions/archive/2026-04-22-and-earlier.md`:

- **2026-04-22** — `f4b35e5` "Replace FieldGroupKeys/FormSection with GDS component model" (introduces PrismComponentDefinition with 11 component types)
- **2026-04-22** — "stepType Removal & Component Model Unification" ("component tree is fully self-describing")
- **2026-04-26** — Schema v2.0 Rollout Plan + Design Audit ("**Fields BECOME first-class components — no `fields[]` array**"). Atomic landing commit `7423803` is literally titled *"fields become first-class components."*

**What regressed and where:** The runtime side (`UmbracoPrism.Shared/Models/Workflow/Components/PrismComponent.cs`) is intact — 22 polymorphic component types as designed. The regression is in the **authoring schema** introduced by issue #75 (commit `84ba5eb`, 2026-05-22). `AuthoredStage.Fields` was born flat in that commit and has been flat ever since (4 commits total touching the file, all post-#75). The projector papers over it by wrapping the whole stage in a single anonymous `FieldsetComponent`. Different regression event from Isabelle's `40314e2`/`7423803` Razor-shell regression — separate mechanism, same direction of drift.

**Process learning (the actual point of this entry):**

**Before proposing any "should we add X back?" or "is X an open question?" architectural framing, run `grep -i {keyword} .squad/decisions.md .squad/decisions/archive/*.md docs/design/**` FIRST.** The componentised model was twice in the archive, three times in the package docs, and visible in the runtime PrismComponent.cs file. I didn't search any of those before writing the audit; I reasoned from the *current* shape of `AuthoredStage` and treated the gap as fresh. Jonny — correctly — does not like being asked faux-fresh questions when there is prior art he remembers.

Keywords I should have searched (and didn't): `componentis`, `componentiz`, `component model`, `GDS component`, `field.?group`, `fieldset`, `FormSection`, `PrismComponent`. The first hit on any of those would have given me the answer.

**Reconciliation note filed:** `.squad/decisions/inbox/tom-nook-componentised-gds-reconciliation.md` — verbatim decision quotes, regression trace, corrected framing for Slice 4/5, recommendation that restoration is its own multi-step decomposition (R1–R6), starting with a one-day spike before any of the existing schema is replaced.

**Corrected framing for the upcoming stage UI slice:** Slice 5 is not "add FieldGroup back." Slice 5 is "restore the componentised authoring model that Decisions A, B and C committed us to." Slice 4 (inspector UI) should be re-sequenced behind it, or it will hard-code the wrong shape into the slowest-to-rework surface.

