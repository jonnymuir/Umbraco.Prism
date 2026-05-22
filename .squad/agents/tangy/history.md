# History: Tangy (Tester)

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
