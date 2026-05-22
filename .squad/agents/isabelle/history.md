# History: Isabelle (Frontend Dev)

#### 2026-05-22T19:33:56.538+01:00 — Issue #74 role-first swim lanes completed

Delivered the first usable slice of issue #74: horizontal role-first swim lanes replacing the old front-stage/back-stage canvas framing. The workflow graph now renders dynamic role lanes from stage actors, with each role (applicant, reviewer, etc.) getting its own lane row. Stages are positioned by actor rather than generic surface hint.

**Key implementation changes:**
- Graph canvas renders role lanes dynamically from stage actor data with lane headers showing role label, stage count, and description
- Stages positioned in their role's lane with cross-lane transition routing that respects role boundaries
- Single 'Add stage' button (context-aware) instead of separate front/back buttons
- Role lanes are focusable sections with semantic labels and descriptions for keyboard + screen reader access
- Embedded conversation pane removed per locked #74 direction; inspector remains persistent on the right
- Stage aria-labels reference the role ("Declaration, Applicant role") not generic "front stage"
- Graph workspace labeled as "Role-first workflow editor workspace"

**Accessibility improvements:**
- Role lanes exposed as focusable `<section>` elements with `aria-labelledby` and `aria-describedby`
- Lane descriptions announced politely when focused ("Applicant lane. 2 stages. Public-facing stages and handoffs.")
- Visual focus indicators on all lane controls with 3px WCAG-compliant outline
- Keyboard Tab navigation through lanes, stages, transitions preserves role context

**Supporting updates:**
- Stories updated to validate role lane presence and test conversation pane absence
- Walkthrough test updated to exercise role-lane structure and help surface (not embedded chat)
- Visual baselines refreshed for swim-lane layout in both canvas and list modes
- Design doc `01-authoring-ux.md` updated to reflect slice framing and inspector-first editing

**Validation:**
- Client build: ✅ Passes
- Storybook CI (Chromium/Firefox/WebKit + axe): ✅ 312 tests pass, no accessibility violations
- Workflow graph keyboard spec: ✅ 4 tests pass (navigation, create stage, delete confirmation, selection)
- Planning smoke walkthrough: ✅ 1 test pass (1.1m full stack readiness)

**Architecture patterns confirmed:**
- Canvas owns structural layout (lanes, stage placement, transition routing)
- Inspector remains the persistent detail editing surface for stage/action properties
- List mode preserved as accessible structural fallback while canvas changes
- Validation, preview, simulation, and help remain as supporting surfaces around the role-first workspace

**Key file paths:** `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.stories.ts`, `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`, `docs/design/workflow-editor-v1/01-authoring-ux.md`, `.squad/decisions/inbox/isabelle-issue74-slice.md`.

**Status:** Issue #74 first usable slice complete and committed to `squad/74-role-first-swim-lanes` branch. Ready for Tangy's QA validation and PR review.

#### 2026-05-21T21:54:07.868+01:00 — WebKit editor-host story stabilization

- Diagnosed the remaining PR #75 Storybook WebKit blocker as story harness timing, not product state: the `Stage Selected` editor-host story was synthesizing a `stage-selected` event during render and then asserting after a fixed delay, which left WebKit free to observe the preview label before the real selection/render cycle had settled.
- Replaced that synthetic selection path with the honest user path inside the story play function: click the graph’s `Declaration` stage button from the component shadow root and wait for the preview label to resolve instead of sleeping for 300 ms.
- Re-validated the directly affected lane locally with `cd src/UmbracoPrism.Client && npm run build` and `cd src/UmbracoPrism.Client && npx test-storybook --url http://127.0.0.1:6006 --browsers webkit --verbose`; the full WebKit Storybook suite passed after the story fix.
- Key files: `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.stories.ts`, `.squad/decisions/inbox/isabelle-webkit-story-fix.md`.

