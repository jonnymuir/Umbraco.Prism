# Slice 6 — JSON twin-pane Definition tab

**Author:** Isabelle (Frontend/a11y)
**Branch:** `squad/82-named-lanes-editor-slice`
**Status:** Landed.

## What shipped

A new top-level **Definition** tab in `<prism-workflow-editor>` containing an
editable JSON view of the current `AuthoredWorkflow`, synced bidirectionally
with the visual editor. Author-facing copy uses "Definition" — the word
"JSON" only appears in subcopy ("Power-user view…").

## Library choice — CodeMirror 6 (not Monaco)

Picked **CodeMirror 6** over Monaco:

| Concern | CodeMirror 6 | Monaco |
|---------|-------------|--------|
| Bundle size | ~351 KB minified across CM modules | ~1 MB+ |
| Shadow-DOM mounting | Mounts cleanly into a host `<div>` inside Lit's shadow root | Historically fights shadow DOM (styles, focus, web worker placement) |
| Keyboard a11y | Built-in `defaultKeymap` + `historyKeymap` + linter | Built-in |
| Modularity | Cherry-pick only what we need | Monolithic |
| Maintenance | Active | Active |

CM6 is loaded **dynamically** from `prism-definition-editor-codemirror.ts`
the first time the Definition tab is activated (`_handleConfidenceTabChanged`
calls `import('./prism-definition-editor.js')`, which itself triggers the
CodeMirror chunk). Authors who stay on Canvas pay zero extra bytes.

## Bundle delta

| File | Before Slice 6 | After Slice 6 | Notes |
|------|---------------|---------------|-------|
| `workflow-editor.js` (main) | 321 KB | **335 KB** | +14 KB for canonical serializer, lint, host wiring |
| `prism-definition-editor-*.js` | — | 4 KB | Element shell, statically importable |
| `prism-definition-editor-codemirror-*.js` | — | 351 KB | **Code-split**, lazy-loaded |

**Synchronous load: 335 KB — well under the 600 KB Slice budget.** Total
including lazy chunk = ~690 KB, but only paid by power users who open the
Definition tab. This honours Jonny's "the JSON pane is for power users;
default flow stays visual" preference.

## Apply / debounce model

* Typing fires `definition-input` with the new text.
* The host debounces **250 ms** before parsing.
* On settling:
  - **JSON valid + schema-clean** → `coerceParsedAuthoredWorkflow` →
    `_commitWorkflowUpdate` (lands on the document-level undo stack) → polite
    live-region announcement ("Definition updated. N stages, M gateways.").
  - **Parse error** → banner shows the error + disabled "Apply when valid" +
    enabled "Revert to current"; visual pane stays on last good state.
  - **Schema violation** (retired `Waiting`/`StatusTimeline` kind, unnamed
    gateway, duplicate keys, missing required fields) → same banner UX with
    a human-readable summary.

Schema/lint mirrors PROJ140/141/142: retired stage kinds are *rejected* in
the Definition pane (the visual side silently rewrites `Waiting → Question`
with a warning marker; the Definition pane refuses to apply so authors see
exactly what the server would reject).

## Undo coordination

The directive: one undo step from either pane reverses the last logical
change.

* While the JSON is **dirty but not applied** (mid-typing, or invalid),
  Ctrl/Cmd-Z stays local to CodeMirror's internal history (CM6's `history()`
  extension).
* Once a valid debounce **applies**, the change goes through
  `_commitWorkflowUpdate` → the same `_undoHistory` stack the visual side
  uses. A Ctrl/Cmd-Z from the Canvas tab toolbar reverses the JSON-applied
  change; the host's `updated()` lifecycle then re-pushes the prior canonical
  text into the Definition pane.

Verified by the Playwright spec `Document-level undo from the visual side
reverses a Definition-applied JSON edit`.

## Canonical serialization

`workflow-canonical-json.ts` exposes `serializeAuthoredWorkflow(w)` →
deterministic JSON with:

* Top-level keys ordered: `definitionKey`, `displayName`, `version`,
  `schemaVersion`, `instancePolicy`, `initialStageKey`, `authorNote`,
  `roles`, `stages`, `gateways`, `transitions`.
* All nested keys sorted alphabetically.
* 2-space indent.

This stability prevents spurious diffs when the visual side commits — the
editor only overwrites the JSON text when the canonical actually changed.

## A11y

* Tab is reachable via the existing roving-tabindex tab harness (arrow keys
  cycle Canvas → Validation → Preview → Simulation → Definition → Help).
* CodeMirror is keyboard-only navigable by default; the editor host carries
  `aria-label="Workflow definition JSON editor"` and
  `data-prism-definition-editor-input` for tests.
* Diagnostics meet 4.5:1 contrast on white (`#b10e1e` border + `#fbeaec`
  background for errors; `#594d00` border + `#fff4d3` background for
  warnings).
* Apply / Revert buttons sit in tab order (standard `<button>`).
* Live region (`aria-live="polite"`) announces "Definition updated. N
  stages, M gateways." after each successful apply, and "Definition reverted
  to the current workflow." after Revert.

## Out of scope / deferred

* **Read-only at the editor-host level** — `<prism-definition-editor>` has
  a `read-only` flag wired in but `<prism-workflow-editor>` doesn't yet
  surface read-only mode. Slice 8 territory.
* **Full JSON-Schema-driven linting from `authored-workflow.schema.json`** —
  the schema lives on the server. The Definition pane runs the same
  hand-coded checks the editor uses elsewhere (retired kinds, named
  gateways, required top-level fields, duplicate keys). If we ever want
  hover-doc support, we'd bundle the schema and switch to a schema-aware
  linter. Not needed for this slice.
* **Auto-fix suggestions** — banner only revert/apply for now. "Auto-fix"
  could come later if authors complain.
* **Visual regression / screenshot coverage** — Slice 7.
* **Docs walkthrough overhaul** — Slice 8.

## Tests

`tests/workflow-editor/workflow-editor-definition-tab.spec.ts` — 7
behavioural Playwright tests, all green:

1. Definition tab shows the current workflow as JSON
2. JSON rename → debounce → visual pane updates + live-region announcement
3. Parse-error JSON → banner + Apply disabled + visual unchanged
4. Schema-invalid JSON (`Waiting` kind) → banner + Apply disabled
5. Visual change → Definition tab reflects within one tick
6. Document-level undo from Canvas reverses an applied JSON edit
7. Definition tab is keyboard-reachable and CodeMirror accepts keyboard input

Full workflow-editor regression sweep: 61 passed (+ 11 pre-existing skipped,
1 flaky on history that recovered on retry — pre-existing flake, not new).

## Files

New:
* `src/UmbracoPrism.Client/src/workflow-editor/prism-definition-editor.ts`
* `src/UmbracoPrism.Client/src/workflow-editor/prism-definition-editor-codemirror.ts`
* `src/UmbracoPrism.Client/src/workflow-editor/workflow-canonical-json.ts`
* `src/UmbracoPrism.Client/src/workflow-editor/workflow-definition-lint.ts`
* `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-definition-tab.spec.ts`

Modified:
* `prism-confidence-tabs.ts` — added `definition` tab slot + button
* `prism-workflow-editor.ts` — Definition state, sync wiring, render, styles
* `package.json` / `package-lock.json` — CodeMirror 6 deps
* `src/UmbracoPrism.Client/src/workflow-editor/README.md` — Definition
  tab documentation
