---
date: 2026-05-04T11:46:55.877+01:00
author: blathers
status: PROPOSED
area: admin-ui, walkthroughs, mock-business-app
---

# Workflow Admin Definitions Panel Is Collapsed by Default

## Context

The `/admin/workflow` page in MockBusinessApp rendered all workflow definition cards fully expanded on load. With multiple definitions, each showing a states table, transitions table, and Mermaid diagram, the page became visually overwhelming for walkthrough screenshots and manual operator use.

## Decision

**Workflow definition cards on the admin screen are collapsed by default.** Operators click a card header to expand it. The Mermaid diagram is rendered on first expand (deferred, not on page load).

Supporting affordances added:
- Expand All / Collapse All toolbar buttons above the definitions panel.
- Animated toggle arrow (▶ → ▷ rotation) on each card header to communicate interactive state.
- Instance IDs in the instances table are truncated to 8 chars + "…" with the full ID accessible via `title` tooltip — reduces horizontal noise while preserving debuggability.

## Rationale

- Walkthrough screenshots need a clean, focused frame — a page-length wall of expanded cards is not photogenic.
- Operator manual use benefits from summary-first layouts: inspect the instances table first, expand a specific definition only when needed.
- No capability is removed: all expand/inspect/edit/advance/reset actions still work.

## Implementation

`src/UmbracoPrism.MockBusinessApp/Program.cs` — admin UI HTML template:
- `.def-body { display: none }` + `.def-card.open > .def-body { display: flex }` toggle via JS.
- `toggleCard(hdr)` function wired to `.def-header onclick`; skips toggle when a child button is the target.
- Mermaid init changed to `startOnLoad: false`; `window._mermaid.run()` called per card on first expand.
- Expand/Collapse All helpers wire to toolbar buttons.
- Instance ID column: `shortId = id.Length > 12 ? id[..8] + "…" : id` with `title` for full ID.
