# History: Tangy (Tester)

## 2026-05-22T19:54:45.780+01:00 — Editor Shell Behavioral Proof Design

**Status:** Tests designed and landed, awaiting Isabelle's shell implementation

**What I delivered:**

1. **New dedicated test file:** `tests/workflow-editor/workflow-editor-shell.spec.ts`  
   Comprehensive behavioral proof covering:
   - Persistent outline visibility and navigation
   - Tabbed confidence surfaces (validation/preview/simulation)
   - Selection sync across outline/graph/list/inspector
   - Keyboard and focus flow through the new shell
   - Integration with existing behaviors (undo/redo, copy/paste)

2. **Enhanced walkthrough assertions:** `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`  
   Added shell-specific checks:
   - Outline visible when editor loads
   - Confidence tabs present and functional
   - Selection sync with outline highlighting
   - Outline stays visible across mode switches
   - Tabs replace stacked panels

3. **Test hook requirements:** `.squad/decisions/inbox/tangy-editor-shell-proof.md`  
   Documented exact semantic selectors/ARIA states needed:
   - `[data-prism-workflow-outline]` and outline item structure
   - `[data-prism-confidence-tabs]` and tab/panel contracts
   - Selection sync via `aria-current`, `aria-pressed`, `aria-selected`
   - Keyboard flow and focus restoration patterns

**Working in parallel with Isabelle:** All test hooks are documented inline with `BEHAVIORAL HOOK REQUEST FOR ISABELLE` comments. The tests are ready to run once her shell implementation lands.

**Validation approach:**
- Tests will fail until shell implementation is complete (expected)
- Build shows in-progress TypeScript errors in Isabelle's files (expected)
- Tests guide the final behavioral contracts without blocking her work
- Once implementation lands: `npm run build`, then run shell spec and planning smoke

**Plain-language verdict:**  
The behavioral proof is complete and ready. It proves the four critical shell improvements (persistent outline, tabbed confidence surfaces, selection sync, keyboard flow) that make this a "mature" editor. The tests are waiting for Isabelle's implementation to land.

## 2026-05-18T22:14:30.041+01:00: Issue #72 Final Review — APPROVED ✅

### Status
**Result:** ✅ LANDING GATE CLEAN AND GREEN

**Re-verified all seven seams after warning cleanup:**
1. ✅ Build — green **and warning-free**
2. ✅ Client build — green
3. ✅ Backend contract tests — green
4. ✅ Playwright contract — green
5. ✅ Storybook CI — green
6. ✅ Planning smoke test — green
7. ✅ dotnet test — all passing (fixture resolved)

**Product statement:** Editor, admin, and runtime still agree on four-workflow reference contract after cleanup. No additional test edits needed.

**Validation cleanliness:** Branch is procedurally clean for merge. Working tree uncommitted files are staging task for Tom Nook's orchestration.

**Decision docs:** Merged to `.squad/decisions.md` (from inbox/tangy-landing-gate.md, inbox/tangy-clean-landing-gate.md, and supporting analysis docs)

## 2026-05-21T21:54:07.868+01:00 — PR #75 localhost-auth gate rerun

**Status:** REJECTED — PR #75 is still not ready to merge.

**Rerun evidence:**
- ❌ Exact local lane repro: `cd src/UmbracoPrism.Client && npm ci && npm run build && npm run test:playwright:localhost-auth -- --max-failures=1`
- ❌ Focused seam repro: `cd src/UmbracoPrism.Client && npm run test:playwright:localhost-auth -- --grep "signed-in member can still call the mock business app API after the whole stack restarts"`
- ✅ Other PR #75 checks currently report green in GitHub (`marketplace-description`, `test`, `storybook-tests`, `core-tests`, `planning-workflow-editor-smoke`)

**What failed in human terms:**
- The localhost-auth lane still falls over on the restart contract, not on the initial sign-in path.
- On the focused rerun, the stack restarted, then the AppHost failed to come back cleanly because port `22194` was still in use.
- That left the readiness probe waiting for the authenticated TestSite seams (`/my-workflows` seed contract / redirect path), so the same lane remains red.

**Verdict:**
- Blathers' fix was not enough to clear the final CI blocker on this branch.
- I did not make any test-only changes, and I did not commit, push, or merge.
- Decision artifact written to `.squad/decisions/inbox/tangy-localhost-auth-gate.md`.

## 2026-05-21T21:54:07.868+01:00 — PR #75 localhost-auth final local verdict

**Status:** GREEN — the last product blocker is gone on the closest faithful local repro.

**Closest faithful local repro:**
- ✅ `cd src/UmbracoPrism.Client && node ../../scripts/validate-aspire-prereqs.mjs --localhost-auth-suite && npx playwright test -c playwright.localhost-auth.config.ts tests/localhost-auth-session.spec.ts --reporter=line --max-failures=1`

