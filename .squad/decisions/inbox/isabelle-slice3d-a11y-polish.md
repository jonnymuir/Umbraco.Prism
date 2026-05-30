---
author: isabelle
date: 2026-05-30T15:30:00+01:00
status: review
area: workflow-editor
confidence: high
branch: squad/82-named-lanes-editor-slice
head: f133146 (slice 3b.1) → slice 3d
---

# Decision — Slice 3d a11y polish on gateway-first inspector and outline

## Summary

Five surgical fixes against Tangy's editor-reset A11y review
(`.squad/decisions/inbox/tangy-editor-reset-a11y-test-review.md`) plus the
two Playwright regression locks Tangy asked for. No backend changes.

## Fixes landed

1. **SHOULD-FIX #1** — outline transition row resolves gateway keys to display
   names via a local `_gatewayLabel` helper (mirrors the inspector pattern).
2. **SHOULD-FIX #2** — `_routeDescriptor` returns structured Lit markup with
   decorative `→` glyphs wrapped in `<span aria-hidden="true">` and a
   structured `aria-label` of the form
   `"from {Stage}, via split gateway {Name}, via join gateway {Name}, to {Stage}"`.
   Visible text unchanged.
3. **IMPROVE #5** — `.outline-gateway-button` picks up the same 3px `#ffdd00`
   `:focus-visible` outline rule the stage and transition buttons use.
4. **IMPROVE #4** — gateway selection from the outline now announces
   `"Selected gateway {Name}"` via `_announceHistory` (the existing polite live
   region at the editor host). No new announcer introduced.
5. **IMPROVE #3** — **picked option (a)**: nested gateway rows. Moved the
   gateway buttons from a sibling `<div class="outline-gateway-row">` into a
   real `<ul class="outline-gateway-list">` / `<li class="outline-gateway-item">`
   children of the stage `<li>`. **Why (a) over (b):** the DOM hierarchy now
   matches the conceptual ownership ("gateway belongs to stage"), no visible
   regression (the renamed CSS rules preserve the original padding/background),
   and authors get the implicit "this group belongs here" cue without an extra
   string of meta copy that would have made the outline noisier. Keyboard
   nav semantics and the focus ring carry over unchanged.

## Verifications

- **WORTH-NOTING #6** — confirmed `Waiting`/`StatusTimeline` are gone from
  `prism-workflow-graph.ts` (Slice 3b.1 closed this). Spec
  `workflow-stage-type-options.spec.ts` exists and is green.
- **Tangy new #1** — `workflow-editor-outline-a11y.spec.ts` proves an author
  changing a join gateway on a `decision-join` incoming route is announced via
  `#inspector-announcer`. Also asserts the current select option label is
  `"Decision join"` (display name), proving the picker itself speaks domain
  language.
- **Tangy new #2** — same spec asserts the outline DOM for the Draft stage's
  outgoing transition row contains `"Review split"` and not `\breview-split\b`.

## Validation

- `npm run build` ✅
- `npm run build-storybook` ✅
- Playwright (gateways, outline-a11y, history, shell, stage-type-options,
  transition-editor) — **18 pass / 4 pre-existing skips**

## Out of scope (untouched)

- `WorkflowSelection` union collapse — Slice 4
- Canvas slot-matrix, read-only graph mode, JSON twin-pane, workflow-json
  attribute, visual regression — Slices 4–7
- Backend
- Known-broken specs `workflow-editor-validation.spec.ts` and
  `workflow-editor-help.spec.ts` — Slice 5

## One non-obvious finding for Slice 4+

`_availableJoinGatewaysForStage` filters joins by `binding.anchorStageKey ===
toStage` (with a lane fallback when anchor is null). Because a join binding's
`anchorStageKey` is the post-join target stage, joins are only offerable on
routes that *land* at that target. You cannot add a previously-unset join to
a route by editing the source side. If Slice 4 wants "pick a join from any
branch route", that filter needs to widen (e.g., lane-key compare regardless
of anchor) or a separate "attach to existing join" affordance on the split
gateway inspector. This is why my Slice 3d test for IMPROVE #4 drives the
*clear* path rather than a no-op re-set.
