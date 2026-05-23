# Decision: Align Walkthrough Smoke Spec to Clean Shell UX

**Author:** Isabelle  
**Date:** 2026-05-24  
**Status:** Recorded

## Context

`prism-workflow-editor-shell` was refactored from a "reference integration page" (marketing copy, code snippet, textbox for API base, workflow count display) to a clean production-ready shell (`<h1>Workflow Editor</h1>`, `<select aria-label="Select workflow">`).

The walkthrough spec `01-planning-workflow-editor.walkthrough.spec.ts` was never updated to match, causing two CI jobs (`planning-workflow-editor-smoke`, `localhost-auth-playwright`) to fail with element-not-found on the old heading.

## Decision

**Update the spec to match current UX — do not roll back the shell refactor.**

The clean shell is the intended production surface. The walkthrough should test the experience as it actually is, not as it was during the reference integration phase.

## Changes

| Selector removed / changed | Reason |
|---------------------------|--------|
| `heading /compose the editor into your app/i` | Shell h1 is now "Workflow Editor" |
| `getByText(/this shell stays focused on authoring/i)` | Marketing copy removed from shell |
| `getByText(/let your business app own.../i)` | Marketing copy removed from shell |
| `combobox 'Workflow definition'` → `combobox 'Select workflow'` | `aria-label` changed with refactor |
| `getByRole('textbox', { name: 'Authoring API base' })` | Textbox removed from shell |
| `getByText(/<prism-workflow-editor/i)` | Code snippet removed from shell |
| `getByText(\`authoring-api-base=...\`)` | Code snippet removed from shell |
| `getByText(/4 workflow definitions discovered/i)` | Discovery count removed from shell |
| `#workflow-key option[value="planning"]` | No `#workflow-key` id; select is now by `aria-label` |
| `.hero` bounding-box ratio check | No `.hero` class in clean shell; check simplified to editor-frame height ratio |
| `[data-prism-panel-toggle="outline"]` → `[data-prism-outline-toggle]` | Attribute name in `prism-workflow-editor.ts` never matched the old test |
| `[data-prism-panel-toggle="properties"]` → `[data-prism-inspector-toggle]` | Attribute name in `prism-workflow-editor.ts` never matched the old test |

## Principle

Tests are the executable counterpart of the intended UX. When UX changes intentionally, tests must follow in the same commit. This was the exception — the spec was not updated when the shell was refactored.
