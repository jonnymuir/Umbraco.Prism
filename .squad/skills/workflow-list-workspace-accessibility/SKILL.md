---
name: "workflow-list-workspace-accessibility"
description: "Build a workflow list/table editor that keeps structural editing fast, shared-model, and screen-reader friendly"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #59 list workspace slice)"
---

## Context

Use this when a workflow editor needs a keyboard-first alternative to a visual graph without splitting into a separate model or sacrificing inline structural editing.

## Patterns

- Keep graph and list on the same `workflow.stages` / `workflow.transitions` model so selection, ordering, and inspector state stay consistent across views.
- Use a semantic `<table>` for the list surface when authors need to compare repeated stage metadata such as key, title, actor, type, action count, and outbound transitions.
- Give each row a dedicated focus trigger for Arrow/Home/End navigation and Enter-to-open-inspector behaviour; let inline fields remain standard inputs/selects in the cells.
- Reserve inline editing for compact, high-frequency structural fields (key, title, actor, type) and keep richer configuration in the inspector.
- Support reordering with both drag handles and keyboard shortcuts (`Alt+ArrowUp` / `Alt+ArrowDown`) so pointer-only gestures are never the sole path.
- Announce filter changes, row moves, and inline edits through a polite live region instead of forcing focus jumps.
- Treat filters as visibility controls only; reordering should still operate on the authored stage array so hidden rows do not create a second ordering contract.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.stories.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts`
- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`

## Anti-Patterns

- Duplicating the workflow data into a separate list-only editing model
- Expanding detailed inspector-sized forms inside every table row
- Shipping drag-only reordering with no keyboard equivalent
- Making filters change the underlying stage order or selection contract
