---
name: "workflow-editor-copy-paste"
description: "Host-owned copy/paste pattern for workflow editor stages and actions"
domain: "workflow-editor"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #64)"
---

## Context

Use this when a workflow editor needs copy/paste across multiple authoring surfaces (graph workspace, inspector action lists, toolbar shortcuts) without fragmenting clipboard behaviour.

## Pattern

1. Keep clipboard state, toolbar buttons, and keyboard shortcuts in the host editor component.
2. Let structural surfaces contribute **selection**, not their own clipboard implementations.
3. Copy stages as authored stage payloads only — fields, waits, actions, editor hints — and intentionally exclude transitions so pasted nodes re-enter the validation flow safely.
4. Copy actions with all params, but normalise timing on paste to the destination context so stage and transition action lists can share one clipboard.
5. After paste, select the new stage or action and surface clipboard status in visible text so keyboard users know what will happen before they paste.

## Why this works

- Host ownership keeps `Ctrl/Cmd+C`, `Ctrl/Cmd+V`, toolbar state, undo history, and accessibility announcements aligned.
- Excluding transitions from stage copies avoids hidden routing duplication and makes validation warnings honest.
- Destination-aware action timing keeps copied actions portable across stages without forcing per-surface duplication logic.

## Anti-Patterns

- Writing directly to the browser clipboard from graph nodes or inspector rows without updating editor state
- Copying stage transitions implicitly when duplicating a stage
- Pasting actions with stale timing that is invalid for the destination context
- Pasting content without moving selection to the new item
