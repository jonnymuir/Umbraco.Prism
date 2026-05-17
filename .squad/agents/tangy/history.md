## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---


## 2026-05-17T18:30:56.987+01:00 — CI Fix Verification Pass

**Task:** Verify both diagnosed fixes are landed and green on `feat/workflow-editor-library-extraction`.

**Findings:**
- Both fixes were already committed and pushed before this session:
  - `47a50cf` — removed re-leaked `HMACSecretKey` from `appsettings.json`
  - `125f166` — increased smoke readiness timeout 5 min → 8 min; job cap 10 min → 15 min
- HEAD confirmed at `125f166`, matching `origin/feat/workflow-editor-library-extraction`
- CI runs `25997011837` and `25997011833` on that commit: **all 5 jobs green**
  - `core-tests` ✅, `planning-workflow-editor-smoke` ✅, `localhost-auth-playwright` ✅, `storybook-tests` ✅, `marketplace-description` ✅
- `appsettings.json` (tracked) confirmed clean — no HMAC key present
- `readinessTimeoutMs` confirmed at `480_000` in `live-app-host.ts`

**Outcome:** No new failures, no new code changes needed. Branch is green and ready for merge review.

**Decision:** `.squad/decisions/inbox/tangy-ci-fix-pass.md`

**Learnings:**
- Before doing any fix work, always check the latest CI run first — fixes may already be landed.
- Checking `git log` against `origin/` head and comparing run SHA to HEAD is the fastest verification path.
- The secret guard test is a permanent CI asset: it caught a real re-leak and will catch future ones.

---

## Learnings (Summarized)

### 2026-05-17T13:59:00+01:00 — Smoke Test Failure: Missing Build Step in CI

**Task:** Diagnose failing `planning-workflow-editor-smoke` CI job on PR #52 without guessing.

**Diagnosis Method:**
1. Downloaded CI artifacts (trace.zip, screenshots) from failed run 25991274355
2. Screenshot showed blank page; test timed out waiting for `[data-prism-workflow-loaded]`
3. Ran test locally → passed (1.1m)
4. Deleted local dist/ directory to simulate CI → would fail same way
5. Compared CI workflow: missing `npm run build` between `npm ci` and test execution

**Root Cause:** MockBusinessApp serves workflow-editor.html from `src/UmbracoPrism.Core/wwwroot/dist/`, which is populated by Vite build. CI never ran the build, so dist/ was empty, resulting in blank page (404/empty response).

**Fix:** Added `npm run build` step to both `planning-workflow-editor-smoke` and `localhost-auth-playwright` jobs in `.github/workflows/ci-tests.yml`. Verified locally: cleaned dist/, rebuilt, test passed.

**Lesson:** CI jobs must include full build chain. Don't rely on pre-existing artifacts. Test "clean checkout → build → test" path locally before pushing CI changes.

**Committed:** `10522f6` — fix(ci): add missing Vite build step to Playwright test jobs  
**Decision:** `.squad/decisions/inbox/tangy-smoke-failure-diagnosis.md`

### 2026-05-17T12:45:42.676+01:00 — Fast-Fail CI Strategy for Flaky Tests

### 2026-05-17 — Recent Session Summary

- Analyzed CI timing and localhost-auth Playwright strategy
- Documented E2E CI architecture recommendations
- Coordinated with Tom Nook on faster-fail strategy
- Decision entries merged to shared decisions.md

## 2026-05-17T12:32:29.455640Z

Analyzed CI timing and localhost-auth Playwright strategy; wrote decision inbox entry for E2E strategy

## 2026-05-17T13:36:14.940+01:00 — E2E Strategy Implementation

**Task:** Implement complete fast-fail + shared-environment strategy for localhost-auth tests.

**Delivered:**
1. CI fast-fail with `--max-failures=1` on localhost-auth lane
2. Dedicated `planning-workflow-editor-smoke` CI job for early signal
3. Playwright worker fixture (`shared-app-host-fixture.ts`) for shared AppHost across specs
4. Planning walkthrough migrated to use worker fixture (removed per-spec lifecycle)
5. Preserved all diagnostics and isolation guarantees

**Key Learnings:**
- **Worker fixtures need `auto: true`** if tests don't explicitly reference them in signatures
- **globalSetup doesn't share state** with worker processes; worker-scoped fixtures are the correct pattern for shared infrastructure
- **Explicit > implicit for isolation:** resetWorkflows() in beforeEach is the right contract for workflow state, not just relying on runtime reset
- **AppHost.start() is already idempotent** (checks `if (this.child) return`), making it safe for multiple specs to call
- **Test passed in 1.1min** (33s startup + execution) vs previous ~3min+ per-spec cost

