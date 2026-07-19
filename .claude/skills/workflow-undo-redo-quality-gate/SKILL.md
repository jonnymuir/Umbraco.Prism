---
name: "workflow-undo-redo-quality-gate"
description: "Minimum honest validation for workflow editor undo/redo slices"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #63 quality gate)"
---

## Context

Use this when validating workflow editor work that adds or changes undo/redo behaviour, history stacks, keyboard shortcuts, or toolbar history state.

## Minimum Gate

1. `dotnet test src/UmbracoPrism.Core.Tests --filter "FullyQualifiedName~Workflow.Authoring"`
2. `cd src/UmbracoPrism.Client && npm run build`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-history.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Authoring .NET tests** catch schema or preview/apply contract drift that can silently break history persistence around validation.
- **Client build** catches TypeScript/Lit state-shape mistakes immediately.
- **Storybook CI** keeps the component and accessibility baseline honest while undo/redo wiring moves through editor surfaces.
- **Graph keyboard contract** proves the existing keyboard-first editor behaviour still works after history listeners or global shortcut handlers are added.
- **Dedicated undo/redo spec** is where acceptance really lives: toolbar state, Ctrl/Cmd+Z parity, redo disablement, ordered edit sequences, and preview/validation survival.
- **Planning smoke** proves the real host shell still loads, previews, and applies authored changes with history-enabled editor state.

## Acceptance Audit Heuristics

- Do not credit the slice unless both toolbar buttons and keyboard shortcuts are covered.
- Explicitly test `Ctrl+Z` / `Cmd+Z` and `Ctrl+Shift+Z` / `Cmd+Shift+Z` where platform handling is abstracted.
- Include at least one sequence that edits through more than one surface (for example graph/list selection plus inspector edit) to prove history is editor-wide, not local-only.
- Include a preview or validation action between undo and redo steps; acceptance requires history to survive those read-only flows.
- Assert disabled/enabled state transitions for undo and redo buttons, not just final data shape.
- Treat retry-only success in the dedicated undo/redo Playwright contract as a blocker, not a pass. This slice is about deterministic editor recovery, so flakiness usually means selection or inspector restoration is still racing after a mutation.
- For stage-create history paths, wait for the newly created stage node to become selected (`aria-pressed="true"`) before asserting its inspector detail. That keeps the contract behavioural while giving the host/editor selection handoff one honest render boundary.

## Anti-Patterns

- Calling the issue green because a single state-mutating helper has unit coverage
- Testing only button clicks and skipping keyboard shortcuts
- Treating preview/validation as out of scope when the acceptance criteria explicitly mention them
- Relying on implementation details like internal stack length instead of user-visible affordances
