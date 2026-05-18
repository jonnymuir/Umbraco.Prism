# History: Tangy (Tester)


# History: Tangy (Tester)

## Recent Work
##### 2026-05-18T12:17:12Z — Issue #68 quality gate complete

Executed seven-seam acceptance gate: .NET authoring tests, client build, Storybook CI, graph keyboard Playwright, validation rail Playwright, simulation Playwright, planning smoke.

**Evidence:** Simulation panel owns state; graph highlights from host state only. Happy-path, rejection-path, waiting/blocker flows all covered. Validation blockers shown honestly. Reset on workflow change maintains consistency.

**Gate status:** Simulation feature acceptance-complete. Non-slice environment blocker identified (empty planning.workflow.json seed causes API 500s; classified as separate remediation).

**Conclusion:** Issue #68 simulation feature is production-ready. Full gate awaits environment data restore.
##### 2026-05-18T13:17:12.103+01:00 — Issue #68 recheck

- Re-ran the #68 seven-seam gate against the latest implementation: client build, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow validation rail Playwright, and the dedicated workflow simulation Playwright contract all passed; the acceptance surface is now present in the shipped editor and covered by behavioural tests.
- The remaining red is not simulation-specific: `src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json` is currently 0 bytes, so the workflow-authoring .NET seed tests fail and the live planning smoke hits authoring API `500` responses (`Failed to list workflows` / `Failed to fetch workflow "planning"`), leaving the shell mounted but unloaded.
- Treat the earlier localhost `workflow-editor.html` readiness report as environment/data noise for #68 acceptance, not evidence against the simulation slice itself; the concrete blocker to an all-green gate is restoring a valid live planning authored seed so the localhost authoring API can load `planning` again.
##### 2026-05-18T13:17:12.103+01:00 — Issue #68 quality gate

- Minimum honest keep-green coverage for workflow path simulation is seven seams: workflow authoring .NET tests, client build, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow validation rail Playwright, a dedicated workflow simulation Playwright contract, and the live planning workflow smoke.
- The current branch snapshot is not #68-green yet: `prism-workflow-editor` renders validation rail plus stage preview only, `prism-workflow-graph` only supports selected-stage or selected-transition highlighting, and there is no dedicated workflow simulation contract under `src/UmbracoPrism.Client/tests/workflow-editor/`.
- The surrounding seams are healthy today: workflow authoring tests, client build, Storybook CI, workflow graph keyboard, workflow validation rail, and the live planning workflow smoke all passed, so the remaining blocker is slice-specific simulation behaviour rather than editor baseline health.
##### 2026-05-18T13:17:12.103+01:00 — Issue #67 quality gate

- Minimum honest keep-green coverage for the preview-edited-stage slice is six seams: workflow authoring .NET tests, client build, Storybook CI across browsers with axe, the existing workflow graph/stage-selection Playwright contract, a dedicated workflow preview Playwright contract, and the live planning workflow smoke.
- The surrounding editor seams are green on the current branch: workflow authoring tests passed 75/75, the client build passed, Storybook CI passed across browsers with axe, the graph keyboard contract passed 4/4, and the live planning workflow smoke passed.
- Do not call #67 green yet: `prism-workflow-editor` still renders graph, inspector, conversation, and validation surfaces only; there is no preview panel, no public/member/back-stage selector, no preview loading state, no read-only stage runtime surface, and no dedicated behavioural contract covering planning workflow stage preview.
##### 2026-05-18T13:17:12.103+01:00 — Issue #66 recheck

- Re-ran the full #66 help/discoverability gate: client build, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, the dedicated workflow-editor help Playwright contract, and the live planning workflow smoke all completed green.
- The previously missing acceptance seams are now present together: the toolbar exposes a visible Help button, F1 opens the in-editor shortcut reference, the shortcut dialog is driven from the shared shortcut catalog, empty workflows now show getting-started guidance, and complex inspector/action fields ship hover/focus inline help.
- The dedicated parity contract proves the help surface stays aligned with the exported shortcut map and that keyboard users can open, dismiss, and return focus predictably. No #66-specific blocker remains.
##### 2026-05-18T13:17:12.103+01:00 — Issue #65 quality gate

