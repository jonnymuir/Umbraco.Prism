---
date: 2026-05-23T14:04:58.778+01:00
author: tom-nook
branch: squad/74-role-first-swim-lanes
issue: "#74"
---

# Final Merge Decision — squad/74-role-first-swim-lanes

## Summary

Branch `squad/74-role-first-swim-lanes` is clean, green, and ready to merge to `main`.

## What Shipped

### Docs (Mabel's work + user direction)
- `docs/guides/README.md` — new index of developer guides
- `docs/guides/extending-prism.md` — guide for domain-specific extension on top of Core (vinyl example)
- `docs/guides/workflow-editor-composition.md` — guide for hosting the editor with minimal complexity
- `docs/walkthroughs/planning-workflow-editor.md` — updated for role-first swim lane UX
- `README.md` — updated with project status and guide references

### Client UX (Isabelle's work)
- `prism-workflow-graph.ts` — independent graph canvas scrolling
- `prism-workflow-editor-shell.ts` — host chrome minimization, simplified launch flow
- `prism-workflow-editor.ts` + `prism-workflow-outline.ts` — editor-prioritised layout
- `prism-confidence-tabs.ts` — improved accessibility and keyboard flow
- `prism-workflow-editor-shell.stories.ts` — new Storybook story for shell composition

### Tests (Tangy's work)
- `layout-professionalization.spec.ts` — 22 behavioral proof tests
- `workflow-browser-surface.spec.ts` — 22 browser-hosted proof tests
- `workflow-editor-shell.spec.ts` — shell behavioral proof
- `vertical-lanes-switcher.spec.ts` — lanes switcher behavioral contract
- `workflow-overflow-responsive.spec.ts` — responsive overflow tests
- `workflow-graph-layout-proof.spec.ts` — DOM geometry proof tests (scroll, lanes, zoom)
- Updated walkthrough and keyboard/stage-preview tests for swim lane selectors
- Updated baseline screenshots

### Squad metadata
- Deleted merged decisions/inbox/* files (Scribe had merged them into decisions.md)
- Added `.squad/agents/tangy/history-summary.md`
- Added `.squad/skills/workflow-editor-role-first-swim-lanes-testing/SKILL.md`

## What Was Excluded (Scratch Artifacts)

Not committed:
- `.copilot/session-plan.md` — session planning artifact
- `.copilot/session-summary.md` — session summary artifact
- `browser-surface-summary.txt` — session scratch note
- `layout-professionalization-checklist.md` — Tangy's transient implementation checklist for Isabelle (content superseded by test specs)
- `src/UmbracoPrism.Client/test-output.txt` — raw test runner output

## Validation

- ✅ TypeScript build: clean (0 errors)
- ✅ .NET tests: 815/815 passing
- ✅ Vinyl/Core boundary split: confirmed in prior Blathers/Tangy work

## Merge Outcome

PR opened from `squad/74-role-first-swim-lanes` → `main`, squash-or-merge as appropriate. All team-relevant changes documented in decisions.md via Scribe's inbox merge (commit 4ebdb23).
