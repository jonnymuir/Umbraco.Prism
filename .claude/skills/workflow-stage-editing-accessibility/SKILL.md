---
name: "workflow-stage-editing-accessibility"
description: "Build stage creation/editing flows that keep structural actions in the workspace and detailed edits in the inspector"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #60 stage creation and editing slice)"
---

## Context

Use this when a workflow editor needs stage creation, deletion, and property editing without collapsing graph/list structure editing into the inspector.

## Patterns

- Keep **create / insert / delete confirmation** in the workspace that owns structure, selection, and ordering.
- Keep **title / key / description / actor / type / action-list editing** in the inspector so graph and list selection share one detailed edit surface.
- Use modal dialogs only for structural create/delete checkpoints; seed focus on open, trap Tab, and restore focus to the invoking control on close.
- Validate duplicate stage keys at both creation time and inspector rename time.
- Surface “no outbound transitions” as an in-context validation message in the inspector instead of blocking stage creation.
- For action lists, combine catalog-backed add flows with keyboard reorder affordances and a drag handle so pointer and keyboard users get parity.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts`

## Anti-Patterns

- Opening full property forms directly inside graph nodes or table rows
- Deleting stages immediately without confirming affected transitions
- Making stage creation drag-only or pointer-only
- Hiding structural validation until publish time
