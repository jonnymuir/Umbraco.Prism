---
name: "workflow-action-forms-editor"
description: "Implement typed workflow action editing and forms-backed field editing inside the inspector without moving detailed configuration into the workspace"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #62 action/forms slice)"
---

## Context

Use this when the workflow editor needs catalog-driven action configuration, parameter editing, and forms-backed action field editing for both stages and transitions.

## Patterns

- Keep action summaries in graph/list workspace surfaces, but keep detailed action configuration inside the inspector.
- Reuse one shared action editor component for stage and transition actions so picker filtering, validation, keyboard reordering, and delete confirmation stay consistent.
- Render generic parameter inputs from catalog schema metadata (`editor`, `valueKind`, `format`, `allowedValues`, `defaultValue`) instead of hard-coding per-action forms.
- Treat forms-backed actions as a specialised schema case: a `fields` array whose item schema drives add/remove/reorder plus field key, label, type, required, help text, validation, default value, and option editing.
- Merge fetched action-catalog entries with local fallback entries when the frontend slice needs richer editor metadata than the current backend exposes, but keep the authored action payload stable (`type`, `timing`, `params`, `summary`).
- Use the established modal accessibility contract for action picker and delete confirmation: labelled dialog, seeded focus, Escape close, Tab trap, and focus restoration.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-action-editor.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-action-editing.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-action-editor.spec.ts`

## Anti-Patterns

- Splitting stage-action editing and transition-action editing into separate bespoke UIs
- Hiding parameter validation until a later save/publish step with no in-context feedback
- Making forms-backed field configuration pointer-only or drag-only
- Encoding action editor behaviour directly in the workspace graph nodes or list rows
