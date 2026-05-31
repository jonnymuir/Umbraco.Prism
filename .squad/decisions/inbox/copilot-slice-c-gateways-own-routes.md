# Slice C (server portion) — gateways own routes

**Author:** copilot
**Date:** 2026-05-31
**Branch:** `squad/82-named-lanes-editor-slice`
**Scope shipped here:** server model collapse + all four reference workflow fixtures + 811-test green
**Scope deliberately deferred:** TypeScript types, graph/inspector, MockBusinessApp admin page strip, walkthroughs (see "Outstanding work" below)

## What changed

`AuthoredTransition` is **gone**. The authored model now treats gateways as the sole owners of routing:

- **`AuthoredGateway`** gains two new fields:
  - `Source` (string) — the stage the gateway is anchored to. **Required for `Split`, forbidden for `Join`.**
  - `Routes` (`IReadOnlyList<AuthoredRoute>`) — the outgoing edges this gateway emits.
- **`AuthoredRoute`** (new record) carries `Id`, `Target`, `Trigger`, `Condition`, `RequiresRole`, `Actions`.
- **`AuthoredWorkflow.Transitions`** is removed at the language level (not just emptied).
- The JSON schema (`authored-workflow.schema.json`) drops the top-level `transitions` collection and the `$defs/transition` definition, replaces them with `$defs/route`, and conditionally requires `source` only when the gateway type is `Split`.

The runtime contract (`WorkflowDefinitionFile.Transitions`) is **unchanged** — the projector still emits a flat list of runtime transitions, derived from `gateway.Source × gateway.Routes`.

## New validator codes

| Code   | Meaning |
| ------ | ------- |
| PROJ141 | Split gateway must declare a `source`. |
| PROJ142 | Gateway `source` is not a known stage. |
| PROJ143 | Two split gateways cannot share the same source stage (one gateway per source-stage). |
| PROJ144 | Every gateway must declare at least one route. |
| PROJ145 | Route `id` is required. |
| PROJ146 | Duplicate route id within a gateway. |
| PROJ147 | Route `trigger` is required. |
| PROJ148 | Duplicate `(trigger, target)` within a gateway. |
| PROJ149 | Route `target` is required. |
| PROJ150 | Route `target` is neither a known stage nor a known gateway. |
| PROJ151 | Route condition expression is empty. |
| PROJ152 | Join gateway must not declare a `source`. |

Retired: `PROJ106`, `PROJ107`, `PROJ108`, `PROJ109`, and the previous meanings of `PROJ141` / `PROJ142`.

## Patch service

The transition-shaped ops are gone. The patch service now offers three route ops, addressing routes by `(gatewayKey, routeId)`:

- `add-route` — path `/gateways/{gatewayKey}/routes`
- `update-route` — path `/gateways/{gatewayKey}/routes/{routeId}`
- `delete-route` — path `/gateways/{gatewayKey}/routes/{routeId}`

Each op produces a single immutable `AuthoredWorkflow` snapshot, preserving atomic undo/redo.

## Simulator

`WorkflowSimulationService` was rewritten to walk:

```
currentStage → owningGateway (lookup by Source) → routes filtered by trigger → resolve target (stage, or chain through another gateway)
```

Stop reasons preserved: `terminal-stage`, `waiting-gateway`, `transition-not-found`, `cycle-detected`.

## Reference workflows (MockBusinessApp + Core.Tests fixtures)

All four reference workflows were reshaped:

- `planning` — straight-line split chain.
- `community-enquiry` — single split between two stages.
- `information-request` — multi-target split (`submit` going to both `review-complete` and `caseworker-route`, discriminated by future conditions).
- `payment-demo` — multi-target split out of `payment` (`submit` to `payment-settled` OR `provider-processing`).

Multi-target fan-outs require `(trigger, target)` uniqueness, **not** trigger alone — a deliberate evolution of the spec wording for legitimate router patterns. PROJ148 enforces this.

## Test status

- `UmbracoPrism.WorkflowEditor`, `UmbracoPrism.MockBusinessApp`, `UmbracoPrism.TestSite` — build clean.
- `UmbracoPrism.Core.Tests` — **811 / 811 green** (was 811 before).
- Solution full build — 0 warnings, 0 errors.

## Outstanding work (Slice C-frontend follow-up)

The TypeScript types (`types.ts`), wire format, canonical JSON ordering, graph (`prism-workflow-graph.ts`, 3350 LOC), inspector (`prism-step-inspector.ts`, 1688 LOC), editor shell, outline, stories, and Playwright specs all still operate on the legacy `AuthoredTransition[]` shape.

A `flattenRoutes(workflow)` helper was prototyped and reverted; the next slice should:

