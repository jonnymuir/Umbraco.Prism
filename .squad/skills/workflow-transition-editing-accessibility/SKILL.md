---
name: "workflow-transition-editing-accessibility"
description: "Build workflow transition creation and editing so structural routing stays in the workspace and detailed routing stays in the inspector"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #61 transition slice)"
---

## Context

Use this when a workflow editor needs graph/list transition creation, inspector-based transition editing, and accessible routing validation without collapsing everything into inline node forms.

## Patterns

- Keep **transition creation** in the structural workspace: drag-to-connect in graph mode should open a confirmation dialog, and list mode should expose an explicit create button.
- Provide a **keyboard-equivalent create path** from the same workspace surface, such as a focused transition handle in graph mode or a row action in list mode.
- Treat the **inspector as the edit surface** for target changes, label/action changes, simple conditions, and role guards after selection.
- Use a small **condition builder** with mode + value (`always`, `event`, `guard`) so authors avoid raw JSON while the authored model still stores one canonical condition string.
- Keep **routing validation** visible in the workspace for unreachable stages and dead ends, and let authors jump from the warning into the affected stage.
- Reuse the project dialog accessibility pattern for transition creation: labelled dialog, seeded focus, Escape handling, Tab trap, and focus restore.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-transition-editor.spec.ts`

## Anti-Patterns

- Creating a transition silently with a default label and no explicit confirmation
- Making transition creation drag-only
- Splitting target editing between graph chips, list rows, and the inspector with different rules
- Hiding unreachable-stage warnings until publish time
