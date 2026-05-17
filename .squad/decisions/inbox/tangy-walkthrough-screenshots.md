---
date: 2026-05-17T17:33:13.797+01:00
author: tangy
status: inbox
---

# Decision: Regenerate walkthrough screenshots after reference shell extraction

## Context

The library extraction refactor (`feat/workflow-editor-library-extraction`) introduced a new reference shell
(`<prism-workflow-editor-shell>`) and a `/workflow-editor` redirect in MockBusinessApp. The planning workflow
editor walkthrough spec (`01-planning-workflow-editor.walkthrough.spec.ts`) was updated to test the new shell
flow, but the screenshots in `docs/images/walkthroughs/planning-workflow-editor/` were captured against the
old direct-URL flow (`/workflow-editor.html?workflow=planning`) before the reference split.

The walkthrough doc also had only placeholder text (`<!-- Screenshot: ... -->`) and had never been updated
with actual `![](...)` image embeds.

## Decision

1. **Commit all reference-split changes** (shell component, runtime library, MockBusinessApp wiring, spec
   updates, doc updates) in a single commit on `feat/workflow-editor-library-extraction` (commit `47a50cf`).

2. **Update the walkthrough doc** to:
   - Embed real screenshot references (replacing all placeholder text).
   - Update Step 1 narrative to describe the new shell redirect and thin-shell guidance copy.
   - Update API path references in Step 7 to reflect the extracted `/api/workflow-authoring/workflows/{key}/preview` and `.../apply` routes.
   - Update the R5 spec back-reference to the renamed file (`01-planning-workflow-editor.walkthrough.spec.ts`).

3. **Trigger `capture-screenshots.yml`** (workflow_dispatch, run 25996681743) on the feature branch to
   regenerate the 8 PNGs from the new shell flow. The workflow commits updated images back to the branch
   automatically when complete.

## Rationale

Screenshots are behavioural documentation — they must match what the spec asserts. The old screenshots showed
the raw editor page without the reference shell UI. The new screenshots must show:
- Step 1: the thin shell with hero copy, workflow picker, API base input, and integration snippet.
- Steps 2–9: unchanged (the embedded editor behaviour is the same).

The capture-screenshots workflow is the canonical regeneration path (SKILL: walkthroughs-as-executable-specs,
R6) — it sets `CAPTURE_SCREENSHOTS=1` and commits images back to the branch, keeping docs and screenshots
in lockstep with the spec.

## Impact

- `docs/walkthroughs/planning-workflow-editor.md` — updated (screenshots embedded, narrative corrected).
- `docs/images/walkthroughs/planning-workflow-editor/*.png` — will be refreshed by workflow run 25996681743.
- No test behaviour changed; the spec already asserted the shell flow. Screenshot update is docs-only.
