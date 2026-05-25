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
