# Workflow editor — visual regression test strategy

> **Audience:** Squad members (Tangy, Isabelle) and any future contributor
> adding or maintaining a visual test for the workflow editor canvas.
> **Last reviewed:** 2026-05-30 (Slice 7).

The canvas is the editor's main reading surface. Authors trust it to show, at
a glance, *which stage owns the work next*, *which gateway routes the work*,
and *which lane the work sits in*. If those things stop reading cleanly — text
crashing, nodes overlapping, scroll not working, arrows pointing at empty
space — the editor stops being trustworthy long before any unit test catches it.

This document describes what we test visually, what we explicitly **do not**
test, and how the suite is built so future contributors extend it without
re-introducing flake.

---

## What we test

Five concerns, taken straight from the user mandate (2026-05-30):

1. **Lane fit.** Every stage card and gateway node renders fully inside its
   declared lane's bounding box. No node escapes into a sibling lane or into
   the gutter between lanes.
2. **No-crash rendering.** Nodes in the same lane do not overlap each other.
   Stage and gateway labels are not clipped: their `scrollWidth` never exceeds
   their `clientWidth` up to a documented title length (currently **40 chars**
   for stage/gateway display names).
3. **Scroll behaviour.** When the workflow exceeds the canvas viewport,
   `.graph-canvas` becomes scrollable on the overflowing axis. Lane header
   strips stay sticky during vertical scroll. Scrollbars do not appear when
   they are not needed.
4. **Arrow legibility.** Every route's SVG endpoint lands on (within ±4 px of)
   the connector point of the node it claims to attach to. The handful of
   canonical layouts also have committed screenshot baselines so an author can
   eyeball a diff if a layout change touches arrow routing.
5. **Add / maintain ergonomics.** The named author flows — *add a stage*,
   *rename a stage without losing selection*, *reorder with Alt+ArrowUp /
   Alt+ArrowDown*, *add a gateway*, *jump to Definition tab and back* — work
   with stable focus and visible keyboard reach.

---

## What we explicitly do **not** test

Drawing the boundary keeps the suite cheap and the signal high.

- **Pixel-perfect styling.** Padding, border-radius, exact colours, exact
  font weights — owned by the component code and Storybook visual review,
  not by Playwright.
- **Font rendering / sub-pixel metrics.** macOS/Linux/CI render fonts
  differently; we use generous diff tolerance on the few screenshots we
  keep, and assert geometry in points rather than pixels everywhere else.
- **Cross-browser fidelity.** Chromium only. Firefox and WebKit visual
  parity are explicitly out of scope for this suite. If a layout regresses
  in another engine, that surfaces through Storybook visual review or a
  bug report — not here.
- **The Umbraco backoffice editor.** Permanently retired. The visual suite
  targets the runtime workflow editor (`<prism-workflow-editor>` and
  `<prism-workflow-graph>`) only.
- **Backend authoring.** Backend has its own xUnit suite. The visual suite
  never asserts backend behaviour.

---

## Tooling

| Concern | Mechanism | Why |
|---|---|---|
| Lane fit, no-overlap, text-fit, arrow endpoints, scroll behaviour, ergonomics | Playwright DOM assertions on measured geometry, computed style, and focus state | Deterministic. Fast. Survives stylistic refactors. |
| Canonical layout snapshots | Playwright `toHaveScreenshot()` against a small set of canonical scenarios | A human-readable diff for the small number of layouts where geometry alone misses the "is it readable?" judgement. |

We deliberately keep the screenshot count low (target **≤ 6**). Screenshots
flake more easily than DOM assertions, are slower to triage, and tempt
contributors into baking ugly layouts into the baseline. Every screenshot
spec sits next to at least one DOM-level spec covering the same scenario.

### Screenshot configuration

- `viewport: { width: 1440, height: 900 }` for every visual spec — pin the
  canvas size so geometry is reproducible.
- `animations: 'disabled'` on every `toHaveScreenshot()` call.
- `maxDiffPixelRatio: 0.02` (2 %). Generous enough to absorb sub-pixel
  font rendering between local macOS and CI Linux runners, tight enough to
  catch a node moving, a label crashing, or an arrow re-routing.
- Mask dynamic content (timestamps, generated IDs, zoom indicator) with
  `mask:` selectors. None of the canonical scenarios currently surface
  dynamic content on the canvas itself; if that changes, mask it.

### Baseline management

- **Baselines live next to the spec** under
  `src/UmbracoPrism.Client/tests/workflow-editor/__screenshots__/` (the
  `pathTemplate` is set in `playwright.config.ts`).
- **Update a baseline only via** `npx playwright test --update-snapshots`
  **on the spec that owns it**, after eyeballing the diff. *Never* update
  all baselines blindly with no review.
- **Reviewer:** the human reviewing the PR. Any baseline update must show
  the old and new image in the PR description (Playwright reporter handles
  this automatically when a snapshot fails). If the new baseline looks
  worse than the old one, the change is rejected; file a visual bug
  against Isabelle and revert the baseline change.
- **Committed baselines are part of the contract.** Treat a baseline diff
  like any other behavioural diff — it needs a reason.

### Flake budget

**Target: 0 %.** A flaky visual test is worse than no visual test, because
authors stop trusting the suite. The rule:

