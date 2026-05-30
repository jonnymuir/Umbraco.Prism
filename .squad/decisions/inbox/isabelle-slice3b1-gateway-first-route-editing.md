---
author: isabelle
date: 2026-05-30T15:30:00+01:00
status: proposed
area: workflow-editor-inspector, workflow-editor-client-wire
---

# Decision: Slice 3b.1 — Gateway-first route editing + closed TS stage-kind

## Context

Follows Slice 3b. Per the named-lanes editor brief and Jonny's
scope-reset directive, transition editing is **only** allowed via the
source gateway's outgoing-route panel; transition creation is removed
from the canvas entirely (no drag-handle, no context-menu item, no `'t'`
shortcut, no list-view row-action). In parallel, the TS `StageKind`
enum is closed to four canonical values and the outbound transition
wire payload is renamed to the canonical `source`/`target`/`trigger`
shape that mirrors xstate/BPMN vocabulary.

## Decision

### Package A — Gateway-first route editing

- **`prism-step-inspector.ts`**: deleted the standalone `transition`
  selection branch (`_renderTransition`, `_availableSplitGatewaysForStage`,
  eight `_updateTransition*` handlers, `_deleteSelectedTransition`,
  `_updateSelectedTransitionActions`, the `transition` `render()`
  branch, and the `selectedTransitionIndex` property). Added a new
  outgoing-routes panel rendered inside `_renderGateway` via
  `_renderGatewayOutgoingRoutes(gateway, binding)` →
  `_renderRouteEditor(transition, transitionIndex)`. Each route row
  carries `data-prism-route-index="${idx}"` on every input, so a single
  set of `_updateRoute*` handlers reads the index from
  `event.currentTarget`. New attribute conventions
  (`data-prism-gateway-route`, `data-prism-route-target`,
  `data-prism-route-label`, `data-prism-route-action`,
  `data-prism-route-target-select`, `data-prism-route-to-gateway`,
  `data-prism-route-role`, `data-prism-route-condition-mode`,
  `data-prism-route-condition-value`, `data-prism-route-delete`,
  `data-prism-route-descriptor`) replace the now-deleted
  `data-prism-transition-*` family.

- **`prism-workflow-graph.ts`**: deleted `CreateTransitionDialogState`,
  `_dragTransition`, `_createTransitionDialog`,
  `_openCreateTransitionDialog`, `_openCreateTransitionFromStage`,
  `_closeCreateTransitionDialog`, `_submitCreateTransition`,
  `_handleWindowPointerMove/Up`, `_startTransitionDrag`,
  `_stageKeyAtClientPoint`, `_scenePointFromClient`,
  `_renderCreateTransitionDialog`, the `transition-handle` button +
  drag-target class, the `add-transition` context-menu item, the
  keyboard `'t'` shortcut, the list-view "Add transition" row-action,
  and the connected/disconnected pointer listeners. The list-view kind
  `<select>` and create-stage dialog `<option>`s are trimmed to the
  closed StageKind set (Question / CheckAnswers / Confirmation /
  TaskList).

- **`prism-workflow-editor.ts`**: dropped the inspector's
  `selectedTransitionIndex` prop; added `selectedActionTransitionIndex`
  plumbing so the route-scope action editor can disambiguate which
  route owns the currently-selected action.

- **`gateway-route-conditions.ts`**: extracted the route-condition
  helpers (`parseTransitionCondition`, `serialiseTransitionCondition`,
  `transitionQuickAction`, `TRANSITION_ACTION_OPTIONS`) into a focused
  module shared by the new route editor.

### Package B — Closed TS `StageKind`, JSON-boundary normaliser, wire rename

- **`types.ts`**: `StageKind` is now exactly
  `'Question' | 'CheckAnswers' | 'Confirmation' | 'TaskList'`.
  `EditorStageType` mirrors the closure. `AuthoredStage` gains a
  non-persisted `legacyKindRewrittenFrom?: 'Waiting' | 'StatusTimeline'`
  marker used purely to drive an editor diagnostic.

- **`workflow-authoring-client.ts`**: `mapStageKind` returns
  `{kind, legacyKindRewrittenFrom?}` and rewrites `Waiting`/
  `StatusTimeline` to `Question`. `stripLegacyStageSurface` strips the
  marker **and** the `waiting` payload when rewritten, so the C#
  `AuthoredWorkflowSchemaValidator` (PROJ140) accepts the save.
  Outbound transitions are now serialised by `serialiseTransition`
  which emits `source`/`target`/`trigger` and drops `fromGateway`/
  `toGateway`. Inbound `normaliseTransition` prefers the canonical
  field names but falls back to the legacy `fromStage`/`toStage`/
  `action` shape so older fixtures and the projection endpoint
  continue to round-trip.

