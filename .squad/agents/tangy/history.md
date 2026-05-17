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

## 2026-05-17T14:40:00+01:00 — Workflow Editor Library Extraction Test Validation

**Task:** Validate workflow editor library extraction slice in dedicated worktree; ensure tests pass and document outcomes.

**Context:**
- Extraction work done by Blathers (commits `9ab9ba4` through `538e843`)
- Worktree at `/Users/jonnymuir/Documents/Projects/Umbraco.Prism-workflow-editor-extraction` on branch `feat/workflow-editor-library-extraction`
- Scope: backend authoring code moved from Core → WorkflowEditor library with new two-line consumer API

**Deliverables:**
1. ✅ Resolved static web asset conflict (both Core and WorkflowEditor had dist/ assets)
2. ✅ Planning smoke test passes (1.1m) after clean rebuild
3. ✅ Verified zero walkthrough spec changes
4. ✅ Verified PhysicalFileProvider serving works for dev (ProjectReference consumption)
5. ✅ Pushed 5 commits to remote (extraction + test validation)
6. ✅ Wrote decision document: `.squad/decisions/inbox/tangy-workflow-editor-extraction-validation.md`

**Key Issue Resolved:**
- **Static web asset collision:** Core/wwwroot/dist and WorkflowEditor/wwwroot/dist both had vite.svg
- **Root cause:** Vite config updated but old Core dist/ not cleaned up
- **Fix:** Removed Core/wwwroot/dist; clean rebuild resolved all conflicts
- **Lesson:** When moving embedded static assets, delete old output directory before building

**Test Validation:**
- Planning workflow editor loads correctly at `/workflow-editor.html?workflow=planning`
- JavaScript module (`workflow-editor.js`) loads and custom element initializes
- All Aspire services (Aspire, TestSite, MockBusinessApp, Keycloak) start successfully
- No 404s, no build errors (except pre-existing deprecation warnings)

**Recommendations:**
1. Open PR now — all 5 commits ready for review
2. Add CI publish-path test (consume WorkflowEditor as NuGet, not ProjectReference) — top risk per design doc
3. After merge: cleanup Core/Workflow/Authoring empty directory in follow-up PR

**Outcome:** Extraction slice validated and ready for PR. Test coverage preserved with zero spec changes. Decision written to inbox for Scribe to merge.

**Next:** Await PR creation and merge. Monitor CI on PR to ensure full localhost-auth suite passes (not just smoke).
