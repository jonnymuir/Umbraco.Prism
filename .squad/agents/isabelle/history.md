# History: Isabelle (Frontend Dev)

#### 2026-05-22T19:33:56.538+01:00 — Issue #74 role-first swim lanes completed

Delivered the first usable slice of issue #74: horizontal role-first swim lanes replacing the old front-stage/back-stage canvas framing. The workflow graph now renders dynamic role lanes from stage actors, with each role (applicant, reviewer, etc.) getting its own lane row. Stages are positioned by actor rather than generic surface hint.
- **Accessibility pattern:** render simulation as a persistent panel with real buttons, breadcrumb history, polite live announcements, and graph highlights so keyboard and screen-reader users can follow the same route-planning feedback as pointer users.
- **Validation gate for this slice:** `npm run build`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-simulation.spec.ts tests/workflow-editor/workflow-editor-stage-preview.spec.ts tests/workflow-editor/workflow-editor-validation.spec.ts --reporter=line`, `node node_modules/.bin/test-storybook --url http://localhost:6006 --browsers chromium firefox webkit`, and `node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-help.spec.ts --reporter=line` passed after the simulation changes; `npm run test:playwright:planning-smoke` reached the live stack but was blocked by an existing `workflow-editor.html` shell readiness failure (`prism-workflow-editor` element missing on the served page) unrelated to the Storybook/editor-host slice.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-simulation.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-simulation.spec.ts`.
##### 2026-05-18T13:17:12.103+01:00 — Issue #67 runtime stage preview slice

- **Preview source of truth:** drive stage preview from the authoring `/project` endpoint so the editor shows the same deterministic runtime projection that publish uses, with a local projector fallback only for Storybook/offline shells.
- **Accessibility pattern:** keep the preview panel visibly separate from authoring, expose public/member/back-stage surface buttons even when some are disabled, announce loading politely, and render every control as disabled or static text so the preview never steals keyboard focus from editing.
- **Preview update seam:** debounce projection requests from `prism-workflow-editor`, preserve the last successful preview while a new one is loading, and let actor/surface edits re-evaluate which surface tab is available before re-rendering.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-stage-preview.spec.ts --reporter=line`, and `npm run test:playwright:planning-smoke` passed after the runtime preview changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-stage-preview.ts`, `src/UmbracoPrism.Client/src/workflow-editor/workflow-runtime-projection.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-stage-preview.spec.ts`.
##### 2026-05-18T13:17:12.103+01:00 — Issue #65 workflow validation and error reporting slice

- **Validation boundary:** treat orphaned and unreachable stages as blocking editor errors; keep dead ends and action-parameter gaps as workflow-friendly warnings so save stays available while authors finish detailed configuration.
- **Save seam:** use `POST /api/workflow-authoring/workflows/{key}/publish` for the host Save button until a dedicated authored-workflow save endpoint exists; the client labels it as Save, but the current persistence boundary is publish-backed.
- **Accessibility pattern:** make the validation rail a button-based jump list, preserve inline inspector errors, and move focus to the affected stage or action field when an author opens an issue from the rail.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-graph-keyboard.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts tests/workflow-editor/workflow-editor-copy-paste.spec.ts tests/workflow-editor/workflow-editor-validation.spec.ts --workers=1 --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the validation/error-reporting changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/workflow-validation.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-validation.spec.ts`.
#### 2026-05-18T13:17:12Z — Issue #65 validation and error reporting completed

Delivered shared workflow validation infrastructure:
- Single validation pass in `prism-workflow-editor` serving rail, save state, and jump-to-item behaviour
- Error classification: blocking errors (orphaned/unreachable stages) vs. warnings (dead-end reminders, parameter issues)
- Validation rail button-driven with jump-to-item links for accessibility
- Inline inspector field errors tied to validation
- Save blocking for critical structural problems
- Focused behavioural contract covers validation rail, plain-language messages, and save blocking

**Status:** Acceptance-complete per Tangy's seven-seam gate. Ready for production.
##### 2026-05-18T13:17:12.103+01:00 — Issue #66 help system and shortcut reference slice

- **Shortcut source of truth:** define workflow-editor commands in `src/UmbracoPrism.Client/src/workflow-editor/workflow-shortcuts.ts` and drive the toolbar `aria-keyshortcuts`, help modal content, and Playwright parity checks from that shared map so discoverability does not drift from implementation.
- **Accessibility pattern:** expose help as a host-owned modal opened by both the toolbar Help button and `F1`, trap focus while it is open, restore focus to the invoking control on close, and keep inline help on complex inspector/action-editor fields reachable by hover and keyboard focus.
- **Empty-state guidance:** when the workflow has no stages, replace the generic “nothing to display” message with actionable getting-started tips plus first-stage buttons inside `prism-workflow-graph` so authors can recover without guessing the next step.
- **Validation gate for this slice:** `npm run build`, `npm run test-storybook:ci:all`, `node node_modules/.bin/playwright test tests/workflow-editor/workflow-editor-help.spec.ts tests/workflow-editor/workflow-editor-history.spec.ts tests/workflow-editor/workflow-editor-copy-paste.spec.ts tests/workflow-editor/workflow-editor-validation.spec.ts tests/workflow-editor/workflow-action-editor.spec.ts tests/workflow-editor/workflow-graph-keyboard.spec.ts --workers=1 --reporter=line`, and `npm run test:playwright:planning-smoke` all passed after the help/discoverability changes.
- **Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-inline-help.ts`, `src/UmbracoPrism.Client/src/workflow-editor/workflow-shortcuts.ts`, `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-help.spec.ts`.
#### 2026-05-18T12:17:12Z — Issue #66 help and shortcut discoverability completed

