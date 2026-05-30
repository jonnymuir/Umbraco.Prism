---
author: tangy
date: 2026-05-30T13:00:00+01:00
status: review
area: workflow-editor
confidence: high
branch: squad/82-named-lanes-editor-slice
head: a251bcd (slice 3a) / b03ee38 (slice 3b)
---

# A11y & test-quality review — editor reset slices 1+1.5+2+3a+3b

## Accessibility verdict

Slice 3b's gateway-first inspector holds the WCAG line — but only just. The new
`Leave through` / `Arrive through` selects are properly labelled, focusable, and
the polite live region announces every change. The outline still nests gateways
inside their anchor stage's `<li>` (good DOM hierarchy), and the help dialog's
focus trap survives the proposal-modal removal. Two real gaps remain: the
outline transition summary leaks gateway **keys** to screen readers instead of
display names, and the new `_routeDescriptor` is a flat string joined with `→`
glyphs with no semantic structure or `aria-label`, so a screen reader reads
"Draft right-arrow Review split right-arrow Decision join right-arrow
Confirmation" with no notion that the middle items are gateways. Net direction:
**hold, with two targeted fixes for Isabelle in Slice 3b.1**.

## A11y findings

1. **SHOULD-FIX** — WCAG 1.3.1 (Info & Relationships), 2.4.6 (Headings & Labels) —
   `prism-workflow-outline.ts:195-200` — Outline transition summary renders raw
   gateway *keys* (`transition.fromGateway`, `transition.toGateway`) rather than
   display names. A screen reader user hears identifiers like `review-split`
   instead of "Review split". The inspector's `_routeDescriptor`
   (`prism-step-inspector.ts:158-169`) correctly uses `_gatewayLabel(…)`. **Fix:**
   reuse `_gatewayLabel` (or equivalent lookup) in the outline so the audible
   text matches the visible domain language.

2. **SHOULD-FIX** — WCAG 1.3.1 (Info & Relationships) — `prism-step-inspector.ts:162-168`
   and `prism-step-inspector.ts:1224-1232` — `_routeDescriptor` joins
   stage/gateway labels with the U+2192 arrow inside a single `<span>`. The
   arrow is decorative and inconsistently announced; there is no `aria-label`
   that says "from … via split gateway … via join gateway … to …". **What a
   screen reader user experiences:** a run-on string with no signal that the
   middle tokens are routing nodes, just four titles glued by an arrow char.
   **Fix:** wrap the visible `→` in `<span aria-hidden="true">` and provide an
   `aria-label` (or `<span class="sr-only">`) such as
   `"from Draft, via split gateway Review split, via join gateway Decision join, to Confirmation"`.
   Optionally upgrade the outgoing-routes list to a `<dl>`/structured layout so
   each segment has a role.

3. **IMPROVE** — WCAG 4.1.2 (Name, Role, Value) — `prism-workflow-outline.ts:120-215` —
   Outline is `<nav>` + `<ol>` + `<li>` with no `role="treeitem"`/`aria-level`,
   and gateway rows sit as a sibling `<div>` inside the stage's `<li>` rather
   than as their own `<li>` child of a sub-list. This is *not* a violation — the
   flat list passes — but a screen reader user has no auditory cue that a
   gateway "belongs to" its anchor stage beyond reading order. **Fix:** either
   move gateway buttons into a nested `<ul>` under the stage `<li>`, or add
   visible+audible text like "Belongs to Application form" inside the gateway
   row.

4. **IMPROVE** — WCAG 4.1.3 (Status Messages) — `prism-workflow-outline.ts` and
   `prism-workflow-editor.ts:991-994` — selecting a gateway from the outline
   fires `outline-gateway-selected` but emits no announcement. Stage selection
   has the same gap. The inspector announcer covers *edits*, not *selection
   changes initiated from outline*. **Fix:** announce
   `"Selected gateway Review split"` via the existing polite region when a
   gateway is picked from the outline or graph.

5. **IMPROVE** — WCAG 2.4.7 (Focus Visible) — `prism-workflow-outline.ts:321-325,
   433-437` — Stage and transition buttons have a 3px `#ffdd00` focus ring, but
   the new gateway button (`.outline-gateway-button`, lines 364-381) has **no**
   `:focus-visible` rule. Falls back to UA default, which against the purple
   border may be low-contrast. **Fix:** add the same yellow outline rule.

6. **WORTH-NOTING (out of scope but flagged)** — `prism-workflow-graph.ts:2724`
   still offers `'Waiting'` and `'StatusTimeline'` in the list-view "Stage type"
   `<select>`. Picking either now produces a stage that fails PROJ140 on save.
   Not strictly an a11y bug, but it routes assistive-tech users straight into a
   silent validation trap. Isabelle should drop them from the option list as
   part of Slice 3b.1.

