# Slice 4 — visual lock + public surface declaration

**Author:** Isabelle (Frontend / a11y)
**Branch:** `squad/82-named-lanes-editor-slice`
**Status:** ready for Scribe

## What changed

### Public surface — ONLY three elements

The workflow editor bundle now declares its public API. Hosts (TestSite Razor pages, reference shell, Storybook, future Razor recipes) may consume these and only these:

1. `<prism-workflow-editor>` — full authoring surface.
2. `<prism-workflow-editor-shell>` — host harness (workflow picker, API base wiring, URL sync).
3. `<prism-workflow-graph>` — vertical-lanes graph. **New:** accepts `read-only` + `workflow-json` for declarative read-only viewer embeds with no JS wiring.

Every other custom element under `src/UmbracoPrism.Client/src/workflow-editor/` is now tagged `@internal` in JSDoc (`prism-step-inspector`, `prism-confidence-tabs`, `prism-help-panel`, `prism-stage-preview`, `prism-workflow-simulation`, `prism-workflow-outline`, `prism-workflow-action-editor`, `prism-inline-help`). Future slices may move, merge, or rename them without notice — consumers must not import them.

API reference: `src/UmbracoPrism.Client/src/workflow-editor/README.md` (new).

### Constraints reaffirmed (no change of direction)

- **No backoffice editor.** Ever. TestSite is runtime-only; `App_Plugins/PrismWorkflowEditor/` has been deleted from `UmbracoPrism.TestSite`. Brewster's "mount editor as v17 web component" recommendation remains permanently rejected.
- **Vertical lanes.** Non-negotiable. No orientation switcher exists in the code; the `vertical-lanes-switcher.spec.ts` (misleadingly named — there was no switcher to test) has been deleted.
- **No linear mode.** ~600 lines of `GraphMode`, `LinearFilter`, drag-reorder, inline editors, `_renderLinear`, `_renderValidationSummary`, and the entire `allow-linear-mode` attribute pathway have been removed from `prism-workflow-graph.ts`. Bundle dropped from 337KB to 311KB.

## Breaking changes

| Area | Change | Migration |
|------|--------|-----------|
| `<prism-workflow-graph>` | `mode` and `allow-linear-mode` attributes removed. | Hosts must not set them. The graph is vertical-lanes always. |
| `<prism-workflow-editor>` | `WorkflowSelection` union narrowed to `{kind:'stage'\|'gateway'} \| null` (was also `'transition'`). | Transitions are auxiliary highlight state via `_selectedTransitionIndex`, not first-class selection. Consumers that listened to `selection-change` already get a transition-free union. |
| `UmbracoPrism.TestSite` | `App_Plugins/PrismWorkflowEditor/` (umbraco-package.json, web-components host, README) deleted. | TestSite remains runtime-only — runs published workflows via the standard `UmbracoPrism.WorkflowEditor` recipe, no backoffice dashboard. |
| Internal elements | Eight previously-undocumented elements now bear `@internal` JSDoc. | If a host imported them directly, raise a Squad decision to promote a stable element. |
| Test suite | `tests/workflow-editor/vertical-lanes-switcher.spec.ts` deleted (asserted behaviour that never existed in the code). | None. |

## New affordances

- `<prism-workflow-graph read-only workflow-json='...'>` renders a published workflow as a navigable, zoomable, screen-reader-friendly graph with **zero authoring affordances**: no Add stage / Add gateway HUD buttons, no dialogs, no context menus, no `workflow-updated` event, `aria-roledescription` = "viewer". `data-prism-read-only` attribute on the host plus `[read-only]` selector available for CSS overrides.
- `GraphReadOnly` Storybook story under `prism-workflow-graph.stories.ts` demonstrates the declarative HTML embed.

## Explicitly deferred

The following items were considered and intentionally not done in this slice:

- **TestSite Razor recipe for embedding `<prism-workflow-graph read-only>`** (Brewster's runtime-embed recommendation, scoped down). The element is ready; the recipe / docs example belongs in the next docs-walkthrough slice.
- **JSON twin-pane editor view** (Slice 6). Out of scope.
- **Visual regression baselines** for the read-only viewer (Slice 7).
- **Canvas slot-matrix refactor** (Slice 5).
- **Composition guide overhaul** beyond the new header link (Slice 8 / docs walkthrough).
- **Removing the `[data-prism-canvas-health-hint]` validation spec assertion** (`tests/workflow-editor/workflow-editor-validation.spec.ts:8`). The assertion is a pre-existing failure on baseline `e113bbb` (verified identical with retry pattern); it was not introduced by Slice 4 and fixing it requires deciding whether to re-introduce a discoverable "open Validation" affordance — out of scope for visual lock.

## Validation

- `npm run build` ✅ (workflow-editor.js: 312.65 kB)
- `npm run build-storybook` ✅
- `dotnet build UmbracoPrism.sln` ✅ (0 W / 0 E)
- Targeted Playwright suite: green except for the one pre-existing baseline failure noted above.
