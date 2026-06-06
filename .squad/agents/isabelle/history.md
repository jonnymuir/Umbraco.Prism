## 2026-06-06: Migrated Workflow Rendering Validation

**Status:** ✅ Complete

All three Blathers-migrated workflows (planning, community-enquiry, information-request) rendered correctly in the editor after migration to the new queues/gateways/routes format.

**Fix:** Added `data-prism-lane=${layout.laneKey}` to stage `<button>` elements in `prism-workflow-graph.ts` — consistent with how gateway buttons already expose this attribute. Enables lane-membership assertions in Playwright without relying on DOM ancestry (stages are absolutely-positioned siblings of lane bands, not children).

**Tests:** 15 new Playwright tests in `tests/workflow-editor/workflow-migrated-workflows.spec.ts`, all green. Full suite: 90 passed, no regressions.

**Key insight:** Single-route Split gateways render as pills showing the trigger label as text; `displayName` is in `aria-label` only. Tests must check `aria-label` for gateway title assertions.

---

## Learnings

### 2026-06-06: Save error dismiss + Y-axis layout fixes

- **Dismiss button pattern:** When adding a dismiss action to a persistent error surface, always clear all related state fields together (both `_saveError` and `_saveErrorCopyStatus`) in the click handler. Use `aria-label` on icon-free buttons, `data-prism-*` for Playwright selectors, and match existing button classes for visual consistency.
- **Parity-stepped Kahn is fragile:** The parity adjustment (even ranks for stages, odd for gateways) breaks for cross-lane nodes that have no incoming edges within their own lane but are downstream of nodes in another lane. Longest-path (uniform step of 1, no post-adjustment) is the correct algorithm for Y-rank in a DAG layout. The `_rowBandCenter` formula and X-position logic are independent of rank parity and required no changes.
- **Preserve original `inDegree` map:** When running Kahn's sort, work on a copy (`new Map(inDegree)`) if the original map is needed intact for downstream logic — even if currently no downstream code reads it, this is safer practice.

---

### 2026-06-06: Y-axis cycle bug (Join gateway backward edge) + field-binding roundtrip guard

**Commit:** 97061d8

#### Y-axis root cause
The `_layout` getter in `prism-workflow-graph.ts` derived a Join gateway's `anchorStageKey` from `deriveGatewayBindings` — but in the new routes model the anchor is always an *upstream* stage (one of the stages that feed into the join), not the downstream merge target. The gateway entries adjacency loop then called `addEdge(gateway:join, stage:anchor)` — a **backward edge**. Combined with the natural forward edge `stage:anchor → gateway:join` from the transitions loop, this created a cycle. Kahn's algorithm cannot rank cyclic subgraphs, so every node in the cycle (and all nodes reachable only from it) stayed at rank 0, placing them all at the same Y coordinate.

**Secondary bug:** The `joinGatewayKeyByAnchorStage.get(transition.toStage)` fallback in the transitions loop mapped upstream stages to their join gateway, incorrectly intercepting routes to regular stages and adding more backward edges. The same anchor-based lookup in `transitionLayouts` was drawing transition chips through the wrong gateway.

**Fix:** Removed the `addEdge` for Join gateways in the gateway entries loop. Removed the `joinGatewayKeyByAnchorStage.get(…)` fallback from the transitions loop. Removed the `joinLayoutByAnchorStage.get(…)` fallback from `transitionLayouts`. The correct downstream edges are already built by the transitions loop from each gateway's own `routes`.

**Pattern to remember:** When deriving a Join gateway's "anchor" for adjacency, the anchor is upstream (a feeder stage), not downstream. Never use the upstream anchor as the target of an outgoing edge from the join.

#### Field-binding roundtrip guard
Added `EditorCanonicalJsonRoundtripTests` to `SeedFileRoundtripTests.cs`. Verifies that editor-canonical JSON (keys sorted alphabetically, `"type"` discriminator appearing *after* sibling properties like `"label"`, `"fieldKey"`, `"required"`) deserialises correctly through `AllowOutOfOrderMetadataProperties = true` and preserves label edits end-to-end. Confirmed `.NET 10` handles out-of-order discriminators natively with that option.

---


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

## Session: 2026-06-06 Save Error Orchestration

**Status:** ✅ Complete

**Isabelle contribution:** Replaced flashing save error toast with persistent inline error surface. Error surface shows title, summary, detail lines, reference id, with copyable text area and copy button. Integrated structured backend errors; sanitises fallback errors to prevent stack trace leakage.

**Team outcomes:**
- Blathers: Backend save validation and structured errors
- Isabelle: Persistent, copyable, sanitised error UI
- Tangy: 4-contract regression coverage

**Integration:** All decisions merged to .squad/decisions.md. Orchestration logged in .squad/orchestration-log/. Session log at .squad/log/2026-06-06T10-27-53Z-save-error-fix.md


## 2026-06-06: Commit 901fa79 — Save error dismiss + Y-axis layout fixes

- Fixed Issue 2: Added dismiss button to save error banner (prism-workflow-editor.ts)
- Fixed Issue 3: Replaced parity-stepped Kahn with longest-path algorithm (prism-workflow-graph.ts)
- Y-axis fix resolves cross-lane rank inheritance bug (payment-complete now renders at correct height)

All fixes validated by Tangy with new test coverage.

---

## 2026-06-06: Commit cfda4bd — Editor hydration for migrated workflow format

**Commit:** cfda4bd

**Task:** Validate and fix editor handling of the three Blathers-migrated workflows (planning, community-enquiry, information-request).

**Fixes applied in `types.ts`:**
- `normaliseStage`: accepts `key` as alias for `stateKey`, `title` for `displayName`, `type` for `kind`
- `normaliseGateway`: accepts `title` for `displayName`, `type` for `gatewayType`/`kind`, `waitingInfo` for `waiting` block
- `normaliseQueueDefinition`: accepts `title` for `displayName`

All changes are additive. Existing field names retain priority.

**Fixtures added (`fixtures/index.ts`):**
- `COMMUNITY_ENQUIRY_WORKFLOW` — single-lane, Split gateway
- `INFORMATION_REQUEST_WORKFLOW` — two-lane (applicant + caseworker), Split + Join gateways
- `PLANNING_WORKFLOW_MIGRATED` — single-lane, 3 Split gateways

**Stories added (`prism-workflow-graph.stories.ts`):**
- `PlanningMigrated`, `CommunityEnquiry`, `InformationRequest`

**Playwright tests added (`workflow-migrated-workflows.spec.ts`):**
- 15 tests across 3 workflows; all pass
- Covers: canvas loads, lane bands, gateway visibility, Y-axis spread, route edges, lane assignment via `data-prism-lane`

**Status:** ✅ TypeScript build clean, 15/15 Playwright tests passing. 17 pre-existing test failures unaffected.

### Key learnings

- Single-route Split gateways render as pills (showing trigger label, not displayName). The displayName is surfaced via `aria-label` — test against `aria-label` not text content.
- Stage nodes carry `data-prism-lane` (added by Blathers) but are NOT DOM children of `[data-prism-role-lane]` section elements — lane bands and stage nodes are siblings, absolutely positioned in the graph scene.
- `waitingInfo` is the field name used in C# Fixtures format; `waiting` is the field name used in the in-memory authored format. Both must be checked in `normaliseGateway`.