7. **PASS — explicitly confirmed:**
   - F1 help dialog still traps focus (`prism-workflow-editor.ts:951-980,
     1391-1399`). The `.modal-backdrop` CSS is preserved and the dialog uses
     `role="dialog"` + `aria-modal="true"`; no `inert` regressions detected.
   - List-workspace reorder (Move up / Move down + `Alt+ArrowUp/Down`) still
     present at `prism-workflow-graph.ts:2614, 2797-2811` with polite live
     announcements (`_announce` at line 1403). The list workspace remains the
     canonical screen-reader-friendly structural editor.
   - New gateway selects are keyboard-reachable via implicit `<label>` wrapping
     (`prism-step-inspector.ts:613-642`); each `_announce(...)` call writes to
     the polite region at line 1264.
   - Tab order through Canvas / Validation / Preview / Simulation / Help tabs
     not affected by the proposal-modal removal.

## Test quality findings

1. **BLOCKER** — `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-validation.spec.ts:48, 68`
   — Spec asserts `[data-prism-canvas-health-hint]` and `[data-prism-open-validation]`
   selectors that **do not exist anywhere in source** (grep returns zero hits).
   Both tests in the file will fail the moment they run. The spec is
   *actively misleading* — it looks like a coverage win but is a future-state
   contract for Slice 5. **Behavioural assertion needed once Slice 5 lands:**
   "Author sees a Canvas health hint and can jump from Canvas to Validation
   without losing context." **Owner:** Tangy to skip-with-comment now; revisit
   when Isabelle ships Slice 5 canvas-slot-matrix.

2. **BLOCKER** — `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-help.spec.ts:93`
   — Empty-state assertion expects copy `"Add the next stage before you branch"`
   that does not exist in `prism-workflow-graph.ts:2545-2555` (the actual copy
   is `"Add the first stage, then connect routes as you model the author
   journey."`). Test will fail on the empty-workflow story. The other two tests
   in the file are healthy. **Behavioural assertion needed:** "Empty workflow
   prompts the author to add the first stage, then surfaces help."
   **Owner:** Tangy (skip the one stale `test('empty workflows…')` block; keep
   tests 1 and 2 live).

3. **SHOULD-FIX** — `src/UmbracoPrism.Core.Tests/Workflow/Authoring/AuthoredWorkflowValidationTests.cs`
   — `Project_WaitingStage_InGatewayOnlyModel_IsRejected` (line 260) bundles
   *two* PROJ140 triggers: `"type": "Waiting"` AND a `"waiting": {…}` payload.
   The validator (`AuthoredWorkflowSchemaValidator.cs:49-55`) has three
   independent triggers: legacy kind `Waiting`, legacy kind `StatusTimeline`,
   and the `HasLegacyWaitingPayload` JSON sentinel. **The sentinel-only path is
   not isolated by any test.** A future refactor could silently drop the
   `HasLegacyWaitingPayload` check and this test would still pass.
   **Behavioural assertion to add:** "Author posting a stage with only a
   `waiting` JSON payload (no retired type) is told the waiting story belongs on
   a join gateway." **Owner:** Blathers.

4. **SHOULD-FIX** — `src/UmbracoPrism.Core.Tests/Workflow/Authoring/`
   — No test pins the `[Obsolete]` shim path on `AuthoredTransition`
   (`AuthoredTransition.cs:35-94`: `FromStage` / `ToStage` / `Action`).
   `AuthoredWorkflowSerializationTests.cs:296-297` *uses* the shim setters but
   only asserts round-trip JSON shape — it does not assert that a caller
   writing `FromStage = "x"` ends up with `Source == "x"` (and equivalent for
   `ToStage`/`Action`). Silent-migration risk if the shim ever stops mirroring.
   **Behavioural assertion to add:** "A caller that writes the legacy stage
   names on an AuthoredTransition gets the same value when it reads back the
   new gateway-first names." **Owner:** Blathers.

5. **SHOULD-FIX** — `src/UmbracoPrism.Client/src/workflow-editor/` —
   `prism-workflow-graph.ts:2724` still lists `Waiting` and `StatusTimeline` in
   the list-view kind `<select>`. No Playwright test catches that authoring
   them now produces a workflow that fails PROJ140 on save. **Behavioural
   assertion to add:** "Author cannot pick a retired stage type (Waiting,
   StatusTimeline) from the stage-type list." **Owner:** Tangy (after Isabelle
   removes them in 3b.1).

