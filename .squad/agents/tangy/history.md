# Tangy — History

QA/Tester specializing in end-to-end validation and quality assurance.

**Current Focus:**
- Issue #57: End-to-end quality validation (COMPLETED)
- Backend quality gate confirmation
- Blocker identification and resolution verification

**Latest:** Green end-to-end validation on issue #57 (2026-05-18T12:17:12Z)

## Learnings

### 2026-05-18T13:17:12.103+01:00 — Issue #61 recheck

- Re-ran the #61 transition-editor quality gate on the latest slice and it is green for issue scope: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, the graph keyboard contract, the dedicated transition-editor Playwright contract, and the live planning workflow smoke all passed.
- The previously missing acceptance behaviours are now present in the shipped surface: graph drag and keyboard creation open a labelled transition dialog, list mode exposes an Add transition action, the inspector edits target/label/condition/role guard, transition delete works cleanly, and workspace warnings surface unreachable and dead-end routing problems.
- #61 is acceptance-complete. Dedicated transition connectivity coverage is in place through Storybook interaction stories plus the focused Playwright transition-editor contract, with manual re-probing confirming the list-mode create path and warning banner behaviour.

### 2026-05-18T13:17:12.103+01:00 — Issue #60 recheck

- Re-ran the #60 quality gate on the latest slice and it is green: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, the focused workflow graph/stage workspace Playwright contract, and the live planning workflow smoke all passed.
- The previously missing acceptance items are now present in the shipped surface: accessible create/delete dialogs, an editable stage inspector for title/key/description/actor/type, catalog-backed stage actions with reorder/remove affordances, and authored description/action data flowing through fixtures plus the authoring API.
- #60 is now acceptance-complete. Coverage is split sensibly between Storybook behavioural stories for inspector/action editing and Playwright contracts for workspace creation, deletion, selection, and live-shell smoke.

### 2026-05-18T13:17:12.103+01:00 — Issue #60 stage editor quality gate

- Re-ran the current #60 baseline and it is green on the latest worktree: authoring-focused .NET workflow tests, client build, Storybook CI across browsers with axe, the workflow graph/list keyboard contract, and the live planning workflow smoke all passed.
- Minimum keep-green coverage for the stage editor slice should add one missing behavioural contract on top of that baseline: a focused Playwright stage-editor spec that exercises create-stage validation, inspector editing, action add/reorder flows, delete confirmation, and keyboard-only paths.
- The current implementation is still short of #60 acceptance: stage creation is template-first rather than dialog-driven, the inspector is read-only, the TypeScript workflow model drops stage `description` and `actions`, the client does not consume the action catalog endpoint, delete is immediate with no affected-transition confirmation, and there is no dedicated stage-editor test yet.

### 2026-05-18T13:17:12.103+01:00 — Issue #59 recheck

- Re-running the issue #59 list workspace gate is green on the latest slice: client build, Storybook CI across browsers with axe, the focused Playwright workflow workspace contract, and the live planning workflow smoke all passed.
- The previously missing acceptance items are now present in the shipped surface: list mode is a semantic table with key/title/actor/type columns, inline row editing, front/back-stage filters, add/insert/delete controls, keyboard plus drag reordering hooks, polite live announcements, and row click handoff into the inspector.
- #59 is acceptance-complete because the list workspace mutates and emits the shared workflow model used by the host editor rather than maintaining a separate list-only state.

### 2026-05-18T13:17:12.103+01:00 — Issue #59 list workspace quality gate

- Minimum keep-green gate for the list/table workspace slice is: client build, Storybook CI across browsers with axe, a dedicated Playwright contract for list-mode keyboard and row-editing behaviour, and the live planning workflow smoke covering list-view entry from the real shell.
- The current worktree is green on build, Storybook CI, the existing workflow-graph keyboard contract, and the planning smoke, but #59 is not acceptance-complete: list mode is still a card/listbox fallback rather than a table-like editing workspace, click selects a row without opening the inspector, inline editing and front/back filtering are absent, and there is no reorder contract.
- Shared-model integrity is already in place because the list mode mutates and emits the same `workflow-updated` workflow object used by the graph/editor host, so the remaining gap is behaviour depth rather than model divergence.

### 2026-05-18T13:17:12.103+01:00 — Issue #58 graph workspace quality gate

