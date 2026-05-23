---
date: 2026-05-23T08:30:10.563+01:00
author: mabel
status: implemented
related_files:
  - docs/guides/workflow-editor-composition.md
  - src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor-shell.ts
  - .squad/decisions/inbox/copilot-directive-2026-05-23T08-30-10.md
---

# Host Philosophy: Keep the Reference Shell Minimal

## Decision

Move all explanatory host content into user-guides documentation. Simplify the reference shell to a thin, focused interface for workflow selection and editor mounting. Remove dynamic authoring API configuration from the UI.

## Why

The reference shell was teaching two concepts at once:
1. How to mount the editor (simple)
2. Why hosts should stay thin (philosophical)

Mixing these made the UI cluttered. The shell serves an operational purpose (let developers choose workflows and mount the editor), while the philosophy and patterns belong in developer guides.

### User Directive

Jonny requested that the host stay minimal and easy to use: move explanatory chrome into documentation, simplify the launch/header, remove the editable authoring API base, and give the mounted editor enough vertical space to own the screen. Captured in `copilot-directive-2026-05-23T08-30-10.md`.

## What Changed

### Removed from Shell

- **Hero section** — "Compose the editor into your app with one element and one API base" copy moved to docs
- **Explanation text** — "This shell stays focused on authoring..." moved to docs
- **Editable authoring API field** — Hard-code or configure via environment; don't expose in UI
- **Integration snippet card** — Code example moved to guides
- **Why this host stays thin sidebar** — Full pattern moved to docs
- **Launch form with button** — Simplified to inline workflow selection

### Kept in Shell

- **Workflow selection dropdown** — Useful for testing multiple workflows
- **Minimal topbar** — Shows workflow title, selection if available
- **Full screen editor** — Removed side panels so editor owns the screen
- **URL parameter handling** — `?workflow=` still works for bookmarking

### Moved to Docs

**New guide:** `docs/guides/workflow-editor-composition.md` covers:

1. **The simplest way** — One element, one API base code snippet
2. **Why hosts stay thin** — Clear split of responsibilities
3. **Configuration philosophy** — What belongs in docs, API, vs. UI
4. **Building custom hosts** — Step-by-step pattern with examples
5. **Next steps** — Links to design docs and setup guides

## Impact

### For Developers Using the Reference Shell
- Cleaner, faster UI for selecting and editing workflows
- More screen real estate for the editor itself
- Same functionality, simpler interface

### For Developers Building Custom Hosts
- Clear philosophy and patterns in guides
- Code examples show the minimal approach
- Understand the "why" behind design decisions

### For Squad Continuity
- Documentation is now the source of truth for host philosophy
- Reference shell is implementation example, not tutorial
- Easier to keep in sync: changes to philosophy update docs once, shell design stays stable

## Basis

- User directive for simplification and screen real estate
- Editor-first design principle: the editor is interesting, the host is boring
- Separation of concerns: UI does mounting and selection, docs teach philosophy
- Pattern alignment: thin shells, thick business logic (existing Prism principle)

## Future Work

None required. The shell is now stable and minimal. If hosts need more features, add them to guides as patterns rather than baking into the reference.
