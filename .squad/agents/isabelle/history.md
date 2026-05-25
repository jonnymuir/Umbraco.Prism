# History: Isabelle (Frontend Dev & Accessibility Lead)

## 2026-05-25T14:34:44.680Z — Merged Gateway Editor Slice Implementation

**Spawn:** isabelle background agent  
**Task:** Build merged gateway editor slice (#83/#84/#85)  
**Outcome:** ✅ Complete

### Deliverables

- `prism-workflow-outline` — Left-side navigation tree for workflow structure (stages → transitions)
- `prism-confidence-tabs` — Tabbed confidence surfaces (Validation, Preview, Simulation, Help)
- `prism-help-panel` — Embedded shortcut reference (no modal needed)
- Extended `AuthoredTransition` with editor-only `fromGateway`/`toGateway` fields
- Inspector full support: gateway creation, editing, deletion
- Graph layout uses explicit gateway fields for accurate visual routing
- All keyboard navigation and ARIA patterns implemented

### Tests

✅ 7 gateway editor (Playwright) tests passing  
✅ Graph keyboard navigation: 5 passed  
✅ Stage preview: green  
✅ TypeScript build: clean  

### Decision Recorded

- Decision: "Merged Gateway Slice — Editor-Only fromGateway/toGateway Fields"
- Status: archived (frontend-only; backend alignment required before load-bearing)
- File location: `.squad/decisions.md` (2026-05-25T15:23:06.241+01:00)

### Cross-Layer Coordination

- Backend fields (`fromGateway`/`toGateway`) deferred to Blathers' publish pipeline decision
- Validation schema alignment required when backend extends `AuthoredTransition`
- All existing single-cursor preview and simulation preserved

**Orchestration log:** `.squad/orchestration-log/2026-05-25T14-34-44-isabelle.md`

---

## Recent Sessions (2026-05-23 to 2026-05-24)

### Session Summary

**Active:** Lane header clearance, viewport sizing, visual regression management, walkthrough CI alignment, behavioral test conversion

**Status:** ✅ All regressions fixed, visual baselines updated, CI gates green

**Key Outcomes:**
- Lane header clearance: `LANE_HEADER_OFFSET` 44→80, stage positioning verified by measured DOM geometry
- Viewport width fix: CSS changed to `fit-content; min-width: 100%` for rightmost lane visibility
- Visual baselines regenerated and committed immediately after layout changes
- Walkthrough CI failures resolved: heading assertions updated, stale marketing text removed
- Behavioral test conversion: replaced pixel-perfect `toHaveScreenshot()` with action-based assertions

**Quality Gate:** TypeScript ✅, build ✅, Storybook CI 33/33 ✅, keyboard spec ✅, visual regression ✅, walkthrough smoke ✅

See `history-archive.md` for detailed per-session records (2026-05-19 through 2026-05-24).

---

## Learnings

- 2026-05-25T15:23:06.241+01:00 — Treat #83's current gateway UI as partial scaffolding only: stages stay action-bearing work nodes, while diamond transition gateways must become named, editable routing nodes with lane-owned waiting info and accessible branch/merge authoring.
- 2026-05-25T14:17:36.055+01:00 — For editor-only gateway slices, bind split and join nodes to existing stage-to-stage branch and merge points in the graph so authors can see lane-owned gateways without changing preview, simulation, publish, or runtime execution semantics.
- 2026-05-25T09:54:48.365+01:00 — For workflow surface cleanup, derive lane meaning from actor and role gates, not a parallel `editorSurface` flag. Strip UI-only surface hints before project/publish requests, and when validation links jump to an issue from the Validation tab, switch back to Canvas so the inspector target is actually visible.
- 2026-05-25T12:49:20.153+01:00 — When moving the workflow editor from coarse front/back language to named lanes, keep the authored contract assignment-driven: expose one lane-owner input, derive list filters from the actual lane keys present, and keep graph/list labels on lane names rather than surface buckets.
- Platform-specific baselines add maintenance burden; deterministic font setup enables single baseline across platforms
- Behavioral assertions (what users can DO) are more robust than pixel-perfect snapshots for cross-platform testing