- **`workflow-validation.ts`**: new `stage-legacy-kind-rewritten`
  warning code surfaces in the inspector validation rail whenever the
  normaliser had to rewrite a Waiting/StatusTimeline stage. Terminal
  kinds set is now `['Confirmation']`.

- **`workflow-runtime-projection.ts`** and `prism-stage-preview.ts`
  `shellLabelFor` lose the `Waiting` / `StatusTimeline` switch arms.

## ⚠️ Breaking change — outbound transition wire field rename

Outbound transition JSON in the publish payload now uses the canonical
names:

| Before (legacy) | After (canonical) |
|-----------------|-------------------|
| `fromStage`     | `source`          |
| `toStage`       | `target`          |
| `action`        | `trigger`         |

The C# `AuthoredTransition` record carries `[Obsolete]` setter shims
that still accept the legacy names on **inbound** requests (Slice 3a),
so any consumer that **only POSTs** to the publish endpoint with the
legacy names will continue to work. Two consumer classes are at risk
and should be audited:

1. **Anyone parsing the publish *response* body** (or any other
   endpoint that echoes back the authored shape) — they will see the
   new field names.
2. **Anyone replaying captured POST bodies** through a typed SDK — if
   the SDK pins the legacy names, it will fail to deserialise the new
   payload after a round-trip through this client.

Suggested follow-up: emit a one-time changelog/migration note in the
SDK README, and ensure the Slice 7 visual-regression baseline captures
a publish payload that documents the new shape.

## Deferred (not blocking commit)

- **`WorkflowSelection` union collapse** in `prism-workflow-editor.ts`
  — the editor still uses three parallel `@state` fields
  (`_selectedStageKey`, `_selectedGatewayKey`,
  `_selectedTransitionIndex`). Build and targeted Playwright are green
  without the collapse since the inspector no longer consumes the
  transition selection field; only the graph (edge highlight) and
  outline (transition row highlight) still read it. Filed as a Slice
  3b.2 polish item.
- Canvas slot-matrix (Slice 5), read-only graph (Slice 4), JSON
  twin-pane (Slice 6), visual-regression baseline (Slice 7), and a11y
  polish #1–4 (Slice 3d) remain in their original slices.

## Validation

- `npx tsc --noEmit` ✅ 0 errors.
- `npm run build` ✅ workflow-editor.js ~336 kB.
- `npm run build-storybook` ✅.
- `npx playwright test tests/workflow-editor/` — the targeted
  inspector/gateway/route specs (gateway-route conditions, retired
  stage types, four gateway specs, transition-editor Tangy #5,
  history undo/redo) all pass. The 6 still-red specs in the
  editor-only suite (copy-paste, help, simulation ×3, validation rail)
  were verified failing on baseline `HEAD` without my changes — they
  are pre-existing and out of scope for this slice. The
  layout-professionalization / walkthrough / four-workflow-contract
  failures require the Aspire/dotnet/Keycloak stack and remain
  pre-existing.
- `workflow-editor-history.spec.ts:61` was rewritten to exercise route
  label edits + route deletion undo/redo on the new
  `GatewayRepresentation` story, since transition creation is no
  longer a canvas affordance.

## New / changed tests

- New: `tests/workflow-editor/workflow-stage-type-options.spec.ts`
  (Tangy SHOULD-FIX #5) — asserts `Waiting`/`StatusTimeline` are not
  offered as stage kinds in either the list-view or create-stage
  dialog.
- New: `tests/workflow-editor/workflow-transition-editor.spec.ts` is
  Tangy #5 verbatim — drives route label, target, role, and condition
  edits on the gateway-route panel and confirms a single atomic undo
  per edit.
- New story: `workflow-editor-editor-host--gateway-representation`
  (inline `makeGatewayWorkflow()` fixture) provides the gateway-shaped
  workflow used by the new specs.

## Files of note

- `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts`
  — normaliser, wire-rename serialisation, legacy-kind marker.
- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
  — gateway-route panel + `_updateRoute*` handlers + selector
  conventions.
- `src/UmbracoPrism.Client/src/workflow-editor/gateway-route-conditions.ts`
  — new module split.
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
  — transition-creation surface deleted.
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-validation.ts`
  — `stage-legacy-kind-rewritten` diagnostic.
