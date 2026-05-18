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

## 2026-05-18T12:17:12Z — Issue #64 acceptance confirmed

Re-ran the full quality gate (seven seams: client build, authoring tests, Storybook CI, graph keyboard, action editor, copy/paste contract, planning smoke). All passed. Issue #64 is acceptance-complete and ready for merge. Unrelated CSS noise does not block this slice.

