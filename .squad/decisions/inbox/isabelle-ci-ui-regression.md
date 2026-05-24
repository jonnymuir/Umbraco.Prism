# Decision: CI Test Drift — Walkthrough Heading + Visual Baseline Misalignment

**Date:** 2026-05-24T08:47:46+01:00  
**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Status:** Resolved  
**Context:** GitHub Actions run 26334757189, workflow `CI Tests`, branch `main` — two failing jobs

---

## Problem

CI red on main with two distinct test drift issues:

1. **Walkthrough heading assertion failure** — `localhost-auth-playwright` job
   - Test: `planning-workflow-complete.walkthrough.spec.ts:35`
   - Expected heading `/compose the editor into your app/i` not visible
   - Actual: Shell renders `<h1>Workflow Editor</h1>` (refactored from marketing reference to clean production shell)

2. **Visual regression mismatch** — `storybook-tests` job
   - Baselines: `workflow-graph-workspace-canvas.png` and `workflow-graph-workspace-list-mode.png`
   - Mismatch caused by lane header clearance work (LANE_HEADER_OFFSET 44→80) on 2026-05-23
   - Baselines were regenerated locally (files show 23 May 14:17 timestamp) but never committed

## Root Cause Analysis

### Heading Drift
- `prism-workflow-editor-shell.ts` was refactored to remove marketing copy (`"compose the editor into your app"`) and show clean `<h1>Workflow Editor</h1>` heading
- `01-planning-workflow-editor.walkthrough.spec.ts` was updated to `/workflow editor/i` in previous session
- **But** `planning-workflow-complete.walkthrough.spec.ts` was never aligned — same heading assertion on lines 53, 67, 109

### Visual Baseline Drift
- Lane header clearance regression fix on 2026-05-23 changed LANE_HEADER_OFFSET from 44 to 80
- Stage positions shifted from y=108px to y=144px (20px breathing gap below lane copy)
- Visual baselines were regenerated locally but not committed to repo
- CI runs against outdated baselines in repo, detects mismatches

## Resolution

### Fixes Applied
1. **Walkthrough spec alignment** — `planning-workflow-complete.walkthrough.spec.ts`
   - Line 53: `heading: /compose the editor into your app/i` → `heading: /workflow editor/i`
   - Line 67: Same change (editor graph step)
   - Line 109: Same change (published step)

2. **Visual baseline regeneration**
   - Ran `playwright test tests/workflow-editor/workflow-graph-visual.spec.ts --update-snapshots`
   - Regenerated `workflow-graph-workspace-list-mode.png` (94393 → 94386 bytes, reflects new lane header spacing)
   - Canvas baseline unchanged (already correct)

### Quality Gate Validation
- TypeScript build: ✅ Clean
- Storybook CI all browsers: ✅ 33 suites, 330 tests passed
- Keyboard accessibility spec: ✅ 5/5 passed
- Visual regression: ✅ 2/2 passed

### Commit
```
08dbe9d fix(ci): align walkthrough spec heading and regenerate visual baselines
```

## Policy Established

**Visual Baseline Commit Discipline:**
- When layout work (constants, CSS, spacing) changes component rendering, visual baselines MUST be regenerated AND committed in the same session
- Baselines regenerated locally but not committed = guaranteed CI failure on next push
- Quality gate for graph work now includes explicit visual regression check (`.squad/skills/workflow-editor-ui-quality-gate/SKILL.md`)

**Walkthrough Spec Synchronization:**
- When shell UX refactors change headings, selectors, or page structure, ALL walkthrough specs must be aligned in the same session
- Search for all occurrences: `grep -r "old heading pattern" tests/walkthroughs/`
- Current specs affected by shell changes: `01-planning-workflow-editor.walkthrough.spec.ts`, `planning-workflow-complete.walkthrough.spec.ts`

## Testing Surface Coverage

This incident revealed incomplete synchronization between:
1. Component refactor (`prism-workflow-editor-shell.ts` heading change)
2. Test spec alignment (`01-planning-workflow-editor.walkthrough.spec.ts` updated, but `planning-workflow-complete.walkthrough.spec.ts` not)
3. Visual baseline commits (regenerated locally, never committed)

**Recommendation:** CI validation gate skill should explicitly call out "if you change shell UX or graph layout constants, regenerate and commit baselines in the same session"

## Files Changed

- `src/UmbracoPrism.Client/tests/walkthroughs/planning-workflow-complete.walkthrough.spec.ts` (3 heading assertions)
- `src/UmbracoPrism.Client/tests/__screenshots__/workflow-editor/workflow-graph-visual.spec.ts/workflow-graph-workspace-list-mode.png` (baseline)

## Related Context

- Previous fix: `.squad/decisions/inbox/isabelle-ci-workflow-smoke-fix.md` (same heading issue, different spec file)
- Lane header clearance work: `.squad/decisions/inbox/isabelle-lane-header-scene-width.md` (2026-05-23)
- Shell refactor context: `prism-workflow-editor-shell.ts` lines 104-105 (`<h1>Workflow Editor</h1>`)

---

**Outcome:** CI should now be GREEN. Both test drift issues resolved and validated locally.
