---
name: "workflow-slot-canvas-movement-model"
description: "Choose command-first movement for slot-based workflow canvases and treat drag as progressive enhancement"
domain: "workflow-editor"
confidence: "high"
source: "observed (2026-05-26T19:58:39.416+01:00 movement-model review)"
---

## Context

Use this when a workflow editor has moved to a lane/slot-based canvas and the team needs to decide how authors should reorder or reposition stages without breaking accessibility or the automatic layout model.

## Patterns

- Keep node placement derived from workflow structure; do not let freeform dragging become persisted canvas coordinates.
- Make **explicit move commands** the primary movement contract:
  - move earlier / later
  - move to lane
  - insert before / after where creation is the safer action
- Keep the list/table workspace as the accessibility-first structural editing surface, even if the graph remains the main visual workspace.
- Reuse one movement mutation path for list actions, keyboard shortcuts, context-menu actions, toolbar actions, and any future drag affordance.
- Treat drag-and-drop as a later convenience layer only if it snaps to valid ghost slots and executes the same command path as keyboard/menu movement.
- Move authored **stages** directly; keep **gateways** derived from routing topology unless there is an explicit later need for constrained gateway movement.
- Announce movement through the live region and keep focus on the moved item after reorder.
- Avoid numeric order fields as the default UX in branching graphs; they imply a false single sequence and create unnecessary validation states.

## Anti-Patterns

- Free-dragging nodes to arbitrary pixels in a slot-based graph
- Making drag-and-drop the only pleasant movement path
- Persisting manual gateway positions that fight the layout engine
- Using numeric order inputs as the main authoring interaction for multi-lane branching workflows
- Letting graph movement and list movement mutate different ordering contracts
