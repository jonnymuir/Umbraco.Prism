# Decision: Inspector "+ Add route" Affordance

**Date:** 2026-05-31  
**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Status:** Decided

---

## Context

After Slice D shipped, the inspector's "Outgoing routes" section in `prism-step-inspector.ts` only *edited* existing routes. The `addRoute`, `buildRoute`, `newRouteId`, and `findOrCreateSplitGateway` helpers existed in `workflow-routes.ts` but no UI handler called them. Authors had to hand-edit the JSON Definition tab to create routes — a blocker for multi-route authoring.

The empty-state message also misleadingly said "Add transitions in the workflow graph", but the graph had no add affordance.

---

## Decision

### 1. Inspector "+ Add route" button

A `<button data-prism-add-route>` is placed in the `section-header-row` of:
- The gateway inspector's "Outgoing routes" section (`_renderGatewayOutgoingRoutes` — Split gateways only, not Join)
- The stage inspector's "Outgoing routes" section

Clicking calls `_handleAddRoute()` which:
1. Resolves the source stage key from either `_selectedStage.stageKey` or the selected gateway's `source` field
2. Calls `findOrCreateSplitGateway(workflow, sourceStageKey)` — creates the gateway if none exists
3. Appends a blank `AuthoredRoute` (id = `newRouteId(source,'','') + '-' + Date.now().toString(36)`)
4. Emits `workflow-updated` with `selection: { kind: 'gateway', gatewayKey }` so the inspector switches to gateway view

### 2. Focus-and-announce pattern

After creation:
- `_newlyAddedRouteId` (plain private field — not `@state()`) is set before emitting
- `updated()` lifecycle hook detects it, clears it, schedules `requestAnimationFrame`
- RAF finds `[data-prism-route-id="${routeId}"] [data-prism-route-target-select]`, scrolls it into view, and focuses it
- The existing `inspector-announcer` aria-live region announces "Route added — choose a destination."

`data-prism-route-id` is added to the `<li>` elements in the route list so the RAF can locate the new route.

### 3. Inline target validation

When a route's `target` is empty:
- The Target `<select>` carries `aria-invalid="true"` and `aria-describedby` pointing at a visible warning
- A `<span data-prism-route-target-warning>Choose a destination</span>` with class `field-error` appears below the select
- Both clear once the user picks a stage
- Saving is not blocked — the server-side validator catches empty targets too

### 4. Empty-state copy

"Add transitions in the workflow graph and they will appear here." → "No routes yet. Use **+ Add route** above to send this stage to its next destination."

---

## What was deferred

**Graph context-menu "+ Add route" entry** — explicitly out of scope (Slice E). The inspector affordance is the primary authoring path. Graph-side creation is a separate, lower-priority slice. No change to `prism-workflow-graph.ts`.

---

## Accessibility notes (WCAG 2.2 AA)

- Button has an `aria-label` including the source stage name ("Add route from {stageName}") for screen reader context
- Focus lands on the Target picker via RAF after Lit re-renders — ensures the focus target is in the DOM
- Live region reuses the existing `inspector-announcer` element; no duplicate live regions added
- `aria-invalid` + `aria-describedby` pattern for the inline warning follows the existing `field-error` / `field-control-error` convention

---

## References

- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-routes.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/add-route-affordance.spec.ts`
