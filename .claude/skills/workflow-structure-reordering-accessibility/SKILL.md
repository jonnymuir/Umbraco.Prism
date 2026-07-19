---
name: "workflow-structure-reordering-accessibility"
description: "Choose accessible reorder patterns for workflow structures without faking freeform canvas placement"
domain: "workflow-editor"
confidence: "high"
source: "observed (2026-05-26T19:58:39.416+01:00 movement UX assessment)"
---

## Context

Use this when a workflow editor shows a visual canvas but the real authoring action is structural reordering rather than arbitrary x/y placement.

## Patterns

- Treat movement as **sequence editing**, not as freeform node placement, when the canvas layout is derived from slots, lanes, or graph ranks.
- Make the **list/table workspace** the canonical reorder surface for the first slice.
- Provide three aligned paths:
  1. visible **Move up / Move down** buttons
  2. keyboard reordering on the focused row trigger (`Alt` + `ArrowUp` / `ArrowDown`)
  3. an optional **drag handle** in the list as pointer enhancement
- Keep focus on the moved row and announce the new position through a polite live region.
- Keep drag-and-drop off the graph canvas until you can provide equivalent keyboard semantics, clear drop targets, and honest placement rules.
- Avoid persistent numeric **order fields** as the primary UX unless authors truly need jump-to-position editing; they introduce duplicate-value conflicts, renumbering logic, and extra validation copy.
- If authors start from the canvas, route them into the reorder surface focused on the selected row instead of pretending the graph itself is freely draggable.

## Anti-Patterns

- Shipping graph-canvas drag as the only movement path
- Letting node drag imply authors can place items anywhere when layout is really computed
- Using a plain order number field as the default interaction for routine reordering
- Moving focus away after reorder and forcing users to rediscover where they are
