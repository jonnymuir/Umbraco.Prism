---
name: "workflow-stage-preview-runtime"
description: "Render editor stage previews from the deterministic runtime projection without making the preview interactive"
domain: "workflow-editor"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #67 runtime stage preview slice)"
---

## Context

Use this when a workflow editor needs to show authors what one selected stage will look like after projection into Prism runtime format.

## Patterns

- Treat the authoring **`/project` endpoint as the preview source of truth** so preview and publish share one projection path.
- Keep a **small local projector fallback** only for Storybook or offline shells; do not let it replace the live app contract.
- Put preview request orchestration in the **host editor**: debounce edits, keep the last good preview visible while loading, and refresh whenever the selected stage or its authored fields change.
- Keep the preview **strictly read-only**:
  - disable every form control
  - render links/actions as static text or disabled buttons
  - keep focus in the editor workspace or inspector instead of the preview
- Show surface chrome separately from the projected form:
  - `public`
  - `member`
  - `back-stage`
  - disable surfaces that do not fit the selected stage

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-stage-preview.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-runtime-projection.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-stage-preview.spec.ts`

## Anti-Patterns

- Rebuilding runtime shell rules in the browser as a separate interpretation from publish
- Letting authors tab into a fake preview form and mistake it for editable data
- Clearing the previous preview immediately on each keystroke instead of showing an updating state
