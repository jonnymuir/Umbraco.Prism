---
date: 2026-05-25T14:17:36.055+01:00
author: Tangy
context: Issue #82 baseline validation for named lanes editor slice
status: proposed
---

# Gateway Representation Behavioural Guardrails

## Decision

Before adding gateway visual representation to the workflow editor for the multi-lane engine, the following behavioural contracts must remain green:

1. **Straight-line workflow execution** — The planning workflow fixture must continue to project correctly and execute through its linear path without regression.

2. **Stage-to-state projection fidelity** — The `PublishAsync_PlanningFixture_ProjectsStagesTransitionsAndActions` backend test proves that authored stages map to published runtime states with correct assignment and action data. Gateway representation work must not break this projection contract.

3. **Assignment-driven lane derivation** — Lane meaning must continue to derive from `actor` and `roleGates` data, not from separate UI-only surface hints. The `workflow-assignment-source-of-truth` skill applies.

4. **Graph path highlighting for single-cursor flows** — The current graph workspace highlights the active path during simulation. When gateways become visual nodes, this highlighting contract must extend to include gateway nodes in the path.

5. **Validation rail contract** — The validation rail must continue to surface unreachable stages, orphaned stages, and missing action parameters. When gateways are added, validation must also detect unreachable gateways, orphaned gateways, and unsatisfiable join conditions.

## Current Test Status (2026-05-25T14:17:36.055+01:00)

### ✅ Green
- Build: TypeScript compilation clean
- Backend workflow authoring tests: 106 passed
- Graph keyboard navigation: 5 passed
- Action editor: 2 passed (1 flaky timeout - pre-existing)
- Validation rail: 1 passed
- Planning smoke (localhost auth): 1 passed

### ❌ Red (Pre-existing, not blocking #82)
- Simulation tests: 2 failed (tests don't switch to Simulation tab before clicking start button)

## Rationale

The multi-lane engine design introduces split and join gateways as first-class workflow elements. These must be represented in the editor graph workspace without breaking the existing single-path workflow contracts that protect planning application and community enquiry flows.

The above five contracts guard the most fragile cross-layer dependencies:
- Backend projection (workflow authoring → runtime state)
- Editor rendering (authored stages → graph nodes)
- Validation diagnostics (authored structure → error messages)
- Simulation path highlighting (runtime execution → visual feedback)

If any of these contracts break during gateway representation work, the editor will lose trust for existing straight-line workflows even though the runtime continues to support them.

## Acceptance Criteria for Gateway Work

When split/join gateways are added to the editor:

1. All green tests listed above remain green
2. New gateway nodes appear in the graph workspace with semantic selectors (`data-prism-gateway`, `role=button` or similar)
3. Keyboard navigation includes gateway nodes in the tab order
4. Validation rail reports gateway-specific issues (unreachable, orphaned, unsatisfiable joins)
5. Simulation path highlighting includes gateway nodes
6. Backend projection tests extend to cover gateway → runtime-token projection

## Related

- `.squad/skills/workflow-validation-quality-gate/SKILL.md`
- `.squad/skills/workflow-assignment-source-of-truth/SKILL.md`
- `docs/design/workflow-multi-lane-engine.md`