6. **WORTH-NOTING — sampled spec behavioural-fitness check**
   - `workflow-editor-gateways.spec.ts` — **behavioural ✅**. Asserts on visible
     names ("Review split", "Decision join"), `role="tab"` + `aria-selected`,
     and on the user-visible inspector field "Split gateway" / "Join gateway".
     Uses `data-prism-*` semantic anchors rather than CSS-derived structure.
     Skipped Slice 3b.1 test is annotated honestly.
   - `workflow-transition-editor.spec.ts` — **mixed**. Mouse-drag handle
     coordinates (lines 13-23) test interaction surface, not user goal; better
     phrased as "Author can connect a route from one stage to the next from
     the canvas". But the keyboard test (line 37+) genuinely proves a user
     journey and uses visible labels.
   - `workflow-editor-shell.spec.ts` — **behavioural ✅**. Switches workflows
     via `getByRole('combobox', { name: 'Select workflow' })` and asserts the
     editor title + visible stage cards change. Reads as user behaviour.

## Recommended new behavioural tests

(in plain product language, in priority order)

1. **"Author can pick a join gateway from a stage's outgoing route and the
   change is announced."** — proves the new `Arrive through` select + polite
   live region wire end-to-end on a real workflow story; covers Slice 3b's
   headline feature.

2. **"Screen reader user reading a transition in the outline hears the gateway
   name, not the gateway key."** — locks the SHOULD-FIX #1 above so it cannot
   regress quietly.

3. **"Author who tries to author waiting on a stage (legacy JSON payload only)
   is told it belongs on a join gateway."** — pins the bare-sentinel PROJ140
   path on the backend.

4. **"Caller using the legacy AuthoredTransition shim (`FromStage`, `ToStage`,
   `Action`) reads back the same values via `Source`, `Target`, `Trigger`."**
   — single xUnit fact, prevents silent shim drift.

5. **"Author editing a gateway's outgoing route can set the condition that
   fires it from the gateway inspector."** — **this is the Slice 3b.1
   done-condition test.** Today (a251bcd) the condition mode/value selects live
   only inside the transition panel (`prism-step-inspector.ts:660-694`). When
   3b.1 lands, condition editing should appear under the *gateway*'s outgoing-
   route panel so authoring a route never requires drilling into a transition
   chip. The test should select a split gateway, find its outgoing-route block,
   change the condition mode to "Guard expression", type a value, and assert
   both the gateway inspector reflects the change and the underlying transition
   condition is updated.

## Verdict on known-broken specs

- **`workflow-editor-validation.spec.ts`** — **SKIP** (with `test.skip` +
  comment `"Re-enable once Slice 5 ships [data-prism-canvas-health-hint] and
  [data-prism-open-validation]"`). Do not delete: the spec encodes the
  intended Slice 5 contract in product language and will be the right harness
  when the canvas health hint lands. Leaving it as a live `test(...)` is
  actively misleading — it implies coverage that does not exist.

- **`workflow-editor-help.spec.ts`** — **SKIP only the third test**
  (`"empty workflows show getting-started guidance and still expose help"`)
  with a comment pointing at Slice 5 graph copy. The first two tests
  (`"help button and F1 open the shortcut guide…"`, `"save and redo shortcuts
  stay discoverable…"`) are healthy and behavioural — KEEP them live.

## What I would NOT change

- **The `WorkflowSimulationServiceTests` pair (lines 11-95).** Two facts only,
  but they prove the exact gateway-first contract: a split is walked through
  invisibly to the next stage; a join pauses with `waiting-gateway`. Adding
  more parametric coverage here would be implementation-mirror noise.

- **The outline's flat-`<ol>`-with-sibling-gateway-row structure** (despite
  IMPROVE #3). A `role="treeitem"`/`aria-level` rewrite would buy little for a
  surface that is read-only navigation; the existing semantic list + visible
  meta ("Split gateway", "Join gateway") is sufficient for AA. Park as
  IMPROVE, do not block.

- **The `MultiLaneGatewayContractTests` skipped facts** for `#84 WaitingCopy`
  and deterministic release. They are honestly skip-flagged with the issue
  number — that is *exactly* the right shape for a contract-ahead-of-impl
  test. Resist the temptation to delete them just to make the suite "all
  green".

- **The polite-live-region-on-edit pattern in `prism-step-inspector.ts:1264`.**
  It does not announce *selection*, only *changes* — which looks like a gap
  but is actually correct: announcing every keyboard navigation event would
  overwhelm screen-reader users. Add a selection announcement at the editor
  host level (IMPROVE #4) instead of broadening the inspector announcer.