- Minimum keep-green gate for the graph workspace slice is: client build, Storybook interaction/a11y run, dedicated keyboard contract spec for the graph, and the live planning workflow smoke.
- Current worktree passes that gate, but #58 is still not acceptance-complete: the graph does not render visual transition edges, transition selection/drag creation are absent, add-stage/context menus are absent, and Storybook coverage is interaction/a11y only rather than visual regression.
- Front-stage/back-stage styling should be treated as a data contract, not just CSS. The component has a dormant `.stage-kind-backstage` rule, but the authored stage model currently provides no placement field to drive it.

### 2026-05-18T13:17:12.103+01:00 — Issue #58 recheck

- Re-running the issue #58 UI gate is green on the latest slice: client build, Storybook CI, the dedicated workflow-graph Playwright contract, and the live planning workflow smoke all passed.
- The previously missing interaction items are now covered in implementation and tests: routed transition edges render, stages and transitions can be selected, add/delete/copy context actions work, drag-to-create transitions is exercised, zoom/fit controls respond, and double-click hands off to the inspector.
- #58 is still not acceptance-complete because the Storybook coverage is still interaction/a11y only; there is no visual regression assertion or screenshot baseline protecting the graph workspace.

### 2026-05-18T13:17:12.103+01:00 — Issue #58 visual regression close-out

- The missing acceptance blocker for #58 is best covered as a dedicated Playwright screenshot contract against the Storybook iframe story, not by overloading Storybook's interaction/a11y runner.
- Stable editor-surface baselines need a fixed viewport plus committed screenshots under `src/UmbracoPrism.Client/tests/__screenshots__/`, with Playwright configured to avoid platform-suffixed snapshot paths so one baseline can serve CI.
- The graph workspace slice is now green with build, Storybook CI (all browsers + WCAG), the new visual regression spec, the existing keyboard contract, and the live planning workflow smoke.

### 2026-05-18T13:17:12.103+01:00 — Issue #61 transition editor quality gate

- Minimum honest keep-green gate for the transition slice is: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, the existing workflow graph keyboard contract, a dedicated transition-editor Playwright contract, and the live planning workflow smoke.
- The current worktree is green on build, authoring tests, Storybook CI, the graph keyboard contract, and the planning smoke, so the plumbing is healthy enough to review the slice without blaming unrelated regressions.
- #61 is not acceptance-complete yet: graph drag creates a transition immediately with a default label instead of prompting, list mode has no transition-create affordance, the transition inspector is read-only, retarget/guard/action editing is absent, unreachable-stage validation is not implemented, and there is no dedicated transition connectivity spec protecting post-edit graph integrity.

## 2026-05-18: Issue #61 Quality Gate & Acceptance

**Outcome:** ✅ Green and acceptance-complete  

### Initial Pass: Quality Gate Confirmation

