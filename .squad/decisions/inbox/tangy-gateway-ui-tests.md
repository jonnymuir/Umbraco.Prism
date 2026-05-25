---
date: 2026-05-25T14:17:36.055+01:00
author: tangy
scope: issue-83
status: active
---

# Gateway Representation Behavioral Tests (Issue #83)

## Context

Issue #83 requires editor-only gateway representation while keeping current stage-to-stage execution intact. This is slice 3 of the multi-lane redesign — gateways become visible in the editor before runtime execution changes.

## Decision

Created `workflow-editor-gateways.spec.ts` with 7 behavioral contracts:

1. **Split gateways** are visually distinct from stages
2. **Join gateways** are visually distinct from stages
3. **Gateways show lane ownership** clearly via `data-prism-lane` attribute
4. **Inspector integration** — selecting a gateway opens gateway-specific inspector content
5. **Transition direction** — split fan-out and join merge are visible in the graph
6. **No-gateway workflows** continue to render correctly (backward compatibility)
7. **List mode** includes gateways alongside stages

## Test Strategy

- Tests written to **pass with zero gateways** (current baseline)
- Tests will **prove gateway UI** when Isabelle implements the rendering
- Tests **avoid execution semantics** (no assertions on runtime join/split behavior)
- Tests **stay on visible affordances** (data attributes, inspector content, lane labels)
- All existing tests remain green (graph keyboard, action editor, validation rail, stage preview)

## Quality Gate

- ✅ Build: Green
- ✅ Backend workflow authoring: 106 passed
- ✅ New gateway tests: 7 passed (zero gateway baseline)
- ✅ Graph visual/keyboard: Green
- ✅ Action editor: Green
- ✅ Validation rail: Green
- ✅ Stage preview: Green
- ⚠️ Simulation tests: Pre-existing failures (don't switch to Simulation tab)
- ⏸️ Planning smoke: Requires Aspire (not needed for this slice)

## Guardrails for Isabelle

When implementing #83, preserve these contracts:

1. Straight-line workflow execution in planning fixture
2. Stage-to-state projection fidelity
3. Assignment-driven lane derivation
4. Graph path highlighting for single-cursor flows
5. Validation rail contract for unreachable stages

## Files

- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-gateways.spec.ts` (new)
- `src/UmbracoPrism.Client/src/workflow-editor/types.ts` (already has `AuthoredGateway`, `GatewayKind`)
- Design doc: `docs/design/workflow-multi-lane-engine.md` (section: "Safest next behavioural slice after #82")
