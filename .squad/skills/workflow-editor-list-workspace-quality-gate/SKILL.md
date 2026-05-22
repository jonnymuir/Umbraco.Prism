---
name: "workflow-editor-list-workspace-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor list/table workspace"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #59 quality gate)"
---

## Context

Use this when validating the accessible list/table editing slice of the workflow editor. A list-mode toggle alone is not enough; the gate must prove shared-model integrity, keyboard behaviour, accessibility, and live-shell wiring.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
3. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
4. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Acceptance Audit Heuristics

- Do not credit “list/table workspace” if the surface is still only a `listbox` of cards with no table-like row/column editing affordance.
- Check that clicking a row opens the inspector; selection-only click behaviour does not satisfy the issue contract.
- Look for explicit support for insert-before, insert-after, delete, and reorder in the list surface itself.
- Search for inline editing controls (`input`, `select`, buttons, editable cells), not just read-only labels.
- Require a front-stage/back-stage filter control if the issue calls for lane filtering.
- Treat Storybook a11y as necessary but insufficient; the focused Playwright contract should exercise list-mode keyboard behaviour and change announcements.
- Credit shared-model parity only when list actions emit or mutate the same workflow object used by graph mode and the host editor.

## Anti-Patterns

- Calling the issue done because a graph toggle reveals a linear fallback
- Accepting double-click or keyboard-only inspector entry when the requirement is row click
- Treating context-menu-only add/delete as equivalent to the requested list-editing workspace affordances
- Claiming reorder support when stage order is never mutated