## 2026-05-21T21:54:07.868+01:00 — Information-request walkthrough fix for PR #75

**Status:** PARTIALLY FIXED — the reported information-request blocker is fixed, and the lane now fails later on planning workflow parity.

**Diagnosis:**
- The page still reached `/request-information` and rendered the heading plus submit button.
- The `First name` field never appeared because the authored reference workflow for `information-request` only defined stages/transitions, not the renderable field payloads the walkthrough exercises.
- Once I restored authored/runtime parity for that workflow, the same mismatch showed up on `payment-demo`, confirming the root issue was sparse authored reference data rather than readiness timing.

**Fix applied:**
- Kept MockBusinessApp on the authored reference runtime path and enriched the authored `information-request` workflow so it projects the expected request form and under-review state.
- Enriched the authored `payment-demo` workflow so it projects the payment form, processing state, reviewer completion transition, and completed confirmation copy.
- Extended confirmation projection so authored confirmation stages can carry follow-up body copy from `Description`.
- Added a payment-specific minimum-amount validation rule so the existing walkthrough/contract expectation for `0` remains honest.

**Validation:**
- ✅ Focused repro before fix: signed-in `/request-information` page had heading + submit only, zero form labels.
- ✅ `dotnet build UmbracoPrism.sln`
- ✅ `cd src/UmbracoPrism.Client && npx playwright test -c playwright.localhost-auth.config.ts tests/walkthroughs/information-request.walkthrough.spec.ts tests/walkthroughs/payment-demo.walkthrough.spec.ts --reporter=line --max-failures=1`
- ⚠️ `cd src/UmbracoPrism.Client && npm run test:playwright:localhost-auth -- --max-failures=1`
  - info-request no longer blocks
  - payment-demo no longer blocks
  - next hidden blocker is now `tests/walkthroughs/planning-notification.walkthrough.spec.ts` expecting `Describe your project`

**Git action:**
- I expanded the authored reference workflows but have not yet made the branch fully green.

**Plain-language verdict:**
- The original information-request failure was real product drift in the authored demo workflows, not a flaky wait.
- Fixing that drift also fixed the next payment-demo failure in the same lane.
- The localhost-auth lane is still not green overall; planning-notification is now the first remaining blocker.
- Decision artifact written to `.squad/decisions/inbox/tangy-information-request-walkthrough-fix.md`.

## 2026-05-22T05:48:34.538+01:00 — PR #75 localhost-auth lane green

**Status:** GREEN — ready on the tested localhost-auth seam.

**What I verified:**
- Reviewed the latest failing GitHub `localhost-auth-playwright` evidence: the planning workflow editor walkthrough stalled because the visible `Send` button was being pointer-blocked by overlapping editor chrome in CI.
- Re-ran the full localhost-auth Playwright lane on the current branch head after the keyboard-activation walkthrough fix landed.
- Result: `34 passed`, `7 skipped`, `0 failed` on `cd src/UmbracoPrism.Client && npm run test:playwright:localhost-auth -- --max-failures=1`.

**Plain-language verdict:**
- The remaining localhost-auth blocker is gone.
- I did not merge the PR.
- Decision artifact written to `.squad/decisions/inbox/tangy-localhost-lane-green.md`.

## 2026-05-22: Issue #74 QA completion and validation

**Role-first swim lanes QA complete and all gates green.**

- Updated workflow graph keyboard tests for role-first lanes
- Added planning walkthrough assertions
- Verified accessibility (Storybook CI)
- All quality gates passing: client build, keyboard tests, visual regression, Storybook accessibility
- Created testing skill and decision inbox notes
- Awaiting merge review

## 2026-05-22T20:06:00Z — Scribe Batch Close: Cross-Agent Sync

**Context:** Batch orchestration complete. Scribe merged 5 decision inbox entries from this session's agent work (Isabelle, Tangy, Tom Nook).

**Your contributions referenced:**
- `tangy-editor-shell-proof.md` — behavioral test spec (24 tests, documented hooks, validation commands)
- `tangy-mature-editor-quality-bar.md` — quality bar definition for "mature" editor

**Cross-agent outcomes:**
- Isabelle delivered shell implementation (outline, tabs, selection sync, focus management)
- Tom Nook locked strategic direction (Phase 1–5 roadmap, Phase 1 is integration-first cohesion)
- Scribe merged all decisions to `.squad/decisions.md`
- Orchestration logs written for all three agents

**Your tests are unblocked:** All semantic hooks documented in your spec. Ready to run once Isabelle's shell implementation lands. Validation commands: `npm run build`, `playwright test workflow-editor-shell.spec.ts`, `npm run test:playwright:planning-smoke`.

**Status:** All squad metadata written; ready for merge.