**Performance Impact:**
- Before: 12 walkthroughs × ~1min startup = ~12min baseline
- After: 1 startup (~33s) for entire batch
- **Expected CI improvement: ~25-28min → ~10-12min for full suite**

**Validation:**
- Local planning smoke run: **1 passed (1.1m)**
- Worker fixture logs confirm clean startup/teardown lifecycle
- Isolation verified: fresh browser context + resetWorkflows() beforeEach
- Decision written to inbox, history updated
- Committed (`7d7f7b9`) and ready to push

**Next:** Push to trigger CI and validate smoke lane runs before broader suite.

### 2026-05-17T17:09:07.957+01:00 — Reference shell smoke coverage

**Task:** Adapt workflow-editor validation for the new reference split so the business app shell proves the downstream integration story, not just the editor internals.

**What changed:**
- Updated the planning workflow walkthrough to start at `/workflow-editor`, assert the thin-shell guidance copy, workflow picker, authoring API base, and integration snippet.
- Scoped the NL input assertion to `data-prism-conversation-input` so the new shell config textbox does not cause strict-mode collisions.
- Added test-side restoration of `workflow-authored/planning.workflow.json` and `.provenance/` so proposal-apply validation stays side-effect free for repeated runs.

**Validation:**
- `dotnet test src/UmbracoPrism.Core.Tests/UmbracoPrism.Core.Tests.csproj -c Release` ✅
- `npm run test:playwright:planning-smoke` ✅
- `npm run test:playwright:localhost-auth -- --grep 'Planning Workflow Editor walkthrough' --max-failures=1` ✅

### 2026-05-17T17:33:13.797+01:00 — Walkthrough doc + screenshot regeneration

**Task:** Determine whether reference-split walkthrough screenshots had been regenerated for the new shell flow; if not, update and regenerate.

**Findings:**
- Screenshots in `docs/images/walkthroughs/planning-workflow-editor/` were captured at `b9f0977` against the OLD direct-URL flow before the reference shell was introduced.
- The walkthrough doc had only `<!-- Screenshot: ... -->` placeholder text — real `![](...)` embeds were never added.
- All reference-split code changes (shell component, runtime library, MockBusinessApp wiring, spec updates) were uncommitted in the worktree despite being recorded as complete in history.

**Actions taken:**
1. Updated `docs/walkthroughs/planning-workflow-editor.md`:
   - Replaced all screenshot placeholders with `![](../images/walkthroughs/planning-workflow-editor/XX.png)` embeds.
   - Rewrote Step 1 narrative to describe the `/workflow-editor` redirect, thin-shell hero copy, workflow picker, API base field, and integration snippet.
   - Corrected API path references in Step 7 to `/api/workflow-authoring/workflows/planning/preview` and `.../apply`.
   - Updated R5 spec back-reference to `01-planning-workflow-editor.walkthrough.spec.ts`.
2. Committed all reference-split changes in `47a50cf` on `feat/workflow-editor-library-extraction`.
3. Pushed branch; triggered `capture-screenshots.yml` workflow_dispatch (run 25996681743) to regenerate the 8 PNGs from the new shell flow.
4. Wrote decision to `.squad/decisions/inbox/tangy-walkthrough-screenshots.md`.

**Lesson:** When the spec is updated to test a new flow, screenshots must be explicitly regenerated via the capture workflow — the old PNGs don't self-update. Track this as a post-spec-change step: trigger capture-screenshots immediately after any spec navigation change.

### 2026-05-17T22:05:30.472+01:00 | Design rewrite batch + CI verification

- Verified CI fixes for PR #53 (feat/workflow-editor-library-extraction): core-tests green (HMAC secret removal in 47a50cf), smoke tests green (readiness timeout 8 min in 125f166). All five CI jobs passing.
- Identified screenshot regeneration requirement: library extraction introduced new reference shell; old screenshots capture pre-extraction flow. Decision documented to trigger `capture-screenshots.yml`.
- Produced two decisions merged to `.squad/decisions.md`:
  1. **tangy-ci-fix-pass.md** — CI verification green; branch ready for merge review.
  2. **tangy-walkthrough-screenshots.md** — Screenshot regeneration strategy (capture-screenshots workflow commits updated images back to branch automatically).
- PR #53 branch is in fully green state pending screenshot workflow completion.
