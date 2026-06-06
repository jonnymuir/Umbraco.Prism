## 2026-06-05: Queue-only model implementation completed

- Tom Nook: Contract definition and implementation plan locked
- Tangy: Behavioural test specification and validation gates defined
- Isabelle: Editor refactor completed (build ✅, Playwright suite ✅)
- Blathers: Runtime refactor completed (core test suite ✅)
- Team coordination: All decisions merged to .squad/decisions.md
- Next: Full integration validation and cross-stack testing

---

# Tangy — History

**Full history:** See `history-archive.md` for sessions prior to 2026-06-04.

## 2026-06-04: Flattened Workflow Model Session

**Agents:** Tom Nook, Tangy, Isabelle, Blathers
**Session:** Queue-first architecture consolidation

See `.squad/decisions.md` and `.squad/log/2026-06-04T21-31-07Z-flattened-workflow-model.md` for details.

## Session: 2026-06-06 Save Error Orchestration

**Status:** ✅ Complete

**Tangy contribution:** Added focused Playwright contracts for save error outcomes: successful save, structured failure, persistent/copyable reporting, recovery after retry. Fixtures include stack-trace-shaped noise to prove sanitization. Tests stay at editor boundary by swapping host workflowSource.

**Team outcomes:**
- Blathers: Backend save validation and structured errors
- Isabelle: Persistent, copyable, sanitised error UI
- Tangy: 4-contract regression coverage

**Integration:** All decisions merged to .squad/decisions.md. Orchestration logged in .squad/orchestration-log/. Session log at .squad/log/2026-06-06T10-27-53Z-save-error-fix.md

## Session: 2026-06-06 Workflow Editor Fix Validation

**Branch:** `fix/workflow-editor-save-and-layout`
**Status:** ✅ APPROVED

**Validation outcomes:**
- Backend build: ✅ 0 errors, 7 warnings (pre-existing)
- Backend tests: ✅ 798/798 passed
- Frontend build: ✅ No TypeScript errors
- Playwright: 137 passed, 20 failed (all 20 pre-existing — walkthrough tests need live app, add-route-affordance failures confirmed pre-existing on baseline)

**Fix reviews:**
- Fix 1 (Blathers — AllowOutOfOrderMetadataProperties): Confirmed at MockBusinessApp/Program.cs line 140, applied to `mockWorkflowJsonOptions` used on the PUT workflow endpoint. No Storybook-level test can cover backend deserialization; the existing shell save test exercises the happy path. Noted as coverage gap for live-app API tests.
- Fix 2 (Isabelle — Dismiss button): Confirmed at prism-workflow-editor.ts lines 2111–2119 with correct `aria-label` and `data-prism-dismiss-save-error`. Added dismiss contract to `workflow-editor-validation.spec.ts` — 7/7 pass.
- Fix 3 (Isabelle — Y-axis longest-path): Confirmed parity-stepped code replaced by longest-path algorithm at lines 472–504. Visual/positional cross-lane layout tests require a running app — noted as a coverage gap.

## Learnings

- Pre-existing Playwright failures (add-route-affordance b/c/d/e, walkthrough tests) are infrastructure-dependent — the live MockBusinessApp must be running for walkthrough/four-workflow-contract tests to pass. Confirm this is expected baseline before flagging as regressions.
- `AllowOutOfOrderMetadataProperties` on a `JsonSerializerOptions` instance (not on the `[JsonPolymorphic]` attribute) is the correct fix pattern for System.Text.Json discriminator ordering in .NET.
- The dismiss button clears both `_saveError` and `_saveErrorCopyStatus` in one click handler — both should be verified gone in the dismiss contract.


## 2026-06-06: Validation workflow-editor-save-and-layout fixes

Validated three coordinated fixes:
- Blathers: JSON polymorphic discriminator order (backend)
- Isabelle: Save error dismiss button + Y-axis layout (frontend)

Added new test `workflow-editor-validation.spec.ts` for dismiss button. Confirmed 137 Playwright tests passing, 20 pre-existing failures unrelated to fixes. Verdict: ✅ APPROVED.

## Learnings

- 2026-05-25T09:32:35.455+01:00 — For the concurrent multi-lane redesign, keep one straight-line workflow proof green as a control while adding new parallel-lane and join-gateway proofs. Slice the work into editor contracts, showcase-story evolution, and live walkthrough proof so the behavioural gate can move forward without losing demo clarity.
- 2026-05-25T09:54:48.365+01:00 — When workflow surface rules are being collapsed, keep Playwright contracts on user-facing language: tab roles, visible lane labels, and assignment copy. Avoid asserting internal surface enums or exact lane counts that can change during cleanup without changing author-visible behaviour.

## 2026-05-25 (09:32:35 UTC) — Behavioural Test Track for Concurrent Lanes

- Issues #78–#80 created for editor, showcase, and walkthrough coverage
- Orchestration log recorded
- Tom Nook executing parallel redesign sequence (#81–#87)
- Coordinated squad execution ready