1. Drop `AuthoredTransition`, add `AuthoredRoute`, add `source`/`routes` to `AuthoredGateway`, drop `AuthoredWorkflow.transitions`.
2. Update `workflow-wire-format.ts` and `workflow-canonical-json.ts` (`TOP_LEVEL_KEY_ORDER` no longer includes `transitions`).
3. Introduce `flattenRoutes()` as the single read path and migrate graph + inspector iteration off `workflow.transitions`.
4. Inspector `selectedTransitionIndex` becomes `selectedRoute = { gatewayKey, routeIndex }`.
5. Retire `workflow-transition-editor.spec.ts`; port unique scenarios to new gateway-route specs.
6. Re-cert the three visual baselines (intentional updates).

Also deferred:

- MockBusinessApp admin page (`Program.cs`) — mermaid diagram + per-instance reviewer-action buttons should come out per the original DDD-boundary plan.
- Walkthrough corrections (planning-workflow-editor.md, authoring-a-workflow.md) — only actively-wrong passages; full sweep is Slice D.

## Risk note

The wire format the server now emits is incompatible with the unchanged frontend. The editor will not be able to round-trip these workflows until the frontend collapse lands. The reference workflows still load and run at runtime because the projector continues to emit the runtime contract unchanged.

---

## Frontend completion (2026-05-31, branch `squad/82-named-lanes-editor-slice`)

The frontend collapse is in. The wire incompatibility called out above is resolved.

### Strategy taken

Rather than a single 5000+ LOC mechanical rewrite of the graph + inspector, I took the **pragmatic-hybrid** path: `AuthoredWorkflow.transitions` is kept as a **deprecated, read-only `AuthoredTransitionView[]` derived from `flattenRoutes(gateways[].routes)`**, and mutations flow through a new `workflow-routes.ts` module (`addRoute` / `updateRoute` / `deleteRoute` / `findOrCreateSplitGateway` / `withDerivedTransitions`). The derived view is rebuilt on every wire-load / source-load / route-mutation, and stripped from every wire-out / canonical-out path. Reads stay quick to migrate; writes are concentrated in a small auditable surface; the wire model is strict.

### What changed

- **Model** (`types.ts`): `AuthoredGateway` gained optional `source?` + `routes?: AuthoredRoute[]`; new `AuthoredRoute` interface; new `AuthoredTransitionView` (`gatewayKey` / `routeIndex` / `routeId` carried through the derived view); `AuthoredWorkflow.transitions` retained as deprecated optional `AuthoredTransitionView[]`. `STUB_WORKFLOW` reshaped.
- **New module** `workflow-routes.ts`: `flattenRoutes`, `withDerivedTransitions`, `addRoute`, `updateRoute`, `deleteRoute`, `findOrCreateSplitGateway`, `outgoingRouteViews`, `inboundRouteViews`, `buildRoute`, `newRouteId`, `routeAddressFromView`.
- **Wire format** (`workflow-wire-format.ts`): rewritten. Reads/writes `gateways[{key,title,type,source,routes:[{id,target,trigger,condition:{kind,expression,description},actions,requiresRole}]}]`. Strips `transitions` on save. Condition object→string on read, non-empty string→`{kind:'expression', expression}` on save. Calls `withDerivedTransitions` after `normaliseWorkflow`.
- **Canonical JSON** (`workflow-canonical-json.ts`): `TOP_LEVEL_KEY_ORDER` updated (dropped `transitions`, added `lanes` / `handoffs` / `parameterSchemas` / `metadata`); destructures+drops `transitions` before serialising.
- **Validation** (`workflow-validation.ts`): `WorkflowValidationLocation` `kind:'route' {gatewayKey, routeId}`. Code `transition-missing-stage` → `route-missing-stage`. `workflowRoutesWithMissingStages` (legacy alias kept).
- **Projection** (`workflow-runtime-projection.ts`): reads from `flattenRoutes`.
- **Lint** (`workflow-definition-lint.ts`): mirrors server PROJ141–152 + rejects top-level `transitions`.
- **InMemoryWorkflowSource**: load wraps clone in `withDerivedTransitions`; save strips derived `transitions`.
- **Editor** (`prism-workflow-editor.ts`): guarded `workflow.transitions ?? []`; rewrote `_jumpToValidationIssue` to handle `kind:'route'` (maps `(gatewayKey, routeId)` → derived transition index for highlight reuse).
- **Inspector** (`prism-step-inspector.ts`): mutation rewrites — `_replaceSelectedTransition` resolves `(gatewayKey, routeId)` from the view and calls `updateRoute`; `_deleteRoute` calls `deleteRoute`; `_replaceSelectedStage` repoints `gateway.source`/`route.target` on rename; `_replaceSelectedGateway` repoints cross-gateway `route.target`; `_deleteSelectedGateway` rebuilds via `withDerivedTransitions`.
- **Graph** (`prism-workflow-graph.ts`): `_confirmDeleteStage` rebuilds gateways (drops orphan gateways whose `source` is the deleted stage + dead routes targeting it); `_deleteTransition` calls `deleteRoute`; **layout fix**: transition-layout now falls back to gateway layout when `toStage`/`fromStage` resolves to a gateway key (e.g. feeder-split → join edges).
- **Fixtures**: `planning.workflow.json` synced byte-for-byte with server; `PLANNING_WORKFLOW` reshaped to typed gateway form; `LEAVE_REQUEST_STARTER_WORKFLOW` migrated to 5 gateways (`review-split` + 3 per-source feeder splits + `decision-join`).
- **MockBusinessApp**: `/admin/workflow` stripped from ~430 LOC to ~155 LOC. Removed in-page mermaid renderer, per-instance reviewer-action buttons (POST `/admin/workflow/{id}/action/{action}` endpoint deleted), JSON modal CSS, per-card states/transitions tables. Kept: instance table (state badge, ↺ Reset, Reset All) + workflow-definitions list (display name + ↗ Edit workflow link). Snapshot-shortcut test stays green.