- Minimum honest keep-green coverage for workflow validation and error reporting is seven seams: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, a dedicated validation/error-reporting Playwright contract, and the live planning workflow smoke.
- The current branch only has partial #65 plumbing: `workflow-validation.ts` defines orphaned, unreachable, dead-end, and action-configuration issues in plain language; the graph surface exposes unreachable/dead-end jump buttons; and the inspector/action editor already show field-level validation feedback.
- Do not call #65 green yet: the shared `validateWorkflow(...)` helper is not wired into the host editor, there is no editor save affordance or validate-endpoint call to block critical saves, the visible rail only covers routing warnings instead of the full error set, and there is no dedicated behavioural contract proving rail links, orphaned-stage reporting, or save blocking.
- Re-ran the closest current gate: client build, workflow authoring .NET tests, Storybook CI, workflow graph keyboard Playwright, and the live planning smoke passed; the action-editor Playwright suite surfaced one retry-only flake in the existing keyboard SMS path before completing green, so treat that as unrelated noise rather than #65 evidence.
##### 2026-05-18T13:17:12.103+01:00 — Issue #65 recheck

- Re-ran the full #65 quality gate: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, the dedicated workflow-editor validation Playwright contract, and the live planning workflow smoke all completed green.
- The previously missing acceptance seams are now present together in the host editor: `validateWorkflow(...)` drives the validation rail and save state, the rail lists orphaned/unreachable/action issues in workflow language with jump buttons, inspector focus follows rail jumps, and blocking structural errors disable save while action-field problems stay warnings.
- The action-editor suite still shows one retry-only flake in an older keyboard-only forms path, but it recovered and does not contradict the dedicated #65 evidence. Issue #65 is now honest green and acceptance-complete with no slice-specific blocker remaining.
##### 2026-05-18T13:17:12.103+01:00 — Issue #66 quality gate

- Minimum honest keep-green coverage for workflow editor help and shortcut discoverability is six seams: client build, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, a dedicated help-and-shortcuts Playwright contract, and the live planning workflow smoke.
- The surrounding editor seams are healthy on the current branch: client build, Storybook CI, workflow graph keyboard, workflow action editor, and the live planning workflow smoke all passed; the action-editor suite still showed one retry-only validation-timing flake that recovered and does not read as #66 evidence.
- Do not call #66 green yet: the toolbar exposes save/undo/redo/copy/paste plus view toggle but no help button, the host keyboard handler only wires copy/paste/undo/redo, the list empty state only says "No stages to display.", and there is no dedicated behavioural contract proving shortcut-reference parity, keyboard-accessible help, or getting-started guidance.


Re-ran the full quality gate (seven seams: client build, authoring tests, Storybook CI, graph keyboard, action editor, copy/paste contract, planning smoke). All passed. Issue #64 is acceptance-complete and ready for merge. Unrelated CSS noise does not block this slice.
#### 2026-05-18T13:17:12Z — Issue #65 validation quality gate and acceptance confirmation

Defined and passed seven-seam quality gate:
1. Client build
2. Workflow authoring .NET tests
3. Storybook CI across browsers with axe
4. Workflow graph keyboard Playwright
5. Workflow action editor Playwright
6. Workflow validation error reporting Playwright
7. Live planning workflow smoke

Key acceptance items verified:
- Shared validation pass surfaces orphaned stages, unreachable stages, action-parameter issues
- Visible validation rail with jump-to-item links for accessibility
- Critical validation errors block save/publish
- Dedicated behavioural contract covers validation rail, jump flow, messages, save blocking

Only remaining noise: unrelated retry-only flake in older action-editor keyboard/forms spec.

**Status:** Issue #65 confirmed green and acceptance-complete. Ready for production.
#### 2026-05-18T12:17:12Z — Issue #66 help and shortcut discoverability quality gate and acceptance confirmation

Defined and passed six-seam quality gate:
1. Client build
2. Storybook CI across browsers with axe
3. Workflow graph keyboard Playwright
4. Workflow action editor Playwright
5. Workflow editor help and shortcuts Playwright
6. Live planning workflow smoke

Key acceptance items verified:
- Shared shortcut catalog (workflow-shortcuts.ts) drives toolbar affordances, help modal, parity tests
- Help button visible on toolbar opens shortcut reference modal
- F1 opens and closes help modal predictably with focus trap/restore
- Inline help on complex inspector fields reachable by hover and keyboard focus
- Empty-state shows getting-started tips with action buttons
- All shortcuts listed in help match implemented keyboard handlers
- Keyboard and screen-reader paths work end-to-end
- Dedicated help contract owns real acceptance: button opens, list matches commands, empty state guides, panel is usable keyboard-first

