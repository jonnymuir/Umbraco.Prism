# Decision: Issue #74 implementation pattern — Role lanes as dynamic structural elements

**Date:** 2026-05-22T19:33:56.538+01:00  
**Author:** Isabelle  
**Status:** Implemented  

## Decision

Workflow graph role lanes are rendered as dynamic structural elements from stage actor data, not static lane dividers. Each role gets a focusable `<section>` with semantic labels, stage counts, and descriptions for keyboard + screen reader access.

## Implementation Pattern

### Lane rendering
- Compute lanes from stage actors during layout pass
- Group stages by actor (lowercase-normalized) with fallback to surface default
- Render lane header with role label (from workflow roles or humanised actor), stage count, and description
- Position stages horizontally in their role's lane row with lane-aware transition routing

### Accessibility structure
- Each lane is a focusable `<section>` with `tabindex="0"`
- Lane headers use `aria-labelledby` (role name) and `aria-describedby` (role description)
- Focus announcement: "{Role label} lane. {N} stage(s). {Role description}."
- Stage aria-labels reference the role: "Declaration, Applicant role" (not "front stage")
- Graph workspace has `aria-roledescription="Role-first workflow editor workspace"`

### Keyboard navigation
- Tab through lanes, then stages within lanes, then transitions
- Lane focus shows visible outline (3px solid #ffdd00, 3px offset)
- Lane descriptions announced on focus for screen reader context

### Transition routing
- Same-lane transitions: horizontal cubic Bézier from right edge to left edge
- Cross-lane transitions: curved path with distance-aware control points
- Both paths use consistent curve calculation (56–180px based on distance)

### Add stage behaviour
- Single "Add stage" button (context-aware)
- If stage selected, use that stage's surface hint for new stage default
- If no stage selected, default to 'front-stage'
- Context menu simplified to single "Add stage" option

### Role label resolution
1. Check `workflow.roles[]` for matching `roleKey` or `claimMapping`
2. Use role's `displayName` if found
3. Fallback to humanised actor string (split by `-_\s`, titlecase each part)
4. Examples: "applicant" → "Applicant", "planning-officer" → "Planning Officer"

### Role description defaults
- Common roles have descriptive copy: "Public-facing stages and handoffs", "Review and decision stages", "Automated checks and status stages"
- Unknown roles: "{Role label} stages and handoffs"

## Why This Pattern

- **Dynamic lanes**: Supports arbitrary roles without hardcoded lane IDs
- **Semantic structure**: Lanes are not just visual dividers; they're navigable landmarks
- **Screen reader friendly**: Lane focus + description announcement provides context before drilling into stages
- **Keyboard parity**: Tab navigation follows role structure instead of flat node list
- **Maintainable**: Adding roles requires no graph code changes; they render automatically

## Consequences

- Lanes are computed every render (acceptable for typical workflow sizes)
- Lane order follows stage definition order (first unique actor seen defines lane position)
- Empty roles (no stages) don't render a lane
- Actor changes immediately reflow lanes (no cached lane structure)

## Anti-Patterns Avoided

- ❌ Hardcoded front/back lane IDs
- ❌ Lanes as non-focusable background decoration
- ❌ Separate "Add front stage" / "Add back stage" buttons
- ❌ Generic "front stage" / "back stage" aria-labels without role context

## Key Files

- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts` — Lane computation, rendering, focus handling
- `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.stories.ts` — Role lane validation in stories
- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` — Role lane structure smoke test
