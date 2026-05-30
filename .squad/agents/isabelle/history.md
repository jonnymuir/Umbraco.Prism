# History: Isabelle (Frontend Dev & Accessibility Lead)

## 2026-05-30T15:30:00+01:00 — Slice 3b.1: Gateway-First Route Editing + Closed TS StageKind

**Session:** named-lanes editor — Slice 3b.1
**Role:** Implementation (TS/Lit frontend; client-only — backend `[Obsolete]` shims from 3a absorb legacy inbound names)
**Branch:** `squad/82-named-lanes-editor-slice`

**Outcomes:**
- ✅ Package A — transition surfaces removed from canvas (drag-handle, create-dialog, context-menu, `'t'` shortcut, list-view row-action); routes are now authored exclusively on the gateway inspector's outgoing-routes panel (`_renderGatewayOutgoingRoutes` / `_renderRouteEditor`). Inspector `transition` selection branch deleted; new `selectedActionTransitionIndex` disambiguates per-route action editors.
- ✅ Package B — `StageKind` closed to `Question | CheckAnswers | Confirmation | TaskList`; legacy-kind normaliser-with-diagnostic pattern emits `stage-legacy-kind-rewritten` warnings and strips both the marker AND the `waiting` payload so PROJ140 accepts the save; outbound transition wire fields renamed to canonical `source`/`target`/`trigger` (inbound prefers canonical, falls back to legacy).
- ✅ New `gateway-route-conditions.ts` module split from inspector for focused route-condition helpers.
- ✅ New `data-prism-route-*` selector convention paired with `data-prism-route-index="${idx}"` enables one set of shared `_updateRoute*` handlers reading `event.currentTarget`.
- ✅ New tests: `workflow-stage-type-options.spec.ts` (Tangy SHOULD-FIX #5), `workflow-transition-editor.spec.ts` rewritten as Tangy #5 verbatim. Retargeted `workflow-editor-history.spec.ts:61` onto new `GatewayRepresentation` story for route label/delete undo/redo.
- ✅ `npx tsc --noEmit` clean; `npm run build` clean; `npm run build-storybook` clean; targeted Playwright specs green (gateway-route, retired-types, gateway×4, transition-editor, history).
- ✅ Verified 6 still-red editor-only specs (copy-paste, help, simulation×3, validation-rail) fail identically on baseline `HEAD` without my changes → pre-existing.

**Key notes / patterns to remember:**
- **Legacy-kind normaliser-with-diagnostic:** when widening becomes narrowing, ride a non-persisted marker on the domain object purely for editor-side diagnostic emission. Strip it (and any payload it gates) at the wire boundary or the server validator will reject.
- **`serialiseWorkflow` returns `Record<string, unknown>`** now — the local projection fallback (`projectWorkflowLocally`) consumes the pre-serialisation `workflow` directly so it can still read `fromStage`/`toStage`/`action`.
- **WorkflowSelection union collapse deferred** (build/tests green without it). Inspector no longer reads `_selectedTransitionIndex`; graph (edge highlight) and outline (row highlight) still do. Filed as Slice 3b.2 polish.
- **Breaking-change-but-not-really:** outbound wire rename is mitigated server-side by `[Obsolete]` setter shims on `AuthoredTransition.cs` (Slice 3a); only third-party SDKs that parse the publish *response* or replay captured POST bodies through a typed model will see the new shape.

**Validation jump UX for transition codes** still routes to source stage via existing graph behaviour; acceptable for this slice.

**Follow-ups:** `WorkflowSelection` union collapse, plus the 6 pre-existing editor-only spec failures (carry-over).

---

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

- 2026-05-26T19:58:39.416+01:00 — For slot-based workflow canvases, keep structural movement off the graph for the first slice: make the list/table workspace the canonical reorder surface, provide visible Move up/Move down controls plus keyboard shortcuts, and treat drag handles as optional enhancement rather than the only path. Persistent numeric order fields create extra validation and renumbering friction, so they should not be the primary authoring UX unless authors truly need jump-to-position editing.

- 2026-05-26T19:40:31.679+01:00 — For workflow canvases with lane columns, keep lanes as left-to-right columns and treat each lane as a slot matrix: stage rows for work nodes, connector rows for gateways, and local sub-slots that only expand where fan-out or parallel routing actually needs space. Ghost create affordances should be contextual, not permanent: show them only on the selected node’s next valid slot, the focused empty slot, or the current branch endpoint so authors can add routing without turning every empty slot into noise.

- 2026-05-25T22:04:00.819+01:00 — For gateway-heavy workflow canvases, compute row-band ranks from the visual stage↔gateway graph, size each lane from its widest row-band slot set, and draw authored routes as orthogonal rails so same-lane sibling gateways widen cleanly while cross-lane fan-out shares a short trunk before branching.

- 2026-05-25T16:48:28.029+01:00 — For gateway-first editor work, derive visual bindings from explicit route fields (`fromGateway`/`toGateway`) before heuristics, then hide route chips and stage handles once gateways exist so the canvas reads as stage → gateway → stage.

- 2026-05-25T21:57:06.676+01:00 — For readable workflow canvases, use a slot grid instead of free placement: stages occupy content rows, gateways occupy connector rows between them, same-lane fan-out consumes extra lane columns, and cross-lane routes should travel on shared connector rails so joins and gateway-to-gateway links do not turn into spaghetti.

- 2026-05-25T15:23:06.241+01:00 — Treat #83's current gateway UI as partial scaffolding only: stages stay action-bearing work nodes, while diamond transition gateways must become named, editable routing nodes with lane-owned waiting info and accessible branch/merge authoring.
- 2026-05-25T14:17:36.055+01:00 — For editor-only gateway slices, bind split and join nodes to existing stage-to-stage branch and merge points in the graph so authors can see lane-owned gateways without changing preview, simulation, publish, or runtime execution semantics.
- 2026-05-25T09:54:48.365+01:00 — For workflow surface cleanup, derive lane meaning from actor and role gates, not a parallel `editorSurface` flag. Strip UI-only surface hints before project/publish requests, and when validation links jump to an issue from the Validation tab, switch back to Canvas so the inspector target is actually visible.
- 2026-05-25T12:49:20.153+01:00 — When moving the workflow editor from coarse front/back language to named lanes, keep the authored contract assignment-driven: expose one lane-owner input, derive list filters from the actual lane keys present, and keep graph/list labels on lane names rather than surface buckets.
- Platform-specific baselines add maintenance burden; deterministic font setup enables single baseline across platforms
- Behavioral assertions (what users can DO) are more robust than pixel-perfect snapshots for cross-platform testing
- 2026-05-30T09:11:01.656+01:00 — Removed the obsolete `prism-conversation-pane` Web Component and its Storybook stories. References existed in the editor-host story, the planning walkthrough spec/docs, the workflow history spec, and a pane-only agent-loop stub spec; the stub spec was deleted and the incidental tests were rewritten around the remaining editor behaviours. Unexpected coupling was limited to skipped/stale proposal-flow tests rather than the live editor shell. Validation run: client build ✅, Storybook build ✅, targeted history spec ✅; walkthrough sanity skipped because the localhost-auth/Aspire prerequisite gate reported no Docker runtime.

- 2026-05-30T11:15:00+01:00 — Slice 1 scope-reset frontend: deleted `prism-proposal-diff.{ts,stories.ts}` and `workflow-authoring-mock-drafter.ts`; stripped modal markup/state (`_proposal`, `_modalOpen`), three handlers, `_closeModal`, the `_modalOpen ||` guard inside `_handleEditorKeydown`, the `prism-proposal-diff` CSS selector (kept `.modal-backdrop` because it is still consumed by the F1 `_renderShortcutGuide` dialog), the `ModalOpen` Storybook story, the `previewProposal`/`applyProposal` HTTP exports from `workflow-authoring-client.ts`, and the `ProposalEnvelope`/`ProposalAgent`/`ProposalOp`/`ProposalPlacement`/`STUB_PROPOSAL` types. **Hidden coupling notes:** (a) `ValidationResult` in `types.ts` was only referenced by `ProposalEnvelope`; I left it in place because it was outside the explicit deletion list and its removal would be a separate cleanup. (b) `nothing` from `lit` survives ~12 other call sites, so the import stays. (c) `.modal-backdrop` is the shared overlay primitive — any future shortcut/help dialog rewrite should preserve it or refactor both consumers together. **Accessibility note:** the removed overlay had backdrop-click-to-dismiss as its only escape affordance (no Esc handler, no focus trap, no `role="dialog"`). When stages+gateways re-introduce any modal surface, build it on the shortcut dialog pattern (`.modal-backdrop` + `role="dialog"` + Esc) rather than copying the proposal-diff scaffolding. **Pre-existing failures observed (not caused by this slice):** `workflow-editor-validation.spec.ts:8` (new "keeps detailed warning copy" assertion expects copy the current code doesn't render) and three `workflow-editor-simulation.spec.ts` tests (simulation start button reports `not visible`). Reproduced both on a clean stash of HEAD — pre-existing and unrelated to proposal-diff removal. Validation run: client build ✅, Storybook build ✅, targeted Playwright (graph-visual, graph-keyboard, editor-shell, editor-help, stage-preview) ✅; validation + simulation specs failed pre-existing tests only.

- 2026-05-30T11:55:00+01:00 — Slice 2 recovery attempt BLOCKED. Split the WIP per Tom Nook's audit into three stashes — `slice-5-canvas-slot-matrix` (stash@{2}, sha f6cbabb), `slice-3-inspector-outline-gateway-authoring` (stash@{1}, sha f782c03), `slice-3-gateway-only-model` (stash@{0}, sha 7c129f3) — leaving only the conversation-pane sweep + help/shortcuts/validation copy + walkthrough/history specs + design docs in the working tree (matched the expected MODIFIED/DELETED list exactly). **Surprise from the audit split:** Slice 1 (HEAD fc1acc5) shipped `prism-workflow-editor.stories.ts` with imports `LEAVE_REQUEST_STARTER_WORKFLOW` and `cloneAuthoredWorkflow` that only exist in the Slice 5 canvas WIP (stash@{2}, `fixtures/index.ts`). With Theme 3 stashed, `npm run build` fails on TS2305 for those two symbols, so Slice 2 alone cannot make HEAD type-check — the audit assumed the only HEAD breakage was the removed `ProposalEnvelope`, but there is a second dangling dependency. Stopped before Storybook/dotnet/Playwright/commit per task rules; stashes preserved untouched for Jonny to decide between (a) landing a minimal fixture shim in Slice 2 to expose those two exports from HEAD, (b) re-ordering so Slice 5 (or just the fixtures shim part of it) lands before Slice 2, or (c) reverting the stories import in Slice 2 to use `PLANNING_WORKFLOW` only. **Carry-over selectors/specs already flagged in Tom Nook's audit and re-confirmed unchanged in this attempt:** `workflow-editor-validation.spec.ts` expects copy that is not rendered anywhere in current source; `workflow-editor-help.spec.ts` expects empty-state copy that depends on Theme 3 canvas changes. Both remain pre-existing source gaps to be addressed in a later slice.

- 2026-05-30T12:15:00+01:00 — Slice 1.5 + Slice 2 SHIPPED. Followed Jonny's approved **Option 3** from the prior decision drop: trimmed `prism-workflow-editor.stories.ts` to depend only on the HEAD-available `PLANNING_WORKFLOW` export, replacing `cloneAuthoredWorkflow(LEAVE_REQUEST_STARTER_WORKFLOW)` in `makeEmptyWorkflow` with an inline `JSON.parse(JSON.stringify(PLANNING_WORKFLOW))` and renaming the displayName from `'Leave Request'` to `'Empty Workflow'`. The `GatewayRepresentation` story semantically required the gateway-shaped leave-request fixture, so it was removed with an inline comment flagging Slice 5 reinstatement (the fixture returns alongside the slot-matrix canvas). Slice 1.5 committed as **5a45a37** (single-file change). Slice 2 then committed as **32c872d** with the 18-file conversation-pane sweep + language reset + design/walkthrough doc realignment + skill/history bookkeeping. Validation run: `npm run build` ✅, `npm run build-storybook` ✅, `dotnet build UmbracoPrism.sln` ✅ (0W/0E). Playwright `workflow-editor-history.spec.ts` ✅ (Slice 2 positive proof). `01-planning-workflow-editor.walkthrough.spec.ts` 3/4 ✅ — the 1 failure is the `signIn`-gated `happy path: ... role-first workspace` test which can't run without the Docker/Keycloak/Aspire stack (same gap noted in my 2026-05-30T09:11 entry; not a Slice 2 regression). `workflow-editor-validation.spec.ts` + `workflow-editor-help.spec.ts` failed 2/5 as predicted in Tom Nook's audit (carry-over to Slice 5). All three stashes preserved untouched: `slice-3-gateway-only-model` (stash@{0}), `slice-3-inspector-outline-gateway-authoring` (stash@{1}), `slice-5-canvas-slot-matrix` (stash@{2}). Scribe owns the remaining untracked inbox/skills/health-report files. **Follow-up for Slice 5:** reinstate `GatewayRepresentation` story (and `LEAVE_REQUEST_STARTER_WORKFLOW` + `cloneAuthoredWorkflow` exports in `fixtures/index.ts`), and clear the carry-over validation/help spec selectors as part of the canvas slot-matrix work.

---

## 2026-05-30 — Scope-Reset Session: Slice 1/1.5/2 Execution

**Session:** workflow-editor-scope-reset  
**Role:** Implementation (frontend deletions, recovery, conversation-pane sweep)

**Outcomes:**
- ✅ Slice 1 frontend deletions (3 files deleted, 4 src + 2 docs edited, commit fc1acc5)
- ✅ Slice 1.5 stories trim (dangling import fix, PLANNING_WORKFLOW trim, commit 5a45a37)
- ✅ Slice 2 conversation-pane sweep (full pane removal, commit 32c872d, builds clean)

**Key Notes:**
- Discovered and fixed dangling stories imports after Slice 1 deletions
- Recommended Option 3 (trim stories to PLANNING_WORKFLOW) approved by team
- 3 git stashes preserved on branch (untouched, pending Slice 5)
- All targeted Playwright tests green across all commits
- Ready for parallel Slice 3a/3b work

- 2026-05-30T15:30:00+01:00 — Slice 3d a11y polish. Five small fixes against Tangy's editor-reset A11y review on the gateway-first inspector + outline (HEAD f133146): (1) outline transition target row now resolves `fromGateway`/`toGateway` keys through a local `_gatewayLabel` lookup so screen readers hear "Review split" not `review-split`; (2) `_routeDescriptor` in `prism-step-inspector.ts` returns structured Lit markup — visible `→` glyphs wrapped in `<span aria-hidden="true">`, and the wrapper carries a structured `aria-label` of the form "from {Stage}, via split gateway {Name}, via join gateway {Name}, to {Stage}"; visible text unchanged; (3) `.outline-gateway-button` now picks up the same 3px `#ffdd00` `:focus-visible` ring the stage/transition buttons use; (4) selecting a gateway from the outline now announces `"Selected gateway {Name}"` via the existing polite live region (`_announceHistory`) at the editor host — no new announcer added, per Tangy's WORTH-NOT-CHANGE #5; (5) outline gateway rows moved from sibling `<div class="outline-gateway-row">` into a proper `<ul class="outline-gateway-list"><li class="outline-gateway-item">` nested under the stage `<li>` so the DOM hierarchy mirrors the conceptual ownership — went with option (a) because there was no visual regression (kept all existing styles, just renamed the row container into a list container with matching padding/background). **WORTH-NOTING #6 verified cleared:** grep for `Waiting`/`StatusTimeline` in `prism-workflow-graph.ts` returns zero hits (Slice 3b.1 took care of both), and `workflow-stage-type-options.spec.ts` is in place and green. **Two new Playwright specs** in `workflow-editor-outline-a11y.spec.ts`: (a) Tangy new #1 — author changing a join gateway on a decision-join incoming route hears the change in `#inspector-announcer`; the test also asserts the current select option label reads "Decision join" (display name), proving the picker speaks domain language. The available-join-options logic restricts join offerings to routes whose `toStage === gateway.anchorStageKey`, so the test drives the clear path (announcement "Route now arrives directly at the target stage.") rather than a no-op re-set. (b) Tangy new #2 — outline DOM for the draft stage's outgoing transition row contains "Review split" and does NOT contain `\breview-split\b`. Validation run: client build ✅, Storybook build ✅, Playwright `workflow-editor-{gateways,outline-a11y,history,shell}.spec.ts` + `workflow-{stage-type-options,transition-editor}.spec.ts` 18 pass / 4 pre-existing skips. **One non-obvious finding for future slices:** `_availableJoinGatewaysForStage` filters joins by `binding.anchorStageKey === toStage` OR (anchor null && same lane). Because the binding's anchor is the post-join target stage, joins are only offerable on routes that land at that target — you cannot add a new join to a route by editing the source side. If Slice 4+ wants "pick join from any branch source's route", that filter needs to widen (e.g., compare lane keys irrespective of anchor) or add a separate `attach to existing join` affordance on the split gateway inspector.

- 2026-05-31T14:00:00+01:00 — Slice 4 visual lock + public surface declaration (HEAD e113bbb → branch `squad/82-named-lanes-editor-slice`). Six packages, executed 1→4→2→5→3→6 to keep the build green between each. **Package 1 (TestSite cleanup):** deleted `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/` (README, umbraco-package.json, web-components host). `Program.cs` already had no editor dashboard registration, csproj already excluded backoffice assets — the deletion was purely file-system. `dotnet build UmbracoPrism.sln` stayed clean (0W/0E). **Package 4 (linear mode retired):** ripped ~600 lines out of `prism-workflow-graph.ts` — `GraphMode`/`LinearFilter`/`ALL_LANES_FILTER` types, `mode` & `allow-linear-mode` @properties, `_linearFilter`/`_dragged*`/`_dragOver*` state, `_toggleMode`, `_visibleLinearStages`/`_visibleLinearGateways`, `_outgoingTransitionsForStage`, `_actionCountForStage`, `_actionSummariesForStage`, `_focusLinearRow`, `_setLinearFilter`, `_commitStageField`, `_moveStage`/`_reorderStageBefore`, `_handleListKeydown`/`_handleLinearRowClick`/`_handleLinearDrag*`/`_handleInlineEditor*`, `_jumpToValidationStage`, `_renderLinear`, `_renderValidationSummary`, the `./workflow-validation` import. Deleted `LinearMode` story from `prism-workflow-graph.stories.ts` and `tests/workflow-editor/vertical-lanes-switcher.spec.ts` (the spec name was misleading anyway — there is no orientation switcher). Stripped `.allowLinearMode=${false}` and `mode="graph"` from `prism-workflow-editor.ts`. **Package 2 (visual lock):** dropped `<span class="surface-tag">${layout.laneLabel}</span>` from both stage and gateway cards — lane info is structurally visible via columns and aria-label still carries `${layout.laneLabel} lane` for SR users. Stage `node-meta` simplified from `${kind} · ${laneLabel} lane` to just `${kind}`. Canvas validation banner already gone with `_renderValidationSummary` removal in Package 4. **Package 5 (selection collapsed):** narrowed `WorkflowSelection` union to `{kind:'stage'|'gateway'} | null` per Jonny's "the union is stage|gateway|none" — transitions are no longer first-class authoring objects post-3b.1. Replaced parallel `_selectedStageKey`/`_selectedGatewayKey` @state with one `_selection: WorkflowSelection`; added derived getters of the same names; kept `_selectedTransitionIndex` as a SEPARATE auxiliary state slot for edge highlight (it's a visual concern, not a selection), wired via a new `_applyTransitionHighlight()` helper. Pruned dead `_paste...` transition branch and the now-unused `AuthoredTransition` import. Bundle dropped 337KB → 311KB after Packages 1+4+2+5. **Package 3 (read-only viewer):** added two new properties to `prism-workflow-graph.ts` — `@property({type:Boolean, attribute:'read-only', reflect:true}) readOnly` (reflected so CSS/selectors target `[read-only]`) and `@property({type:String, attribute:'workflow-json'}) workflowJson` (parsed in `updated()` lifecycle with try/catch + `console.error` on failure, then assigned to `this.workflow`). In read-only mode the editor: gates all four dialog renderers + context menu + canvas `contextmenu` + stage/transition `contextmenu` handlers with `this.readOnly ? nothing : ...`; renders empty placeholders instead of Add stage / Add gateway HUD buttons; suppresses the empty-state Add first stage CTA with alternate copy; switches `aria-roledescription` to "viewer". Added `data-prism-read-only` attribute on the host. New `GraphReadOnly` Storybook story demonstrates a fully declarative HTML embed: `<prism-workflow-graph read-only workflow-json='...'>` with no JS wiring — exactly what Razor authors need. **Package 6 (public API surface):** declared three public elements and ONLY three — `<prism-workflow-editor>`, `<prism-workflow-editor-shell>`, `<prism-workflow-graph>` (now also a viewer). Wrote `src/UmbracoPrism.Client/src/workflow-editor/README.md` documenting attributes / JS-only properties / events / data hooks / read-only behaviour for each. Tagged the other eight elements (`prism-step-inspector`, `prism-confidence-tabs`, `prism-help-panel`, `prism-stage-preview`, `prism-workflow-simulation`, `prism-workflow-outline`, `prism-workflow-action-editor`, `prism-inline-help`) with `@internal Composition detail of <prism-workflow-editor>; not part of the public API surface.` JSDoc above their `@customElement` decorator so consumers see the boundary in IDE tooltips. Added a header in `docs/guides/workflow-editor-composition.md` linking to the new component README and re-stating the runtime-only + three-elements-only constraints. **Validation:** `npm run build` clean, `npm run build-storybook` clean, `dotnet build UmbracoPrism.sln` clean (0W/0E). Targeted Playwright validation spec failure (`workflow-editor-validation.spec.ts:8` — `[data-prism-canvas-health-hint]` element) verified to be **pre-existing on baseline e113bbb** — identical failure with the same retry pattern. Not a regression. **Two non-obvious decisions for future slices:** (a) Read-only mode does NOT also hide selection/zoom — viewers still need keyboard navigation and zoom for accessibility, just no mutation. If Slice 5+ wants a fully passive "print preview" variant, that's a separate attribute (e.g. `frozen` or `presentation`) on top of `read-only`. (b) `workflow-json` parser lives in `updated()` not `connectedCallback()`/`willUpdate()` because Razor can flip the attribute after mount (e.g., on workflow switch) — the parser must re-run on every attribute change, not just initial connect.

- 2026-05-30T19:23:00Z — Slice 5 canvas slot-matrix layout (HEAD 469b81f → branch `squad/82-named-lanes-editor-slice`). Three packages, ~620 net lines added on `prism-workflow-graph.ts`. **Stash discipline:** `stash@{0}` (`slice-5-canvas-slot-matrix`, ~2200 LOC) was treated as a design reference only — saved as `scratch-slice5-stash.diff` for offline reading, NEVER popped or applied. Reimplemented every primitive on the current Slice 4 baseline because the stash patches against the pre-Slice-4 world (linear mode, surface-tag, validation banner) and would have conflicted ~2200 lines. Scratch diff cleaned up before commit. **Package A — slot-matrix layout:** new constants `ROW_BAND_PITCH=152`, `LANE_INSET=28`, `SLOT_GAP=56`, `GATEWAY_TRUNK=36` replace the old `VERTICAL_GAP`/`GATEWAY_OFFSET`. New types `VisualNodeKind`, `rowRank` field on stage/gateway layouts, `VisualRouteLayout` carrying `routePoints[]` + `branch`/`merge`/`simulationPath` flags, and `visualRouteLayouts` on `WorkspaceLayout`. The `_layout` getter is fully rewritten as: (1) build node IDs `stage:<key>` / `gateway:<key>`; (2) build adjacency from gateway anchor edges + stage→fromGateway / source→toGateway / toGateway→targetStage transitions, falling back to direct stage→stage when no gateway is named; (3) Kahn topological sort; (4) parity step keeps stages on even rank, gateways on odd rank (final fixup ensures gateway-only-rank-0 still bumps to 1); (5) lane width auto-widens to widest row band (`max(LANE_WIDTH, LANE_INSET*2 + Σwidths + (n-1)*SLOT_GAP)`); (6) row band centre Y = `TOP_PADDING + LANE_HEADER_OFFSET + NODE_HEIGHT/2 + rowRank*ROW_BAND_PITCH`. Replaced the curved `_buildTransitionPath` with an orthogonal Manhattan helper suite: `_layoutCenter`, `_rowBandCenter`, `_slotOffset` (sibling x-corridor), `_gatewayAttachmentInset`, `_routeEntryPoint`/`_routeExitPoint`, `_railY`, `_pushRoutePoint`, `_normaliseRoutePoints`, `_pathFromPoints`, `_labelPositionFromRoute`, plus a new `_buildVisualRoutePath`. **Package A — render side:** `_renderGraph` now emits `[data-prism-route-path/-from/-to/-simulation-path]` SVG paths as the visible route rails (Slice 7 hook), with `marker-end` moved from chip paths to the rails. Stage/gateway nodes gained `[data-prism-stage-card]`, `[data-prism-gateway-node]`, `[data-prism-row-rank]`, and lanes gained `[data-prism-lane-container]` for Slice 7. Branch/merge classes are now ONLY on chip paths so `.edge-path.branch-path` count equals transition count, not 2×. **Package B — fixtures + stories:** added `cloneAuthoredWorkflow<T>()` (JSON deep-clone) and `LEAVE_REQUEST_STARTER_WORKFLOW` to `fixtures/index.ts` (gateways live on the `applicant` lane, three split branches into amendments/evidence/reviewer-assessment, three joins back to `decision-confirmed`, `waiting` payload only on the join). Rewrote `prism-workflow-graph.stories.ts` `GATEWAY_WORKFLOW` to a clone of the shared fixture, added `SAME_LANE_FAN_OUT_WORKFLOW` + new `SameLaneFanOut` story for the slot-matrix proof. Replaced `prism-workflow-editor.stories.ts:makeGatewayWorkflow()` (~100 inline LOC) with `cloneAuthoredWorkflow(LEAVE_REQUEST_STARTER_WORKFLOW)`. **Package C — tests:** `workflow-editor-validation.spec.ts` and `workflow-editor-help.spec.ts` revived by adding `[data-prism-canvas-health-hint]`+`[data-prism-open-validation]` strip below the editor statusbar (clicks switch `_activeConfidenceTab='validation'`), and adding "Add the next stage before you branch" to the empty-state tips list. `workflow-editor-gateways.spec.ts` adapted to the new fixture (lane=`applicant`, 3 branches not 2, removed the dead `List view` button click — Slice 4 retired list mode). Replaced `workflow-graph-layout-proof.spec.ts` (1013 LOC of stale viewport/scroll/header-clearance proofs) with 5 new behavioural invariants drawn from the stash: vertical lane columns + top-to-bottom flow, same-lane fan-out widens lane and gives sibling slot corridors, cross-lane fan-out reads stage→gateway→branch row→join→stage, no-overlap across both stories, every-node-inside-lane. Replaced `workflow-graph-visual.spec.ts` orphaned `list mode displays stages in editable table` with a small lane-rendering smoke. Deleted the two Slice-7-bound screenshot baselines (`workflow-graph-layout-baseline.png`, `workflow-graph-layout-scrolled.png`) so the viewer-vs-frame visual regression can be re-captured cleanly when Slice 7 lands. **Validation:** `npm run build` ✅, `npm run build-storybook` ✅, full `tests/workflow-editor/` Playwright pass — 54 passed, 11 skipped, 1 flaky-on-retry-then-passed (`workflow-action-editor.spec.ts:71`). `dotnet build UmbracoPrism.sln` clean (0W/0E). **Two non-obvious decisions for future slices:** (a) Branch/merge styling lives on the **chip path** only, not the route rail — the rail is the orthogonal Manhattan structural hop with `marker-end`, the chip carries the colour + `.branch-path`/`.merge-path` semantic class. If Slice 7's visual regression baseline wants the rail to be the coloured line, move the class but bake the count expectation into one place — chips and rails BOTH match `.edge-path` so the test must qualify with `.edge-chip` or `.route-rail`. (b) Parity-step in `_layout` lifts a same-kind successor by **+2** ranks and a cross-kind successor by **+1** — this is what makes adjacent-stages still leave a gateway-row gap (rank 0,2) and adjacent stage→gateway tighten to one band (rank 0,1). If Slice 6+ wants nested gateway compounds (gateway→gateway), the parity rule needs revisiting: today gateway→gateway lands on different parity ranks (+1) which is correct, but gateway→gateway→gateway will keep flipping parity which may produce visually surprising stacking. Decision file at `.squad/decisions/inbox/isabelle-slice5-canvas-slot-matrix.md`. Recommend `git stash drop stash@{0}` once a human verifies the merge.
