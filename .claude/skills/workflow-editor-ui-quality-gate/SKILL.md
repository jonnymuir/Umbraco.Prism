---
name: "workflow-editor-ui-quality-gate"
description: "Minimum honest validation for Workflow Editor UI slices"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #58 quality gate)"
---

## Context

Use this when validating a Workflow Editor frontend slice such as graph, list, inspector, or confidence tooling. Storybook alone is too narrow, but full end-to-end only is too slow to isolate regressions.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
3. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
4. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript/Lit contract drift immediately.
- **Storybook CI** proves the component stories still render and pass their interaction/a11y checks.
- **Dedicated keyboard spec** protects the accessibility contract that can be too specific for story play functions.
- **Planning smoke** proves the real editor shell, authoring API wiring, and workflow fixture still work together.

## Visual Regression Add-On

When the acceptance criteria explicitly call for Storybook visual regression on an editor surface, add:

1. `cd src/UmbracoPrism.Client && npm run test:playwright:workflow-graph-visual`
2. Commit the generated baselines from `src/UmbracoPrism.Client/tests/__screenshots__/`
3. Wire the visual script into `.github/workflows/ci-tests.yml` so the baseline is enforced on PRs

This keeps Storybook interaction/a11y coverage and screenshot coverage separate: Storybook test-runner stays focused on behaviour + WCAG, while the Playwright visual spec owns deterministic screenshot assertions.

## Acceptance Audit Heuristics

- Search for explicit handlers before crediting an interaction requirement:
  - `dblclick` / double-click
  - `contextmenu`
  - drag/pointer handlers for graph gestures
  - zoom/fit-to-screen controls
- Search for transition rendering, not just transition data being mentioned in labels or inspector summaries.
- Treat front-stage/back-stage styling as incomplete if the data model cannot express placement, even if dormant CSS rules exist.
- Treat Storybook as incomplete for “visual regression” acceptance unless snapshot/screenshot assertions are present.

## Anti-Patterns

- Calling the slice green because Storybook passes while live shell smoke is red
- Calling transition rendering done when transitions only appear in text summaries
- Crediting accessibility from a list fallback if the graph-specific keyboard contract is untested
