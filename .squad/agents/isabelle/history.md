# History: Isabelle (Frontend Dev)

## Current Work — 2026-05-23 (Recent Session)

**Active:** Workflow editor graph layout regression fixes and validation  
**Status:** ✅ All regressions fixed and regression-proofed

### Recent Outcomes

1. **Graph Layout Regressions Fixed** (2026-05-23T12:27:26Z)
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
