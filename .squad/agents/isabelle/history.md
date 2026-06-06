## 2026-06-05: Queue-only model implementation completed

- Tom Nook: Contract definition and implementation plan locked
- Tangy: Behavioural test specification and validation gates defined
- Isabelle: Editor refactor completed (build ✅, Playwright suite ✅)
- Blathers: Runtime refactor completed (core test suite ✅)
- Team coordination: All decisions merged to .squad/decisions.md
- Next: Full integration validation and cross-stack testing

---

## 2026-06-01 — Queue-Model Slice: Editor Queue-First Wiring

**Session:** queue-model slice  
**Branch:** Shared queue model implementation  
**Commit:** 4f500f6

**Task:** Update shared workflow editor and shell to accept host-supplied `availableQueues`; shift queue-first language through UI layers.

**Status:** ✅ Complete

### What Changed

- **prism-workflow-editor:** Now reads `availableQueues` from host setup (e.g., TestSite passes `["web-user"]`, MockBusinessApp passes `["admin"]`).
- **prism-workflow-editor-shell:** Shell component accepts `availableQueues` prop and wires it to editor for demonstration.
- **Editor copy:** Author-facing UI now talks about queues instead of lanes where the editor surface or host-facing API exposes that concept.
- **Canvas grouping:** Uses explicit `queueName` from stages + `availableQueues()` from host to group stages by queue (not inferred from `actor` or `roleGates`).

### Implementation Details

- Queue labels and picker options come from host-supplied queue catalog first, authored workflow data only as fallback.
- Internal helper/type names still use some `lane*` identifiers where that does not leak through the host or authoring surface — no forced global rename.
- Runtime authorization and queue access rules remain out of scope for this slice (Blathers' responsibility).

### Validation

- ✅ `src/UmbracoPrism.Client npm run build` — clean, no TypeScript errors
- ✅ Playwright workflow-editor-shell spec — validates queue wiring with mock availableQueues

### Known Issue

Payment demo editor shows validation false-positives (Join gateway `flattenRoutes()` bug). Separate slice required to fix normalisation and validation. See: Payment Demo Editor Inspection decision.

---


## 2026-06-04: Flattened Workflow Model Session

**Agents:** Tom Nook, Tangy, Isabelle, Blathers  
**Session:** Queue-first architecture consolidation  
**Decision:** Single `WorkflowDefinition` contract approved

**Key outcomes:**
- AuthoredWorkflow + WorkflowDefinitionFile + ProjectedWorkflow → single canonical schema
- Lanes renamed to queues; laneKey + queueName merged to single identifier
- Gateways elevated from metadata to first-class definition elements
- Editor and runtime both use persisted contract directly

**Team coordination:** Decisions merged from 4 agents into `.squad/decisions.md`

**Isabelle contribution:** Flattened client editor onto persisted workflow definition; Definition tab and visual editor now use same contract; client build and Playwright tests passed.

## Session: 2026-06-06 Graph Cleanup Orchestration

Scribe processed team decisions and orchestration from this session's work. All three agents' outcomes documented in decisions.md. Session included payment-demo backend simplification (Blathers), client save path and graph UI fixes (Isabelle), and regression contract clarification (Tangy).

