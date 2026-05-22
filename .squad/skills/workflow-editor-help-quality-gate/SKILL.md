---
name: "workflow-editor-help-quality-gate"
description: "Minimum honest validation and acceptance audit for workflow editor help and shortcut discoverability"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #66 quality gate)"
---

## Context

Use this when validating workflow-editor work that adds a help surface, shortcut reference, inline field guidance, or getting-started empty-state copy. This slice looks small but actually crosses toolbar wiring, keyboard contracts, action-editor field help, focus management, and discoverability; a screenshot or one shallow click-through is not honest proof.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
3. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-action-editor.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-help.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript drift between shortcut definitions, toolbar affordances, and the help surface.
- A shared shortcut catalog is the preferred seam: add commands there first, then let the toolbar metadata, help modal, and parity tests consume the same source.
- **Storybook CI** keeps the help surface and inline guidance accessible across browsers with axe in the loop.
- **Graph keyboard coverage** protects the editor-wide keyboard model that help must plug into.
- **Action editor coverage** protects inline explanations on complex parameters and forms-backed fields.
- **Dedicated help/shortcut Playwright coverage** is where the acceptance criteria belong: help button opens the reference, the list matches implemented commands and keys, empty state shows getting-started tips, and keyboard users can open, move through, and close help predictably.
- **Planning smoke** proves the real authoring shell still loads after the help surface is added.

## Acceptance Audit Heuristics

- Do not credit “help button opens shortcut reference” unless the author can trigger the surface without leaving the editor.
- Do not credit “shortcut reference lists all commands with keys” unless the list is derived from the same source of truth as the actual handlers or button metadata.
- Do not credit “shortcuts are discoverable” if the only evidence is hidden `aria-keyshortcuts` attributes.
- Do not credit “inline help” if descriptions exist in schema data but are not visible on hover/focus or otherwise reachable from the UI.
- Do not credit “empty state shows getting-started tips” if the editor only says there is nothing to display.
- Keyboard accessibility needs more than focusability: opening, reading, and dismissing help must work from the keyboard, and focus should return predictably.

## Anti-Patterns

- Calling the slice green because Storybook renders a help icon
- Treating field descriptions in data models as shipped inline help
- Counting undo/redo/copy/paste shortcuts as enough when save, view-switch, help, delete, or duplicate are missing from the reference
- Writing a shortcut list by hand without a parity test against the implementation
