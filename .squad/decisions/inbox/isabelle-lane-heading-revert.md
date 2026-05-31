# Decision: BUG-VR-1 sticky lane headers deliberately reversed

**Date:** 2026-05-31  
**Author:** Isabelle (via Copilot)  
**Branch:** `squad/82-named-lanes-editor-slice`

## Context

Slice 7.5 fixed BUG-VR-1 by giving `.lane-header` `position: sticky` so the lane label remained visible as users scrolled down a tall lane in the workflow-graph canvas. A Playwright spec (`workflow-canvas-scroll.spec.ts`) was written to guard this behaviour.

## Decision

At Jonny Muir's explicit request (2026-05-31), the sticky behaviour has been removed. Lane headers are now plain flow elements that sit at the top of their lane and scroll away with the canvas when the user scrolls down.

The associated Playwright assertion has been updated to confirm the header is **not** sticky — i.e. that it scrolls with the canvas rather than staying pinned.

## Why this is not a regression

Future visual-test reviewers should treat any diff that shows a lane header moving out of view on scroll as **correct** behaviour, not a regression. The spec `LARGE_WORKFLOW: lane header scrolls with the canvas (not sticky)` is the authoritative guard for this intent.

## Files affected

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts` — `.lane-header` rule stripped of sticky declarations
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-canvas-scroll.spec.ts` — sticky assertion replaced with non-sticky assertion
