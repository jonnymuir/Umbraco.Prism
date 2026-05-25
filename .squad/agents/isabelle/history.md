# History: Isabelle (Frontend Dev & Accessibility Lead)

## 2026-05-25T16:48:28Z — Gateway-Only Redo: Editor UX Rebuild

**Spawn:** isabelle background agent  
**Task:** Rebuild gateway-only editor UX  
**Outcome:** ✅ Complete

### Deliverables

**Decision: Gateway-first editor surface in the client** (2026-05-25T16:48:28.029+01:00)

- Canvas now reads: stage → gateway → next node (no direct stage-to-stage edges)
- Gateway shapes: diamond/diagonal nodes (remove rounded cards)
- Transition editor flows redirected through gateway routing
- Remove transition chips and stage route handles from gateway-first canvas
- Move waiting copy ownership onto join gateways
- Prefer explicit `fromGateway`/`toGateway` bindings over topology heuristics

### Visual Model Changes

- Outline + confidence-tabs preserved from prior slice
- Canvas topology visually reads as gateway-first routing
- List mode: gateway-first node representation (stages + gateways, no transitions)
- Inspector: gateway creation/editing centered on split/join routing intent

### Backend Gap Identified

Current contract lacks first-class stage↔gateway edges. Client can bind stage→gateway→stage through `fromGateway`/`toGateway`, but cannot author gateway→gateway or join→stage without hidden stage-to-stage transport underneath.

→ **Future work:** Backend should promote route endpoints to first-class stage/gateway references, or introduce explicit gateway-edge records.

### Validation Gate

Editor passes targeted tests checking canvas topology, diamond rendering, waiting copy attribution, and visual language unmistakably gateway-first.

### Orchestration Log

Written to `.squad/orchestration-log/2026-05-25T15-48-28-isabelle.md`

### Frontend TypeScript Alignment (Follow-up)

Dedicated slice needed to unify client transport model with backend gateway-only contract. Current assumptions in `types.ts`, `workflow-authoring-client.ts` remain hybrid.

---

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

## 2026-05-25T21:04:00Z — Canvas Layout Gate Cleared; Pending UX Implementation

**Task:** Frontend UX implementation phase for canvas layout slot grid  
**Status:** 🟡 Pending — awaiting implementation action

### Handoff Summary

Canvas layout geometry gate has been cleared by Tom Nook (revision owner) and Tangy (reviewer re-check):

- ✅ Same-lane sibling overlap fixed in route drawing
- ✅ Join-gateway branch overlap fixed in geometry
- ✅ Geometry tests measure real DOM slot readability
- ✅ Client validation lanes passed

### Implementation Charter

**Decision: Keep the workflow canvas clean by separating validation from layout** (proposed)

- Canvas tab focused on authoring and reading topology only
- Validation detail belongs on Validation tab (no warning duplication on Canvas)
- Canvas layout moves to **slot grid**:
  - Content rows for stages
  - Connector rows for gateways
  - Lane columns that widen for same-lane fan-out
  - Cross-lane fan-out using shared connector rails

**Decision: Gateway-first canvas draws unique adjacency rails** (proposed)

- Node placement: row-band / slot-grid based (unchanged)
- Route drawing: one orthogonal rail per visual adjacency
- Same-lane exit/entry: spread across node faces for separate slot corridors
- Join cases: branches stop at join boundary; one trunk downstream

### Required Changes

1. **Remove validation from Canvas tab** — Delete routing warning banner; use compact status line only
2. **Fix stage/gateway overlap** — Reserve connector rows; prevent stage card shifts
3. **Implement orthogonal rails** — Per-adjacency routes with clean elbows
4. **Same-lane slot sizing** — Widen lane locally when multiple gateways in same lane
5. **Cross-lane branching** — One shared trunk, then branch into target lane connector rows

### Test Coverage Expectations

Tangy will validate:
- Same-lane sibling gateways do not overlap
- Cross-lane branch work reads as branch row before join
- Canvas does not repeat Validation tab warnings
- Gateway-to-gateway and join routing follow connector rails

**Team coordination:** Immediate handoff to Isabelle for UX implementation  
**Decisions recorded:** `.squad/decisions.md` (5 canvas-related proposals)  
**Orchestration log:** `.squad/orchestration-log/2026-05-25T21-04-00Z-tom-nook.md`, `.squad/orchestration-log/2026-05-25T21-04-00Z-tangy.md`

---

## Learnings

- 2026-05-25T22:04:00.819+01:00 — For gateway-heavy workflow canvases, compute row-band ranks from the visual stage↔gateway graph, size each lane from its widest row-band slot set, and draw authored routes as orthogonal rails so same-lane sibling gateways widen cleanly while cross-lane fan-out shares a short trunk before branching.

- 2026-05-25T16:48:28.029+01:00 — For gateway-first editor work, derive visual bindings from explicit route fields (`fromGateway`/`toGateway`) before heuristics, then hide route chips and stage handles once gateways exist so the canvas reads as stage → gateway → stage.

- 2026-05-25T21:57:06.676+01:00 — For readable workflow canvases, use a slot grid instead of free placement: stages occupy content rows, gateways occupy connector rows between them, same-lane fan-out consumes extra lane columns, and cross-lane routes should travel on shared connector rails so joins and gateway-to-gateway links do not turn into spaghetti.

- 2026-05-25T15:23:06.241+01:00 — Treat #83's current gateway UI as partial scaffolding only: stages stay action-bearing work nodes, while diamond transition gateways must become named, editable routing nodes with lane-owned waiting info and accessible branch/merge authoring.
- 2026-05-25T14:17:36.055+01:00 — For editor-only gateway slices, bind split and join nodes to existing stage-to-stage branch and merge points in the graph so authors can see lane-owned gateways without changing preview, simulation, publish, or runtime execution semantics.
- 2026-05-25T09:54:48.365+01:00 — For workflow surface cleanup, derive lane meaning from actor and role gates, not a parallel `editorSurface` flag. Strip UI-only surface hints before project/publish requests, and when validation links jump to an issue from the Validation tab, switch back to Canvas so the inspector target is actually visible.
- 2026-05-25T12:49:20.153+01:00 — When moving the workflow editor from coarse front/back language to named lanes, keep the authored contract assignment-driven: expose one lane-owner input, derive list filters from the actual lane keys present, and keep graph/list labels on lane names rather than surface buckets.
- Platform-specific baselines add maintenance burden; deterministic font setup enables single baseline across platforms
- Behavioral assertions (what users can DO) are more robust than pixel-perfect snapshots for cross-platform testing
