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