Established minimum honest gate for issue #61 (6 seams):
1. `cd src/UmbracoPrism.Client && npm run build` — TypeScript/Lit integration
2. `cd src/UmbracoPrism.Core.Tests && dotnet test --filter "FullyQualifiedName~Workflow.Authoring"` — Backend model contract
3. `cd src/UmbracoPrism.Client && npm run test-storybook:ci:all` — Accessibility via axe across browsers
4. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-graph-keyboard.spec.ts` — Graph keyboard contract
5. `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test tests/workflow-editor/workflow-transition-editor.spec.ts` — Dedicated transition contract
6. `cd src/UmbracoPrism.Client && npm run test:playwright:planning-smoke` — Live planning workflow smoke

Found acceptance items missing in initial state (no label prompt, no list create affordance, no editable inspector, etc.).

### Second Pass: Recheck

Confirmed all acceptance items complete:
- Graph drag-to-connect and keyboard handles open labelled transition modal
- List mode exposes explicit Add transition row action
- Inspector edits target, label/action, condition, role guard inline
- Transition delete works cleanly from inspector
- Workspace warnings surface unreachable and dead-end routing problems
- All six gates pass end to end

**Result:** Issue #61 is green and acceptance-complete per Isabelle's delivery.

---

## 2026-05-18: Issue #60 Quality Gate & Acceptance

**Outcome:** ✅ Green and acceptance-complete  

### Initial Pass: Quality Gate Confirmation

Established minimum honest gate for issue #60 (6 seams):
1. `npm run build` — TypeScript/Lit integration
2. Authoring-focused .NET workflow tests — Backend model contract
3. `npm run test-storybook:ci:all` — Accessibility via axe across browsers
4. Existing workflow graph/list keyboard contract — Graph contract maintained
5. Dedicated Playwright stage-editor behavioural contract — Comprehensive coverage
6. Live planning workflow smoke — End-to-end shell scenario

Found acceptance items missing in initial state (dialog-driven create, editable inspector, etc.).

### Second Pass: Recheck

Confirmed all acceptance items complete:
- Create dialog validates duplicate keys with seeded focus
- Delete confirms and warns about affected transitions
- Inspector fields (title/key/description/actor/type) editable inline
- Actions reorderable via keyboard and drag
- All keyboard accessibility flows tested
- Live announcements for screen readers
- All six gates pass end to end

**Result:** Issue #60 is green and acceptance-complete per Isabelle's delivery.

---

## 2026-05-18: Issue #58 Quality Gate and Acceptance Completion

**Scope:** Quality gate for issue #58 graph workspace, visual regression coverage, acceptance verification.  
**Outcome:** Identified missing acceptance items, confirmed interaction work green, added visual regression coverage with committed baselines and CI wiring. Issue #58 now acceptance-complete.

### Three-Pass Approach

1. **Quality gate definition** — Four-part UI gate: client build, Storybook interaction/a11y, dedicated keyboard contract spec, live planning workflow smoke.
2. **Recheck verification** — Confirmed all previously missing behaviours now implemented and tested: transition edges, selection, context actions, drag-to-create, zoom/fit, inspector handoff.
3. **Visual regression closure** — Playwright screenshot contract against Storybook iframe stories, committed baselines under `tests/__screenshots__/`, CI wired into Storybook test job.

### Key Findings

- Previous blocker was missing visual regression assertion, not implementation gaps.
- Four-part gate now all-green: build, Storybook (all browsers), keyboard contract, live smoke, and new visual regression spec.
- Baselines stable with fixed viewport and committed screenshots, avoiding platform-suffixed snapshot paths.

### Acceptance Status

✅ Issue #58 is now acceptance-complete. All quality criteria met.

## 2026-05-18: Issue #59 Quality Gate & Acceptance

**Outcome:** ✅ Green and acceptance-complete  

### Initial Pass: Quality Gate Confirmation

Established minimum honest gate for issue #59 (4 seams):
1. `npm run build` — TypeScript/Lit integration
2. `npm run test-storybook:ci:all` — Accessibility via axe across browsers
3. Focused Playwright contract for keyboard/list behaviour
4. Live planning workflow smoke test

Found acceptance items missing in initial state (list not yet table, no inspector click, etc.).

### Second Pass: Recheck

Confirmed all acceptance items complete:
- Semantic table rows with inline editing
- Front/back-stage filters
- Add/insert/delete and reorder (drag and keyboard)
- Live announcements
- Row click opens inspector
- All four gates pass end to end

**Result:** Issue #59 is green and acceptance-complete per Isabelle's delivery.

---

## Issue #60: Stage editor quality gate

**Date:** 2026-05-18T12:17:12Z  
**Outcome:** ✅ Green and acceptance-complete

Quality-gated issue #60 stage editor slice with comprehensive verification:

### First Pass: Gap Identification

Tracked missing acceptance items:
- Dialog-driven stage creation and deletion
- Editable inspector with description field
- Action catalog integration
- Delete confirmation showing affected transitions
- Keyboard-only editing flows

### Verification Gate

1. ✅ Authoring-focused .NET workflow tests — Pass
2. ✅ Client build — Green
3. ✅ Storybook CI across browsers with axe — All pass
4. ✅ Existing workflow graph/list keyboard contract — Maintained
5. ✅ Dedicated Playwright stage-editor behavioural contract — Comprehensive coverage
6. ✅ Live planning workflow smoke — Passing

### Second Pass: Recheck and Acceptance

Confirmed Isabelle's delivery:
- Create dialog validates duplicate keys
- Delete confirms and warns about affected transitions
- Inspector fields (title/key/description/actor/type) editable inline
- Actions reorderable via keyboard and drag
- All keyboard accessibility flows tested
- Live announcements for screen readers

**Result:** Issue #60 is green and acceptance-complete. Workflow editor stage editing slice ready for production.
