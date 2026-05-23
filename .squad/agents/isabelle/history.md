# History: Isabelle (Frontend Dev)

## Current Work — 2026-05-23 (Recent Session)

**Active:** Lane header clearance and viewport scene-width regression fixes  
**Status:** ✅ Both regressions fixed, all geometry proofs GREEN, visual baselines updated

### Recent Outcomes

1. **Lane Header / Viewport Width Regressions Fixed** (2026-05-23T13:24:52Z)
   - `LANE_HEADER_OFFSET` increased from 44 → 80: stages now start at y=144px (scene), giving 23px clear gap below lane header copy text (was −13px collision)
   - `.graph-viewport` changed from `width: 100%; height: 100%` → `width: fit-content; min-width: 100%; min-height: 100%`: bordered viewport now expands to encompass the full scene-frame width regardless of lane count; vertical and horizontal scroll preserved
   - `data-prism-lane-header={laneKey}` added to `.lane-header` div — Tangy can use this to measure actual rendered header geometry in layout proofs
   - Visual baselines updated: `workflow-graph-workspace-canvas.png`, `workflow-graph-layout-baseline.png`, `workflow-graph-layout-scrolled.png` — all regenerated to reflect new stage positions
   - Decision recorded to `.squad/decisions/inbox/isabelle-lane-header-scene-width.md`
   - Quality gate: TS build ✅, graph stories 3/3 browsers ✅, keyboard spec 5/5 ✅, layout proof 11/11 (3 skipped) ✅, visual graph 2/2 ✅

2. **Graph Layout Regressions Fixed** (2026-05-23T12:27:26Z)
   - Fixed vertical scroll not working for tall workflows
   - Resolved swimlane boundary overlap issues
   - Corrected graph-viewport/canvas sizing calculations
   - Width formula: `SIDE_PADDING * 2 + roleLanes.length * LANE_WIDTH + Math.max(0, roleLanes.length - 1) * LANE_GAP`
   - Height formula: `TOP_PADDING * 2 + LANE_HEADER_OFFSET + maxStagesInAnyLane * NODE_HEIGHT + Math.max(0, maxStagesInAnyLane - 1) * VERTICAL_GAP + TOP_PADDING`
   - Semantic hooks preserved for testing: `[data-prism-role-lane]`, `.graph-canvas` overflow contract, shell anchoring
   - All quality gates GREEN (TypeScript build, tests 12/12 passed, accessibility 5/5, visual regression 2/2)

2. **Graph Scroll Layout Recommendation** (2026-05-23T10:25:20Z)
   - Comprehensive diagnosis of vertical/horizontal overflow and narrow viewport failures
   - Recommended container hierarchy with CSS and responsive patterns
   - Accessibility planning: drawer focus management, keyboard shortcuts, screen reader support
   - Decision recorded to `.squad/decisions.md`

3. **Graph-Canvas Scroll Container Implementation** (2026-05-23T10:02:16Z)
   - Moved scroll container from `.graph-viewport` to `.graph-canvas`
   - Shell chrome (outline, inspector, toolbar) now anchored while workflow graph scrolls independently
   - TypeScript build successful, direct tests passed

### Earlier Phases (Archived)

Earlier work (2026-05-18 to 2026-05-23T10:02:16Z) archived to `history-archive.md`:
- Issue #65: Validation and error reporting infrastructure
- Issue #67: Runtime stage preview with projection
- Issue #74 Part 1: Role-first swim lanes
- Phase 2: Shell cohesion and browser-surface reset
- Phase 3: Tabbed layout redesign with Canvas as primary surface

See `history-archive.md` for full session-by-session record.

### Quality Metrics

- TypeScript: Clean build
- Tests: Workflow overflow 12/12 passed, shell 4/4, lanes 3/3, keyboard accessibility 5/5
- Visual regression: 2/2 passed
- Regression proof validation: 7/11 tests GREEN (4 regressions fixed as predicted)

### Next Steps

- User review of proof-based testing methodology
- Monitor mobile/responsive edge cases
- Consider keyboard shortcuts for tab navigation

---

## 2026-05-23T13:24:52Z — Lane Header Clearance & Viewport Width Final Fixes (Completed)

**Status:** ✅ BOTH REGRESSIONS FIXED AND VALIDATED BY PROOF TESTS

**What was delivered:**

1. **Lane Header Clearance Fix** — `LANE_HEADER_OFFSET` 44→80
   - Previous: Stage tops at `TOP_PADDING + 44 = 108px`, lane copy bottom ~124px → 16px collision (regression)
   - Fixed: Stage tops at `TOP_PADDING + 80 = 144px` → 20px clear gap below lane copy
   - Updated both stage y-position formula AND scene height formula to stay synchronized
   - Proof: Tangy's layout proof test shows stage at 144px, copy bottom at 124px → **20px breathing gap**

2. **Viewport Background Width Fix** — `.graph-viewport` CSS strategy
   - Previous: `width: 100%; height: 100%` → viewport pinned to scroll container visible width, clipped rightmost lanes
   - Fixed: `width: fit-content; min-width: 100%; min-height: 100%` → viewport now grows to match scene-frame width
   - Horizontal scroll on `.graph-canvas` preserved (overflow: auto retained)
   - Proof: Tangy's viewport test shows `viewport.clientWidth = 1024px` covering full 3-lane scene in shell context, scrollable to 1058px rightmost lane

3. **Test Hook Addition** — `data-prism-lane-header={laneKey}` on `.lane-header` div
   - Enables Tangy to measure actual rendered header boundaries in future layout proofs
   - Improves test robustness over magic CSS selectors

**Files modified:**
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts` (LANE_HEADER_OFFSET constant, viewport CSS, test hook)
- Visual regression baselines regenerated: 2 layout-proof screenshots + 1 graph-visual screenshot

**Validation (all GREEN):**
- TypeScript build: ✅ Clean
- Layout proof tests: ✅ 9/9 (non-skipped) GREEN
- Geometry validation: ✅ Stage positions, lane bounds, viewport coverage all verified by measured DOM geometry

**Decisions recorded:** 2 merged to decisions.md
- `isabelle-screenshot-regression-fix.md` (scene height fix, 2026-05-23T12:45:58Z)
- `isabelle-lane-header-scene-width.md` (header+viewport fixes, 2026-05-23T13:24:52Z)

**Team coordination:** Tangy provided measured proof tests that mathematically validate both fixes. This is the methodology shift from visual snapshots to measured DOM geometry for layout regressions.