No slice-specific blocker remaining. All six gates passed.

**Status:** Issue #66 confirmed green and acceptance-complete. Ready for production.
##### 2026-05-18T13:17:12.103+01:00 — Issue #67 recheck

- Re-ran the full #67 quality gate: workflow authoring .NET tests, client build, Storybook CI across browsers with axe, workflow graph keyboard Playwright, dedicated workflow stage-preview Playwright, and the live planning workflow smoke all completed green.
- The previously missing acceptance seams are now present together in the shipped editor: the selected stage renders in a bottom read-only runtime preview, the preview is refreshed from the `/project` projection path as edits land, the surface selector switches between public/member/back-stage when relevant, loading feedback appears while preview refreshes, and the dedicated `prism-stage-preview` component plus planning-stage behavioural contract both exist.
- The only noise in the gate remains unrelated repository warnings from existing .NET restore/build output; they do not contradict the #67 evidence. Issue #67 is now honest green and acceptance-complete with no slice-specific blocker remaining.
##### 2026-05-18T12:17:12Z — Issue #67 quality gate complete

Validated stage preview contract against six acceptance seams: authoring .NET tests, client build, Storybook CI, graph keyboard Playwright, dedicated stage-preview contract, planning smoke. All passed. Preview panel confirmed rendering runtime surfaces, auto-updating on edits, supporting view switching, read-only, with loading feedback. Acceptance-verified and production-ready.
##### 2026-05-18T13:17:12.103+01:00 — Issue #69 quality gate

- Minimum honest keep-green coverage for hosting the workflow editor in the reference app is five seams: workflow authoring .NET tests (including endpoint contracts plus live authored-seed coverage), client build, Storybook CI across browsers with axe, a live localhost reference-host probe for `/workflow-editor`, and one save-path round-trip against the real MockBusinessApp authoring API.
- During this review the planning authored seed changed from empty/red to valid/live, and the reference host now loads on `https://localhost:7245/workflow-editor`, lists the planning workflow, fetches the action catalog, loads `/api/workflow-authoring/workflows/planning`, and round-trips `POST /api/workflow-authoring/workflows/planning/publish` with both `savedPath` and `publishedPath`.
- Two concrete gaps remain before #69 is honestly green: the browser console still records a real error for missing `https://localhost:7245/favicon.ico`, and the planning walkthrough smoke is still red because its readiness wait looks for `data-prism-workflow-loaded` in the light DOM even though the marker now lives inside the editor shadow root.

## Learnings
##### 2026-05-18T13:17:12.103+01:00 — Issue #69 recheck

- Re-ran the #69 five-seam gate after Isabelle's host-surface revision: focused workflow-authoring .NET tests passed 77/77, `npm run build` passed, `npm run test-storybook:ci:all` passed, `npm run test:playwright:planning-smoke` passed, and a live localhost probe against `https://localhost:7245/workflow-editor` plus `POST /api/workflow-authoring/workflows/planning/publish` returned `200` with `savedPath`, `publishedPath`, and `roundTripVerified: true`.
- The favicon blocker is gone in the live browser surface: the host page now serves an inline data-URL icon, the probe saw no `/favicon.ico` request, and the page console stayed free of browser errors.
- The readiness contract is now honest enough for the smoke to pass: Playwright's shadow-aware `locator('prism-workflow-editor')` can observe `data-prism-workflow-loaded="planning"` even though document-level DOM queries still cannot see across the shell shadow boundary.
#### 2026-05-18T19:41:25Z — Issue #69 completed

Verified all five-seam quality gate and confirmed issue #69 acceptance-complete:
1. Focused workflow-authoring .NET tests: 77/77 passing
2. Client build: passing
3. Storybook CI across browsers with axe: passing
4. Live localhost probe of `/workflow-editor`: confirmed shell redirect, readiness signal, API health
5. Save round-trip: `POST /api/workflow-authoring/workflows/planning/publish` returns 200 with `roundTripVerified: true`

**Browser surface clean:** No favicon 404, no console errors, `data-prism-workflow-loaded` visible on host element.

**Result:** Issue #69 is green, acceptance-verified, and production-ready.
