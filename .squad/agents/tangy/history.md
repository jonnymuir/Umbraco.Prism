# History: Tangy (Tester)

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

### 2026-05-18T13:17:12.103+01:00 — Issue #63 flake close-out

- The dedicated undo/redo contract was over-eager on the stage-create path: asserting the inspector immediately after dialog close occasionally outran the host-to-graph selection handoff even though the selected stage surfaced on the next render. Stabilise that path by waiting for the new stage node itself to exist and report `aria-pressed="true"` before asserting the inspector detail.
- Re-ran the full #63 keep-green gate after tightening the behavioural contract: authoring-focused .NET workflow tests, client build, Storybook CI across browsers with axe, the graph keyboard contract, the dedicated workflow-editor history contract, and the live planning workflow smoke all passed. That is now honest green coverage for the shipped undo/redo slice.

## 2026-05-18T12:17:12Z — Issue #63 quality gate complete

Identified missing acceptance items, stabilized flaky stage-create test, confirmed selection determinism, and marked slice as green and acceptance-complete.

### 2026-05-18T13:17:12.103+01:00 — Issue #64 copy/paste quality gate

- Minimum honest keep-green coverage for workflow editor copy/paste is seven seams: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, the graph keyboard contract, the action editor contract, a dedicated copy/paste Playwright contract, and the live planning workflow smoke.
- On the current branch, the supporting seams are healthy but the #64 slice has not landed: the editor toolbar only exposes undo/redo plus view toggle, the graph context menu only offers “Copy stage JSON” and “Copy transition JSON”, and there is no stage/action paste flow, clipboard state indicator, or Ctrl/Cmd+C and Ctrl/Cmd+V authoring contract.
- Do not call #64 green until the shipped surface proves pasted stages/actions become the active edit target, copied stages do not bring inbound or outbound transitions with them, validation warnings surface immediately after paste, and the dedicated behavioural contract covers same-stage and cross-stage action paste alongside keyboard shortcuts.

## Learnings

### 2026-05-18T13:17:12.103+01:00 — Issue #64 recheck

- Re-ran the full #64 copy/paste gate: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, dedicated workflow copy/paste Playwright, and the live planning workflow smoke all passed.
- The dedicated behavioural contract now proves the previously missing acceptance seams: copied stages get fresh `-copy` keys, pasted stages exclude transitions, validation warnings surface after paste, toolbar clipboard state is visible, Ctrl/Cmd+C and Ctrl/Cmd+V work, action paste works in the same stage and a different stage, and the pasted stage/action becomes the active edit target immediately.
- The unrelated `govuk-frontend.min.css` authoring-test noise did not block this recheck; the live planning smoke stayed green, so #64 is now honest green and acceptance-complete.

### 2026-05-18T13:17:12.103+01:00 — Issue #65 quality gate

- Minimum honest keep-green coverage for workflow validation and error reporting is seven seams: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, a dedicated validation/error-reporting Playwright contract, and the live planning workflow smoke.
- The current branch only has partial #65 plumbing: `workflow-validation.ts` defines orphaned, unreachable, dead-end, and action-configuration issues in plain language; the graph surface exposes unreachable/dead-end jump buttons; and the inspector/action editor already show field-level validation feedback.
- Do not call #65 green yet: the shared `validateWorkflow(...)` helper is not wired into the host editor, there is no editor save affordance or validate-endpoint call to block critical saves, the visible rail only covers routing warnings instead of the full error set, and there is no dedicated behavioural contract proving rail links, orphaned-stage reporting, or save blocking.
- Re-ran the closest current gate: client build, workflow authoring .NET tests, Storybook CI, workflow graph keyboard Playwright, and the live planning smoke passed; the action-editor Playwright suite surfaced one retry-only flake in the existing keyboard SMS path before completing green, so treat that as unrelated noise rather than #65 evidence.

### 2026-05-18T13:17:12.103+01:00 — Issue #65 recheck

- Re-ran the full #65 quality gate: client build, workflow authoring .NET tests, Storybook CI across browsers with axe, workflow graph keyboard Playwright, workflow action editor Playwright, the dedicated workflow-editor validation Playwright contract, and the live planning workflow smoke all completed green.
- The previously missing acceptance seams are now present together in the host editor: `validateWorkflow(...)` drives the validation rail and save state, the rail lists orphaned/unreachable/action issues in workflow language with jump buttons, inspector focus follows rail jumps, and blocking structural errors disable save while action-field problems stay warnings.
- The action-editor suite still shows one retry-only flake in an older keyboard-only forms path, but it recovered and does not contradict the dedicated #65 evidence. Issue #65 is now honest green and acceptance-complete with no slice-specific blocker remaining.

## 2026-05-18T12:17:12Z — Issue #64 acceptance confirmed

Re-ran the full quality gate (seven seams: client build, authoring tests, Storybook CI, graph keyboard, action editor, copy/paste contract, planning smoke). All passed. Issue #64 is acceptance-complete and ready for merge. Unrelated CSS noise does not block this slice.

## 2026-05-18T13:17:12Z — Issue #65 validation quality gate and acceptance confirmation

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