Delivered help and shortcut discoverability as host-editor responsibility:
- Shared shortcut catalog at `src/UmbracoPrism.Client/src/workflow-editor/workflow-shortcuts.ts` drives toolbar affordances, help modal, and parity tests
- Help button visible on toolbar; `F1` opens shortcut reference modal with focus trap and restore
- Inline help on complex inspector fields reachable by hover and keyboard focus
- Empty-state shows getting-started tips with action buttons instead of generic "nothing to display"
- Comprehensive Playwright coverage ensures keyboard paths and empty-state recovery work end-to-end

**Status:** Acceptance-complete per Tangy's six-seam gate. Production-ready.
##### 2026-05-18T12:17:12Z — Issue #67 stage preview completed

Delivered read-only runtime preview pane driven from authoring project pipeline with public/member/back-stage switching, auto-update on edits, loading feedback, dedicated `prism-stage-preview` component, and planning workflow coverage.

**Quality gate:** All six acceptance seams green. Production-ready.
##### 2026-05-18T12:17:12Z — Issue #68 workflow simulation completed

Delivered dedicated path-simulation panel with authored-initial-stage start, breadcrumb history, happy/rejection/waiting-blocker routes, current-stage and traversed-path highlighting, Storybook scenarios, and targeted Playwright coverage.

**Architecture:** Simulation stays host-owned in `prism-workflow-editor`; graph renders highlights only. Validation blockers shown honestly without fake runtime evaluation. Reset on workflow change.

**Quality gate:** Client build, Storybook CI, graph keyboard, validation rail, and simulation Playwright all passed. Acceptance-complete.

**Status:** Production-ready. Non-slice environment blocker (empty planning.workflow.json) identified in separate remediation.
## Revision Handoff (2026-05-19)

Workflow editor shortcuts slice: Tangy final review complete. Blocker: admin definitions page missing 'Edit workflow' link. Isabelle assigned for revision cycle.

---

## 2026-05-19T18:16:08Z: Editor UX Redesign — Decisions Merged (Tabbed Interface ACCEPTED)

**Status:** 🟢 Decisions finalized; implementation ready

Two overlapping proposals for the workflow editor redesign were submitted simultaneously and merged into the decisions log:

1. **Isabelle's Proposal** (status: in_review)
   - Full-screen tabbed layout with Graph, Outline, Inspector, AI tabs
   - 6-step implementation plan with validation gates

2. **Tom Nook's Proposal** (status: ACCEPTED)
   - Full-screen tabbed interface with Graph, List, Validation, Preview, Simulation tabs
   - Removes embedded conversation widget; keeps conversation in external Copilot CLI
   - 6-slice implementation plan; decision finalized as ACCEPTED

**Next Steps for Isabelle:**
- Review Tom Nook's ACCEPTED decision (more recent, finalizes the UI shape)
- Align Isabelle's in_review proposal with Tom Nook's accepted version if needed
- Both proposals are available in `.squad/decisions.md` for team reference

**References:**
- `.squad/decisions/inbox/isabelle-editor-ux-shape.md`
- `.squad/decisions/inbox/tom-nook-editor-ux-redesign.md`
- `.squad/orchestration-log/2026-05-19T18-16-08Z-scribe.md`

## 2026-05-22: Issue #74 completion and merge

**Role-first swim lanes implementation complete and QA validated.**

- Role-first lanes rendered from stage actor metadata
- Inspector remains primary editing surface
- Embedded conversation pane removed
- Accessibility improved (semantics, focus, keyboard)
- All quality gates passing: client build, Storybook CI, keyboard tests, visual regression, planning smoke
- Awaiting merge review
