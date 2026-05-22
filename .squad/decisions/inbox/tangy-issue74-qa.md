## Decision: Issue #74 slice QA gate is not green

**Date:** 2026-05-22T19:18:38.794+01:00  
**Author:** Tangy  
**Status:** Proposed  

The first `#74` slice is currently blocked at the client build seam, so it should not be treated as green yet.

## Evidence

Focused client gate started with:

1. `cd src/UmbracoPrism.Client && npm run build`

That build fails in `src/workflow-editor/prism-workflow-editor.stories.ts`:

- line 270: `Property '_proposal' does not exist on type 'never'`
- line 271: `Property '_modalOpen' does not exist on type 'never'`

## Why it fails

The updated `ModalOpen` story now tries to force private editor state with this cast:

`PrismWorkflowEditorElement & { _proposal: ..., _modalOpen: boolean }`

TypeScript collapses that intersection to `never` because `_proposal` is already a private member on `PrismWorkflowEditorElement`, so the build stops before Storybook or Playwright seams can run.

## Smallest next fix

Keep the story-only modal setup, but stop intersecting with the component class for private fields. Use a loose story-only escape hatch (for example a plain structural cast) or expose a safe helper seam for opening the modal in stories.
