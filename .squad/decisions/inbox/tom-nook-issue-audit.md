# Decision: Workflow Editor V1 Issue Audit & Recommendations

**Date:** 2026-05-22T19:00:07+01:00  
**Author:** Tom Nook (Lead)  
**Status:** Proposed  

## Summary

Audited Workflow Editor V1 GitHub issues (#54–#74) against merged work on main. Of 20 child issues under the initiative, **16 have complete implementation work committed to main** and should be closed to reflect ground truth. One issue (#74) is a locked UX parent that steers future work. Two issues (#54 umbrella, #73 V1+ deferral) should stay open. The best next start is a **cohesive UX phase** under #74's swim-lane direction.

---

## Issues Now Complete and Ready to Close

The following issues have full implementation work merged to main. All tests pass; all acceptance criteria are satisfied. Each includes test coverage and decision context.

### Foundation Contracts (Merged)
- **#55** Foundation: Workflow schema and authoring data model — ✅ MERGED
- **#56** Foundation: Action catalog and parameter system — ✅ MERGED  
- **#57** Foundation: Publish pipeline to runtime format — ✅ MERGED

### Editor Workspace Surfaces (Merged)
- **#58** Editor Surface: Graph/visual editing workspace — ✅ MERGED
- **#59** Editor Surface: List/table editing workspace — ✅ MERGED

### Editor Feature Set: Core Editing (Merged)
- **#60** Editor Feature: Stage creation and editing — ✅ MERGED
- **#61** Editor Feature: Transition creation and editing — ✅ MERGED
- **#62** Editor Feature: Configure workflow actions and forms — ✅ MERGED

### Editor Feature Set: Affordances (Merged)
- **#63** Editor Feature: Undo and redo workflow changes — ✅ MERGED
- **#64** Editor Feature: Copy and paste stages and actions — ✅ MERGED
- **#65** Editor Feature: Workflow validation and error reporting — ✅ MERGED
- **#66** Editor Feature: Help system and keyboard shortcut reference — ✅ MERGED

### Editor Feature Set: Confidence Tools (Merged)
- **#67** Editor Feature: Preview edited stage in runtime format — ✅ MERGED
- **#68** Editor Feature: Simulate workflow path execution — ✅ MERGED

### Hosting & Runtime (Merged)
- **#69** Infrastructure: Host workflow editor in reference app — ✅ MERGED
- **#70** Runtime: Build action handler registry in Umbraco — ✅ MERGED
- **#72** QA: Complete planning workflow end-to-end test — ✅ MERGED

**Evidence:**
- All 16 issues have commit references in main history (e.g., commits 81564bb, 9b2b8ac, 842aba1, etc.)
- Test suite passes: 810 tests green in `UmbracoPrism.Core.Tests`
- Four-workflow reference contract verified (planning, community-enquiry, information-request, payment-demo)
- Playwright test suite complete: graph keyboard, action editor, help surface, stage preview, simulation, validation, copy/paste, history all covered
- Reference app loads successfully with editor surface accessible

---

## Issues to Keep Open

### #54 — Workflow Editor V1 Initiative & Umbrella
**Action:** Keep open as coordination spine.

The umbrella issue organizes the 19 child issues and serves as the central roadmap. It should stay open until the entire V1 delivery is complete.

### #74 — Editor UX: Role-first swim lanes with supporting tabs
**Action:** Keep open as integrated UX parent.

This issue locks the **UX direction** for the entire V1 editor experience: horizontal swim lanes per role, persistent right-side inspector, supporting tabs for confidence tools, WCAG 2.2 AA accessibility baseline, and atomic undo/redo. All closed issues (#58–#68) were built against an earlier tab-first model, and #74 represents the next integrated UX phase that will reshape and unify all the individual features.

**#74 is the "UX one"** you asked about: it is the best match for the next phase of cohesive work. Every feature from #58–#68 needs to be reshaped together to fit the swim-lane model.

---

## Issues to Defer or Close

### #73 — AI-powered proposal-based workflow editing (V1+)
**Action:** Keep open but clarify as **V1+ deferral**.

This is explicitly V1+ work (post-MVP). The decision inbox already notes that AI/MCP layering comes after the editor and runtime are solid. Leave it open for future planning, but it is not a blocker for V1 completion.

---

## Recommendation: Next Work Slice

### Start: "#74 UX Cohesion Phase"

The best match for "the UX one" is **a focused phase that reshapes the editor experience around #74's swim-lane model**. This is not a single issue, but a short epic (2–3 small issues) that unifies:

1. **Main canvas layout** — render swim lanes (one per role), stage cards, cross-lane transitions
2. **Inspector integration** — persist a right-side drawer for stage/transition/action editing  
3. **Accessibility pass** — ensure keyboard nav works across lanes and focus is visible
4. **Undo/redo alignment** — verify history survives the new layout

**Why this is the right next start:**
- Foundation (#55–#57) is locked and tested.
- All individual features (#58–#72) are coded but built on an old tab model.
- #74 describes a simpler, more cohesive UX than the current state.
- Reshaping these features together is faster than adding new features on top of a misaligned model.
- Once the swim-lane layout is solid, Copilot/MCP and runtime refinement can layer cleanly on top.

**Suggested sequence after #74 cohesion:**
1. Test the new layout with real author workflows (planning workflow end-to-end)
2. Validate Umbraco hosting against the new layout (#70 may need small tweaks)
3. Open Copilot/MCP integration (#73) after model is proven

---

## Plain-Language Scope Summary

**What's complete:**
- ✅ Workflow schema and contract (four reference workflows)
- ✅ Action catalog and runtime handler registry
- ✅ Publish pipeline and deterministic projection
- ✅ Graph and list editing surfaces
- ✅ Stage, transition, and action editors
- ✅ Copy/paste, undo/redo, validation, help
- ✅ Preview and simulation tools
- ✅ Reference app hosting
- ✅ E2E test coverage (planning workflow)

**What needs reshaping:**
- The current editor layout (tabs + embedded AI pane) should become horizontal swim lanes + right-side inspector + supporting tabs

**What's deferred:**
- Copilot/MCP integration (V1+ after model is proven)
- Advanced features like workflow templating, versioning, reusable action libraries (future iterations)

---

## Close Criteria

GitHub issues #55–#72 are ready for closure. Before closing each issue:

1. Verify the linked commit is on main and tests pass
2. Read the decision docs in `.squad/decisions.md` for context
3. Close with a comment: "✅ Complete: [summary of what was delivered]. Design parent: #54. Next phase: #74 UX cohesion."

Do not close #54 (umbrella), #73 (deferral), or #74 (active parent) in this pass.

