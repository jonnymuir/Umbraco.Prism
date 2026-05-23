---
name: "workflow-graph-role-lane-rendering"
description: "Render dynamic role-first swim lanes from stage actor metadata"
domain: "workflow-editor"
confidence: "high"
source: "observed (2026-05-22T19:33:56.538+01:00 issue #74 role-first swim lanes)"
---

## Context

Use this when building or extending workflow graph canvas code that needs to group stages by role ownership instead of treating the workspace as a generic node field.

## Patterns

- **Compute lanes dynamically** from stage actor data during layout pass; do not hardcode lane IDs.
- **Group stages by actor** with lowercase-normalised comparison and surface fallback ("reviewer" for back-stage, "public" for front-stage) when actor is empty.
- **Render lane headers** with role label (from `workflow.roles[]` or humanised actor string), stage count, and descriptive copy.
- **Position stages horizontally** in their role's lane row with x-coordinate based on stage index and y-coordinate based on lane row.
- **Route transitions** with lane awareness: same-lane transitions use horizontal cubic Bézier; cross-lane transitions use distance-aware curved paths.
- **Make lanes focusable** as `<section tabindex="0">` elements with `aria-labelledby` and `aria-describedby` for keyboard + screen reader access.
- **Announce lane context** on focus: "{Role label} lane. {N} stage(s). {Role description}."
- **Label stages with role context**: aria-labels should reference the role ("Declaration, Applicant role") not generic "front stage".
- **Use consistent curve calculation** for transitions: `Math.min(Math.max(distance / 2, 56), 180)` based on horizontal or vertical distance.
- **Single context-aware Add Stage** button instead of separate front/back buttons; infer surface from selected stage's actor if available.

## Role Label Resolution

1. Normalise actor to lowercase
2. Search `workflow.roles[]` for matching `roleKey` or `claimMapping`
3. Use role's `displayName` if found
4. Otherwise, humanise actor string: split by `-_\s`, titlecase each part
5. Examples: "applicant" → "Applicant", "planning-officer" → "Planning Officer"

## Role Description Defaults

Provide helpful copy for common roles:
- "public", "applicant", "resident", "citizen", "customer" → "Public-facing stages and handoffs"
- "member" → "Signed-in member stages and handoffs"
- "reviewer", "caseworker", "officer", "administrator", "admin" → "Review and decision stages"
- "system" → "Automated checks and status stages"
- Unknown → "{Role label} stages and handoffs"

## Accessibility Requirements

- Lane `<section>` must be focusable (`tabindex="0"`)
- Lane must have visible focus indicator (3px solid outline, 3px offset)
- Lane header must have unique `id` for `aria-labelledby`
- Lane description must have unique `id` for `aria-describedby`
- Graph workspace must have `aria-roledescription="Role-first workflow editor workspace"`
- Tab navigation order: lanes → stages → transitions → transition handles

## Examples

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts` — `_layout` getter computes lanes, `_renderGraph` renders lane sections
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.stories.ts` — Stories validate lane presence with `[data-prism-role-lane]`
- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` — Smoke test checks role lanes exist and graph has role-first aria-roledescription

## Anti-Patterns

- Hardcoding front/back lane IDs instead of computing from actors
- Making lanes non-focusable background decoration
- Using generic "front stage" / "back stage" aria-labels without role context
- Separate "Add front stage" / "Add back stage" buttons instead of context-aware single button
- Static lane rendering that requires code changes when roles are added
- Caching lane structure across renders instead of recomputing from current stage list
