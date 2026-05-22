---
name: "workflow-editor-copy-paste-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor stage/action copy-paste slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #64 quality gate)"
---

## Context

Use this when validating workflow-editor work that lets authors copy and paste stages or actions, reuse configuration across stages, and rely on keyboard shortcuts or toolbar clipboard state. The slice spans graph/list selection, inspector-driven action editing, authoring validation, and editor-level shortcut handling, so no single layer is enough to call it green honestly.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd /Users/jonnymuir/Documents/Projects/Umbraco.Prism && dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-action-editor.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-copy-paste.spec.ts --reporter=line`
7. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript state drift in shared workflow, clipboard state, and toolbar wiring.
- **Authoring .NET tests** protect validation/projection behaviour the pasted workflow must still satisfy.
- **Storybook CI** keeps the edited editor surfaces WCAG-clean while copy/paste affordances are added.
- **Graph keyboard coverage** protects existing workspace navigation and selection behaviour after shortcut listeners are introduced.
- **Action editor coverage** protects the shared action surface that action paste must target in both stage and transition contexts.
- **Dedicated copy/paste coverage** is where acceptance actually lives: new stage keys, copied-property fidelity, transition exclusion, validation-after-paste, toolbar clipboard indication, same-stage and cross-stage action paste, shortcut parity, and immediate selection for editing.
- **Planning smoke** proves the real shell still loads and hosts the editor correctly after editor-level clipboard changes.

## Acceptance Audit Heuristics

- Do not credit stage copy if the pasted stage reuses the original `stageKey`.
- Do not credit stage paste if inbound or outbound transitions come along with the copied stage.
- Require a visible clipboard state in the toolbar; a transient toast alone is not enough for “copied item indicated in toolbar”.
- Require the newly pasted stage or action to become the active edit target immediately.
- Validation warnings must surface in the editor after paste when the copied content is incomplete in its new context.
- Cover action paste in the source stage and at least one different target stage.
- Explicitly test Ctrl/Cmd+C and Ctrl/Cmd+V where platform handling is abstracted.

## Anti-Patterns

- Treating existing action add/remove tests as equivalent to action paste coverage
- Crediting generic browser clipboard writes without an editor paste path
- Counting JSON copy in a context menu as workflow-authoring copy/paste
- Calling the slice green without a dedicated behavioural contract for transition exclusion