#### 2026-05-21T21:54:07.868+01:00 — Linux workflow graph rerun follow-up

- Reproduced the still-red `storybook-tests` workflow-graph visual lane against current PR #75 branch state, then rechecked it in the closest available Linux/CI mode using the existing Debian-based Node devcontainer image with Playwright Chromium.
- Confirmed the remaining mismatch was still text rasterization drift: the graph and list screenshots were stable inside Linux, but the committed baselines were still coming from a different renderer despite the earlier fallback stack fix.
- Hardened the visual harness by vendoring an embedded Inter test font into `workflow-graph-visual.spec.ts`, forcing the graph shadow root to use that font for all controls and content, and keeping the Chromium screenshot flags pinned to grayscale/sRGB output.
- Refreshed the two workflow graph baselines from the Linux repro and re-validated `npm run test-storybook:ci:all` plus the Linux Playwright visual spec pass after the refresh.
- Key files: `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-visual.spec.ts`, `src/UmbracoPrism.Client/tests/assets/fonts/inter-400.ttf`, `src/UmbracoPrism.Client/tests/assets/fonts/inter-600.ttf`, `src/UmbracoPrism.Client/tests/assets/fonts/inter-700.ttf`, `.squad/decisions/inbox/isabelle-storybook-rerun-fix.md`.

#### 2026-05-21T21:54:07.868+01:00 — Linux workflow graph visual baseline follow-up

- Reproduced the PR #75 `storybook-tests` visual failure in a Linux container against commit `20cf8b3`, matching the GitHub Actions pixel drift on the workflow graph canvas and list-mode screenshots.
- Confirmed the rendered UI was unchanged and the remaining mismatch was baseline-only after the earlier harness stabilization, then replaced the two workflow graph baselines with Linux-captured images from the CI-like repro.
- Re-validated the targeted lane in the closest available CI mode with `CI=1 npm run test:playwright:workflow-graph-visual` inside the project Linux devcontainer image; the visual spec passed there after the baseline refresh.
- Key files: `src/UmbracoPrism.Client/tests/__screenshots__/workflow-editor/workflow-graph-visual.spec.ts/workflow-graph-workspace-canvas.png`, `src/UmbracoPrism.Client/tests/__screenshots__/workflow-editor/workflow-graph-visual.spec.ts/workflow-graph-workspace-list-mode.png`, `.squad/decisions/inbox/isabelle-linux-visual-fix.md`.

#### 2026-05-21T21:54:07.868+01:00 — Workflow graph visual regression fix

- Diagnosed PR #75 `storybook-tests` failure as a cross-platform visual baseline mismatch in `workflow-graph-visual.spec.ts`: GitHub Actions Linux reported 8,174 differing pixels for the canvas view and 19,017 for list mode while the branch baselines had been recorded on macOS.
- Stabilized the screenshot harness instead of changing product UI: the visual spec now launches Chromium with `--font-render-hinting=none` and pins the graph host to an Arial/Helvetica fallback stack through `--uui-font-family` before capturing screenshots.
- Re-recorded the workflow graph baselines under `src/UmbracoPrism.Client/tests/__screenshots__/workflow-editor/workflow-graph-visual.spec.ts/` to match the stabilized harness output.
- Validation: `npm run test-storybook:ci:all` and `CI=1 npm run test:playwright:workflow-graph-visual` both passed after the fix.

**Status:** Visual lane stabilized for CI and ready for re-run.

#### 2026-05-18T22:14:30.041+01:00 — Issue #72 E2E Test Implementation

Implemented 4 missing behavioural tests for planning workflow complete E2E coverage, converting all `.skip()` placeholders to working tests:

1. **Complete multi-stage flow** — Declaration → Application Form → Check Answers → Submitted with full form interaction and validation
2. **Validation enforcement** — Required field blocking with graceful multi-mechanism detection (error summary, field errors, disabled buttons)
3. **Member continuation** — Partial completion → dashboard navigation → resume with preserved state
4. **Back-stage review** — Submission visible in MockBusinessApp admin interface at `/admin/workflow`, infrastructure validated

