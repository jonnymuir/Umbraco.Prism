---
name: "workflow-action-editor-quality-gate"
description: "Minimum honest validation and acceptance audit for the workflow editor action/forms configuration slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #62 quality gate)"
---

## Context

Use this when validating workflow-editor work that lets authors pick actions from the catalog, configure generic parameters, and build forms-backed action payloads. The slice crosses authoring-catalog metadata, schema-driven UI, stage and transition inspector contexts, accessibility, and live-shell wiring, so a single test layer can only give false confidence.

## Minimum Gate

1. `cd src/UmbracoPrism.Client && npm run build`
2. `cd /Users/jonnymuir/Documents/Projects/Umbraco.Prism && dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all`
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts --reporter=line`
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-action-editor.spec.ts --reporter=line`
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke`

## Why this combination works

- **Build** catches TypeScript model drift between authored actions, catalog metadata, and editor components.
- **Authoring .NET tests** protect action-catalog discovery, parameter-schema validation, and backend defaults the UI depends on.
- **Storybook CI** proves the inspector and editor stories still render and remain WCAG-clean.
- **Graph/list keyboard coverage** keeps selection, inspector entry, and baseline keyboard affordances honest.
- **Dedicated action-editor Playwright coverage** is where context filtering, schema-driven widgets, forms-backed field editing, validation blockers, delete confirmation, and keyboard-only authoring should live.
- **Planning smoke** proves the real shell still loads the authoring API and does not regress while the slice deepens.

## Acceptance Audit Heuristics

- Do not credit “action picker filters by context” if the UI only offers stage actions or ignores `transition` applicability.
- Do not credit “generic parameter editor” if authors can only change timing or other hard-coded controls instead of schema-derived widgets.
- Do not credit “forms-backed actions” unless the field list supports add, remove, and reorder and the type picker includes text, number, textarea, select, radio, and date.
- Validation must prevent invalid save/confirm in the editor surface; server-only errors after submit are not enough.
- Action summaries should reflect authored values in plain language, not just echo static catalog summary text.
- Require coverage across at least five action types with materially different schemas, including one transition-context path.
- Keep a separate keyboard-first path that proves the shared action editor can add an action, edit required fields, reorder a forms-backed field with accessible controls, and cancel/confirm delete with focus handled explicitly.

## Anti-Patterns

- Calling the slice green because the backend catalog exists while the UI still cannot configure parameters
- Treating stage action add/remove/reorder as equivalent to action configuration
- Counting generic Storybook rendering as the dedicated behavioural contract
- Accepting immediate delete as equivalent to explicit confirmation
