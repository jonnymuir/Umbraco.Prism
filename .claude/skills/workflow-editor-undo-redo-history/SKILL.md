---
name: "workflow-editor-undo-redo-history"
description: "Host-level undo/redo pattern for workflow editor mutations"
domain: "frontend"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #63)"
---

## Context

Use this when a workflow-editor slice adds or changes authored mutations across the graph workspace, inspector, or shared action editor. The workflow model is edited from multiple child components, so undo/redo becomes unreliable if each surface tries to keep its own history.

## Pattern

1. Keep the undo/redo stack in `prism-workflow-editor`.
2. Treat `workflow-updated` as the single mutation event for history capture.
3. Snapshot both the authored workflow JSON and the current stage/transition selection so undo/redo restores the editing context, not just the data.
4. Pass the selected stage/transition back down into `prism-workflow-graph` so graph highlighting stays aligned after host-driven undo/redo.
5. Expose recovery through three affordances together:
   - toolbar buttons with disabled states
   - keyboard shortcuts (`Ctrl/Cmd+Z`, `Ctrl/Cmd+Shift+Z`)
   - a visible status bar plus polite live-region announcements
6. Reset history only when a fresh workflow loads; preview, validation, and proposal review should not clear local history.
7. Cap retained entries to the latest 50 changes.

## Why this works

- Graph, inspector, and action editor mutations all already converge on one event seam, so host-level history captures every edit without new coupling.
- Restoring selection keeps the user anchored in the same stage or transition after undo/redo, which matters for keyboard users and inspector-driven editing.
- Keeping preview and validation outside the reset boundary preserves author confidence: inspection does not destroy recovery.

## Anti-Patterns

- Separate undo stacks inside graph and inspector
- Capturing only workflow JSON without restoring selection context
- Clearing history when validation banners or preview modals open
- Adding keyboard shortcuts without visible disabled-state buttons or status feedback
