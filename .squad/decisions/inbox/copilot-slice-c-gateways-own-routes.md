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
