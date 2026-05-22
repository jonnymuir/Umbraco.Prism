---
name: "workflow-editor-shadow-dom-readiness"
description: "Keep workflow editor smoke readiness checks aligned with Shadow DOM hosts"
domain: "testing"
confidence: "high"
source: "observed + implemented (2026-05-18T13:17:12.103+01:00 issue #69 quality gate)"
---

## Context

Use this when a localhost smoke or walkthrough waits for a workflow-editor readiness marker. A selector that worked before a host-shell refactor can silently go stale if the marker moves from light DOM onto a node inside a custom element shadow root.

## Pattern

1. Do not use `page.waitForSelector(...)` on a readiness attribute that only exists inside Shadow DOM unless the test framework is explicitly piercing that boundary.
2. Prefer one of these contracts instead:
   - expose the readiness attribute on the custom-element host itself (the adopted fix for `prism-workflow-editor`), or
   - wait on visible user-facing shell content plus a shadow-aware locator/snapshot for the mounted editor state.
3. When a smoke fails with `customElementDefined: true` but `element-not-found` for the readiness marker, suspect selector drift before blaming the host app or API.
4. Remember that Playwright locators pierce open Shadow DOM, but `document.querySelector(...)` inside `page.evaluate()`/`waitForFunction()` does not. A readiness probe can therefore be green via `locator('prism-workflow-editor')` while ad-hoc DOM scripts still report `element-not-found`.
5. Pair the UI readiness check with a quick request and console audit so you can separate “editor did not load” from “editor loaded but the probe cannot see inside the shadow root,” and so harmless browser-surface gaps like a missing favicon do not get misclassified as host failure.

## Examples

- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor-shell.ts`

## Anti-Patterns

- Treating an unpierced light-DOM selector miss as proof that the editor failed to boot
- Mixing a shadow-aware Playwright locator with a document-level fallback script and assuming they should see the same DOM
- Waiting on hidden implementation markers when a stable visible host signal already exists
- Declaring localhost hosting broken without checking the live network calls and rendered shell first