**Key decisions:**
- Tests use real Playwright actions (no mocks) for honest behavioural coverage
- Graceful handling of current workflow scope: planning workflow ends at "submitted" (terminal) without explicit caseworker review/rejection stages yet
- Back-stage test validates infrastructure readiness (admin UI exists, shows instances) while documenting that full rejection/re-submission requires workflow extension
- All tests include screenshot steps for walkthrough documentation

**Validation:**
- Client build: ✅ Passes
- Backend tests: ✅ 349/349 workflow tests pass
- No skipped tests: ✅ Verified 0 `.skip()` in test file

**Files:**
- `src/UmbracoPrism.Client/tests/walkthroughs/planning-workflow-complete.walkthrough.spec.ts` — Implemented 4 tests
- `.squad/decisions/inbox/isabelle-issue-72-tests.md` — Decision document

**Status:** All #72 acceptance criteria now have executable test coverage. Ready for Tangy's re-validation.

---

# History: Isabelle (Frontend Dev)


### Quality Gate

All six gates pass:
1. ✅ Authoring-focused .NET workflow tests
2. ✅ Client build
3. ✅ Storybook CI across browsers with axe
4. ✅ Existing workflow graph/list keyboard contract
5. ✅ Dedicated Playwright stage-editor behavioural contract
6. ✅ Live planning workflow smoke

**Status:** Green and acceptance-complete per Tangy's recheck.
##### 2026-05-18T13:17:12.103+01:00 — Issue #69 browser-surface green fix

- **Readiness contract:** reflect `data-prism-workflow-loaded` on the `<prism-workflow-editor>` host once the authored workflow is loaded so localhost smoke checks do not depend on piercing shadow DOM.
- **Hosted shell polish:** the reference host page should carry its own inline favicon to keep the live browser console free of avoidable 404 noise.
- **Walkthrough resilience:** when host chrome overlaps graph controls, prefer the editor's keyboard contract (`focus()` + key activation) over pointer clicks in the planning walkthrough so the smoke exercises the accessible path instead of a brittle hit target.
- **Validation gate for this fix:** `dotnet test src/UmbracoPrism.Core.Tests/ --filter "FullyQualifiedName~Workflow.Authoring" --nologo`, `npm run build`, and `npm run test:playwright:planning-smoke` passed after the browser-surface fixes.
- **Key file paths:** `src/UmbracoPrism.Client/workflow-editor.html`, `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-editor.ts`, `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`, `.squad/decisions/inbox/isabelle-issue-69-green-fix.md`.
#### 2026-05-18T19:41:25Z — Issue #69 browser-surface blockers resolved

Completed the remaining surface-level fixes for issue #69:
- Removed `/favicon.ico` 404 from host page by serving inline data-URL favicon.
- Reflected `data-prism-workflow-loaded` readiness attribute on `<prism-workflow-editor>` host element (not shadow-only).
- Updated localhost smoke to use shadow-aware Playwright locators for readiness check.
- Validated browser console cleanliness on live reference host.
- All five-seam gate passed: .NET tests (77/77), build, Storybook CI, localhost probe, save round-trip.

**Status:** Issue #69 acceptance-complete and production-ready. Browser surface blockers resolved.
##### 2026-05-18T13:17:12.103+01:00 — Issue #68 workflow path simulation slice

- **Simulation ownership:** keep path simulation state in `prism-workflow-editor` so the graph highlight, simulation panel, and shared validation issues all stay in sync from one authored-workflow source of truth.
- **Simulation boundary:** start from `initialStageKey`, stop automatically at waiting/terminal/dead-end stages, and treat route-specific blocking validation issues as disabled transition buttons while still showing condition and role-guard copy as author guidance rather than pretending to execute runtime rules.
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