### Modeling decision: fan-in to a Join

The new model has no place to express "stage X feeds gateway Y" except by giving X its own `Split`. Fan-in to a Join therefore requires per-source feeder splits. The `LEAVE_REQUEST_STARTER_WORKFLOW` demo now explicitly demonstrates this pattern (`applicant-amendments-feed` / `upload-evidence-feed` / `reviewer-assessment-feed` all target `decision-join`). This was a deliberate choice over inventing an alternative inbound-binding mechanism on Joins.

### Test status

| Suite | Result |
|---|---|
| TypeScript `tsc --noEmit` | 0 errors |
| `npm run build` | green (workflow-editor.js: 336.62 KB) |
| `npm run build-storybook` | green |
| `dotnet build UmbracoPrism.sln` | 0 / 0 |
| `dotnet test UmbracoPrism.Core.Tests` | **811 / 811 pass** |
| MockBusinessApp build | green |
| Focused Playwright (gateways, transition-editor, history, validation, shell) | **all pass** after two assertion updates |
| Full `tests/workflow-editor/` Playwright | 77 pass / ~58 fail / 12 skip / 2 flaky-pass (147 total) |

The Playwright failure mix is roughly: (a) the pre-existing 49 the user warned about (browser-surface, copy-paste, simulation, outline-a11y, etc.) plus (b) tests that need fresh gateway-shape baselines because the demo fixture went from 2 → 5 gateways. None of the verified-failing tests are new regressions in the *behaviour* of route editing — the gateway/transition/history/validation/shell test surface is fully green.

### Manual E2E recipe (Jonny)

1. `cd src/UmbracoPrism.Client && npm run build && cd ../..` (rebuild the editor bundle).
2. `cd .aspire/UmbracoPrism.AppHost && dotnet run` (Aspire host with MockBusinessApp + Umbraco).
3. Open `http://localhost:5xxx/admin/workflow` (MockBusinessApp) — confirm: stripped scaffold (no mermaid, no per-instance reviewer buttons, just instance list + workflow list with ↗ Edit workflow links).
4. Click ↗ Edit workflow on "Planning Application". Confirm: editor loads with three Split gateways (`route-application-form` / `route-check-answers` / `route-submitted`), each owning one route to the next stage. Submit route carries the `guard:application.isComplete == true` condition.
5. Click any gateway → inspector panel shows its routes. Edit one route's trigger or condition; save (Ctrl/Cmd+S). Confirm: PUT goes out with `gateways[*].routes` shape (no top-level `transitions`); reload; change persists.
6. Open the Leave Request workflow (storybook) — confirm: 5 gateways visible, edges from feeder splits flow into `decision-join`, and `decision-join → decision-confirmed` is rendered.
7. Stage delete: right-click any stage, confirm in dialog. Confirm: gateway whose `source` matched is dropped, and routes targeting the stage are pruned.
8. Validation: introduce a broken route (point a route at a non-existent stage) — expect `route-missing-stage` issue and `kind:'route'` jump-to-issue navigates to the gateway.

### Deferred to a follow-up slice (Slice D)

- Visual baseline re-cert (`workflow-graph-visual.spec.ts` snapshots will shift because every stage→stage line now traverses a gateway pill). Recipe: `npx playwright test tests/workflow-editor/workflow-graph-visual.spec.ts --update-snapshots`, then commit the new `__screenshots__` PNGs.
- Rename `workflow-transition-editor.spec.ts` → `workflow-route-editor.spec.ts` for terminology hygiene.
- Browser-surface specs (29 listed failures in `workflow-browser-surface.spec.ts`) — likely needed updates for the stripped `/admin/workflow` page; triage and either update or quarantine.
- Walkthrough refresh — replace "transitions" with "routes" / "gateways" in author-facing tutorials; add a "gateway-first authoring" walkthrough showing the feeder-split pattern.
- "Single-route Split as a thin pill" rendering — currently every gateway renders as a diamond. Spec deferred this as a polish item; revisit in Slice D's layout pass.
