---
name: "workflow-assignment-source-of-truth"
description: "Keep workflow lane meaning derived from actor/role assignment and strip editor-only surface hints before projection or publish"
domain: "workflow-editor"
confidence: "high"
source: "observed (2026-05-25T09:54:48.365+01:00 issue #81)"
---

## Context

Use this when the workflow editor has accumulated both authored assignment data (`actor`, `roleGates`) and a second UI-only surface flag that tries to say the same thing. That duplication makes later lane redesign risky because the editor can publish stale or contradictory meanings.

## Patterns

- Treat `actor` and `roleGates` as the authored source of truth for lane assignment.
- When named lanes become first-class, keep shared assignment on workflow-level lane definitions and let stages/gateways reference the lane by key.
- Project effective assignment back onto published state/gateway metadata so current runtime consumers stay assignment-driven even before multi-lane execution lands.
- Derive editor lane labels, descriptions, and styling from that assignment data in one shared helper.
- Prefer one author-facing **lane owner** control over separate actor/surface toggles, then translate that value back into `actor` and `roleGates` in one shared helper.
- Generate list filters and similar navigation affordances from the lane keys actually present in the workflow instead of hard-coding journey/operations buckets.
- Ignore or strip legacy surface hints on the way back to preview/project/publish APIs so the runtime contract stays clean.
- If validation links originate in a non-canvas tool tab, switch back to Canvas before focusing the selected inspector target.
- Keep behavioural tests pinned to visible assignment copy and navigation outcomes, not internal surface enum names.

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/workflow-stage-assignment.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-stage-preview.spec.ts`

## Anti-Patterns

- Writing `editorSurface` back into authored workflow payloads when actor/role data already defines the assignment.
- Letting preview or publish depend on UI-only lane flags.
- Leaving validation jump targets hidden behind another tab and calling that “jump to item” support.