> If a visual test is flaky on its second run, fix the root cause (usually
> "I asserted geometry before the layout settled" or "the fixture has
> non-deterministic content") or convert it to a DOM assertion. Do not
> bump the tolerance and walk away.

Every screenshot spec must wait for a deterministic signal — usually
`await page.waitForLoadState('networkidle')` plus an
`expect(element).toBeVisible()` — before snapshotting. Arbitrary
`waitForTimeout` calls are forbidden in this suite.

---

## Canonical scenarios

Four canonical workflows, each exercising a distinct shape. The first three
already exist as Storybook stories; the fourth is added in Slice 7.

| Scenario | Story ID | What it proves |
|---|---|---|
| `SINGLE_LANE_LINEAR` | `workflow-editor-workflow-graph--workspace-canvas` | Baseline shape — one lane, several stages in sequence. The smallest possible "did anything regress in the simple case?" test. |
| `MULTI_LANE_FAN_OUT` | `workflow-editor-workflow-graph--gateway-representation` | Three lanes, a Split gateway branching applicant work into two parallel lanes, a Join gateway waiting for every branch. The canonical "real workflow". |
| `SAME_LANE_FAN_OUT` | `workflow-editor-workflow-graph--same-lane-fan-out` | A single lane forced to host two sibling Split gateways — exercises the Slice 5 lane-widening rule. |
| `LARGE_WORKFLOW` | `workflow-editor-workflow-graph--large-workflow` | Exceeds the canvas viewport on both axes — proves scroll behaviour and that the canvas does not bunch nodes when there is plenty of work to show. |
| `READ_ONLY_VIEWER` *(optional)* | `workflow-editor-workflow-graph--graph-read-only` | Declarative `<prism-workflow-graph read-only workflow-json="…">` — assert no authoring affordances render. |

Add a new canonical scenario only when an existing one fails to express the
concern. **Resist scenario sprawl.** A new scenario forces a new screenshot
baseline and another set of DOM assertions; the maintenance cost compounds.

---

## Spec layout

Each spec file is named for the concern it proves, not the scenario it
walks through. A single spec iterates the canonical scenarios in a
`describe.each`-style block where it makes sense.

```
tests/workflow-editor/
  workflow-canvas-lane-fit.spec.ts        — Concern 1
  workflow-canvas-no-overlap.spec.ts      — Concern 2 (geometry)
  workflow-canvas-text-fits.spec.ts       — Concern 2 (typography)
  workflow-canvas-scroll.spec.ts          — Concern 3
  workflow-canvas-arrows.spec.ts          — Concern 4 (DOM + screenshots)
  workflow-editor-ergonomics.spec.ts      — Concern 5
```

### Data-attribute contract the visual suite relies on

If you are refactoring `prism-workflow-graph.ts`, do **not** remove or
rename any of these hooks without updating the visual suite in the same
commit. They are the public surface the visual contract leans on.

| Attribute | On | Purpose |
|---|---|---|
| `data-prism-component="workflow-graph"` | root | "Did the graph render at all?" |
| `data-prism-mode="graph"` | root | Distinguishes graph vs list workspace mode. |
| `data-prism-read-only="true|false"` | root | Read-only viewer check. |
| `data-prism-lane-container=<laneKey>` | lane column | Lane bounding box for fit / overlap / arrow endpoint tests. |
| `data-prism-role-lane=<laneKey>` | lane column | (synonym, kept for backwards compat) |
| `data-prism-lane-header=<laneKey>` | lane header | Sticky-header scroll test. |
| `data-prism-stage-card=<stageKey>` | stage shell | Bounding box for fit / overlap / text-fit. |
| `data-prism-stage=<stageKey>` | stage button | Click target, label container. |
| `data-prism-gateway-node=<gatewayKey>` | gateway shell | Bounding box for fit / overlap / arrow endpoint. |
| `data-prism-gateway=<gatewayKey>` | gateway button | Click target, label container. |
| `data-prism-route-path=<key>` | SVG path | Endpoint assertion. |
| `data-prism-route-from=<key>` | SVG path | Endpoint mapping. |
| `data-prism-route-to=<key>` | SVG path | Endpoint mapping. |

---

## Running the suite

### Locally

```bash
cd src/UmbracoPrism.Client

# Run the visual suite only (fast, recommended pre-commit):
npx playwright test tests/workflow-editor/workflow-canvas-*.spec.ts \
                    tests/workflow-editor/workflow-editor-ergonomics.spec.ts \
                    --reporter=line

# Run a single concern:
npx playwright test tests/workflow-editor/workflow-canvas-lane-fit.spec.ts --reporter=line

# Update screenshot baselines after a deliberate, reviewed visual change:
npx playwright test tests/workflow-editor/workflow-canvas-arrows.spec.ts --update-snapshots
```

Storybook starts automatically via the Playwright `webServer` config — no
need to start it by hand.

### In CI

The suite runs as part of the standard Playwright workflow-editor job.
Snapshot diffs are uploaded as Playwright HTML report artefacts; reviewers
open the report to compare expected vs actual before approving an update.

### When you add a new spec

1. Justify it in terms of one of the five named concerns above. If it
   doesn't fit, write down why a sixth concern is now in scope.
2. Pin the viewport to `{ width: 1440, height: 900 }`.
3. Prefer DOM-level geometry assertions over screenshots. Only add a
   screenshot if a human eyeball is genuinely the only way to catch the
   regression.
4. Run the spec twice locally. If it isn't stable across two runs, fix
   the cause before opening a PR.
