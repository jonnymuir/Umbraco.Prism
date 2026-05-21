---
name: "workflow-graph-two-lane-accessibility"
description: "Build a two-lane workflow graph that keeps front/back-stage orientation and keyboard parity together"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #58 graph workspace slice)"
---

## Context

Use this when a workflow-style canvas needs to show visually distinct authoring lanes while still supporting Storybook axe checks, keyboard focus, and a non-pointer fallback path.

## Patterns

- Render front-stage and back-stage as explicit lanes with stable headings so the visual distinction is structural, not just colour.
- Let the graph component own structural canvas actions (selection, zoom, fit, add/delete, drag-to-connect) and emit the updated workflow model upward.
- Keep the inspector as the detailed edit surface; graph double-click or keyboard inspect actions should transfer focus there instead of opening inline forms on the node.
- Accept an optional `editorSurface` hint on stages, but infer the lane from role gates and actor labels until the authoring contract persists lane metadata explicitly.
- Avoid nested interactive controls inside stage nodes; use a stage hit target and separate drag handle/button siblings so axe stays green.
- Every scrollable graph viewport needs `tabindex="0"` to satisfy `scrollable-region-focusable` in Storybook.
- Linear/list mode should preserve orientation and keyboard navigation even if transition editing stays graph-first.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.stories.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts`

## Anti-Patterns

- Making the graph node itself a composite interactive wrapper with nested buttons
- Treating front/back-stage as colour-only styling with no lane structure or labels
- Letting graph mutations bypass the host workflow state
- Shipping drag-only affordances without context-menu or keyboard alternatives
