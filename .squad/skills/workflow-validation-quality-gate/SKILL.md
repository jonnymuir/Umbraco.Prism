---
name: "workflow-validation-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor validation and error-reporting slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #65 quality gate)"
---

## Context

Use this when validating workflow-editor work that claims to surface workflow errors and warnings in plain language. The slice crosses authored-workflow diagnostics, graph/list navigation, inspector field feedback, and the host editor's save behaviour, so a partial UI check or backend-only test gives false confidence.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd /Users/jonnymuir/Documents/Projects/Umbraco.Prism && dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-action-editor.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-validation.spec.ts --reporter=line`
7. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript contract drift between the host editor, graph, inspector, and shared validation helpers.
- **Authoring .NET tests** protect projector diagnostics, validate/project/publish endpoints, and action-schema rules the editor must trust.
- **Storybook CI** keeps the editor surfaces WCAG-clean while validation messaging and focus behaviour change.
- **Graph keyboard coverage** proves the rail can move authors to the affected item without pointer-only affordances.
- **Action editor coverage** proves field-level parameter feedback stays honest for stage and transition actions.
- **Dedicated validation/error-reporting Playwright coverage** is where orphaned-stage detection, unreachable-stage reporting, rail copy, jump-to-item flows, and save blocking should live.
- **Planning smoke** proves the real authoring shell still loads and the slice does not regress the live workflow editor.

## Acceptance Audit Heuristics

- Do not credit “orphaned stage validation” if the editor only reports unreachable stages; an isolated non-initial stage with no inbound or outbound transitions must be called out explicitly.
- Do not credit “validation rail” if warnings only appear inside the graph workspace or inside the inspector; the host editor needs one visible place authors can scan.
- Do not credit “click error jumps to affected item” unless the jump opens the right stage, transition, or action and lands the inspector on the right context.
- Missing action parameters may be warnings, but they still need workflow-friendly language and field-level feedback in the inspector.
- Do not credit save blocking until the main editor affordance is visibly disabled or otherwise prevented when blocking errors exist, and the focused spec proves it.
- Messages should talk about stages, transitions, and actions, not raw JSON paths or schema internals.

## Anti-Patterns

- Calling the slice green because `workflow-validation.ts` exists while nothing consumes it
- Treating graph-only routing warnings as the requested validation rail
- Relying on server diagnostics alone when the acceptance criteria require editor-native guidance
- Counting existing graph/action tests as sufficient without a dedicated validation/error-reporting behavioural contract
