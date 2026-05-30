---
author: isabelle
date: 2026-05-30T12:35:00+01:00
status: proposed
area: workflow-editor-inspector
commit: b03ee38
---

# Decision: Slice 3b — Gateway-first inspector and outline authoring

## Context

Per Jonny's 2026-05-30T11:05 scope-reset directive (answer #1): triggers
and conditions are authored on the **source gateway's outgoing-route
affordance**, not on the target stage and not via a separate transition
inspector tab. `StageKind.Waiting` is deleted outright; join gateways own
waiting copy. Same-lane fan-out has no cap (answer #3).

Slice 3a (Blathers) locks the server model. Slice 3b (this slice) brings
the client inspector + outline into alignment so authors can see and edit
routes through the gateway lens.

## Decision

### `prism-step-inspector.ts`
- Drop `Waiting` from `STAGE_TYPE_OPTIONS`. Stage kinds now: form, review,
  decision, confirmation, system-work.
- Add `_routeDescriptor(transition)` — composes the rail
  `fromStage › splitGateway › joinGateway › toStage` (nulls skipped) as a
  single readable line, rendered as a `gateway-routing-hint` summary and
  used in live-region announcements.
- Add `_availableSplitGatewaysForStage(stageKey)` /
  `_availableJoinGatewaysForStage(stageKey)` — derived from
  `deriveGatewayBindings(workflow)` so the choices are exactly the
  gateways already bound to that stage's outgoing/incoming routes.
- Add explicit `fromGateway` / `toGateway` `<select>` controls in
  `_renderTransition`, plus `_updateTransitionFromGateway` /
  `_updateTransitionToGateway` handlers that mutate the transition and
  announce the change.

### `prism-workflow-outline.ts`
- Group rows by lane via `_laneGroups()` (lane key from `stageLaneKey` or
  `stage.actor` fallback). Each lane is a `<section>` with heading.
- Nest split-gateway rows under their anchor stage via
  `_splitGatewaysForStage(stageKey)`.
- Emit a dedicated `outline-gateway-selected` CustomEvent — gateways are
  first-class selectable nodes in the outline alongside stages.

### `workflow-gateway-representation.ts`
- `deriveGatewayBindings` now builds `explicitSplitBindings` /
  `explicitJoinBindings` from any transition that carries `fromGateway` or
  `toGateway`, and prefers those over heuristic anchor inference.
- Authors who set the route's gateway explicitly get a stable binding that
  does not drift when topology around it changes.

## Caveat — partial fit on directive answer #1

The "standalone transition inspector **tab**" is gone — selection is
driven by the outline/canvas, not a tab strip. However triggers and
condition mode (always / event / guard) are still edited inside
`_renderTransition` (the per-transition inspector panel), not inside the
gateway inspector. The directive's stricter reading is that selecting a
Split gateway should reveal its outgoing routes as a list, each editable
inline.

**Recommended follow-up (Slice 3b.1):** relocate the
condition-mode/condition-value/action controls into the gateway inspector
as a list of outgoing-route rows, so the authoring entry point is
"selected gateway → its outgoing routes", consistent with answer #1.

## Accessibility notes

- New `<select>` controls reuse the `.field-control` /
  `<label class="field-block">` pattern with `prism-inline-help` tooltips:
  keyboard reach and labelling are native.
- Both selectors trigger `_announce(...)` live-region messages naming the
  gateway, so screen-reader users get audible confirmation of the route
  rebind.
- Outline gateway rows currently rely on visible text; a follow-up should
  add explicit `aria-label`s naming the gateway kind (Split / Join) and
  its anchor stage to disambiguate when multiple gateways share an
  anchor.

## Validation

- `npm run build` (client): clean.
- `npm run build-storybook`: clean.
- Playwright `workflow-editor-history.spec.ts` +
  `workflow-editor-stage-preview.spec.ts`: 5/5 green.
- Files modified: exactly the 3 from the stash; no bleed.
- Commit: **b03ee38**.

## Coordination

- Pairs with Blathers' Slice 3a (server model lock). At commit time Slice
  3a was still unstaged WIP in the working tree — my commit did not stage
  any of his files.
- Follow-up 3b.1 (gateway-inspector route list) is the right place to
  fully satisfy directive answer #1.
