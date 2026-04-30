# Session Log: PR #38 CI Green Closeout

**Date:** 2026-04-30  
**Milestone:** First feature-branch + PR + green-CI release cycle (feature policy active as of commit `c04f32b`)  
**Result:** ✅ All 4 CI checks green; PR #38 squash-merged as `dc316fb` on `main`

---

## One-Paragraph Summary

PR #38 ("fix(ci): green up CI Tests + Squad Release on main") was the first work item under the new feature-branch + PR + green-CI policy. Mabel, Blathers, and Brewster collaborated on branch `fix/ci-green` over two failed CI rounds and one successful round. Round 1 fixed a MockBusinessApp DI crash; round 2 misdiagnosed the root cause and shipped a deadlock-inducing polling fix; round 3 identified the true culprits: (a) Umbraco's sequential notification dispatch + stale composer ordering, and (b) a config-presence anti-pattern gating authentication scheme defaults. The new branching policy worked: the PR gate caught the round 2 regression that would otherwise have landed on main. Final commit `dc316fb` verified all 4 CI checks green.

---

## Timeline

### Round 1: Blathers — MockBusinessApp DI Crash (Commit `6751662`)

**Symptom:** `localhost-auth-playwright` lane timing out; readiness probe unable to connect  
**Root cause:** SEC-003 added `IWorkflowContentSanitizer` dependency to `BusinessAppWorkflowEngine`. MockBusinessApp's DI container never registered this interface (only TestSite does via `WorkflowBuilderExtensions`). App crashed at startup; Aspire kept port bound but with no HTTP response.  
**Fix:** Added `PassthroughSanitizer` (file-scoped) to MockBusinessApp's `Program.cs`  
**Impact:** Unblocked Playwright lanes; all 601 Core tests pass  

### Round 2: Blathers — Polling for Content Types (Commit `46826fe`)

**Symptom:** Workflow pages seeded as `published: false`  
**Assumed root cause:** Concurrent handler dispatch race — `WorkflowPageSeeder` running before `PrismContentTypeSeeder` on fresh databases  
**Attempted fix:** Add async polling: wait up to 90 seconds for `workflowPage` type to exist before seeding  
**Regression:** Home and dashboard also failed to publish; `/dashboard` returned 500  
**Why:** Umbraco's notification handlers are **sequential**, not concurrent. The async poll held the dispatch chain for 90 seconds, preventing type-creating seeder from running behind it — **deadlock**.

### Round 3: Brewster — Real Root Causes + Fixes

#### Fix 1: Composer Ordering (Commit `ffa1034`)

**Diagnosis:** Sequential notification dispatch means registration order is execution order. `TestSiteComposer` had no `[ComposeAfter]` attribute, so assembly load order determined whether type-seeder ran before or after content-seeder. On fresh CI, it didn't.  
**Fix:** Add `[ComposeAfter(typeof(PrismComposer))]` to `TestSiteComposer` — idiomatic Umbraco tool for cross-assembly handler dependencies  
**Result:** Types created first, content seeded second; all workflow pages publish

#### Fix 2: Auth Scheme Defaults (Commit `42b85e5`)

**Diagnosis:** `PrismComposer` gated auth scheme registration on presence of `Prism:VaultUri` config value. Security patch `b6336fd` removed this secret from tracked `appsettings.json`. Silent result: `DefaultAuthenticateScheme` never registered.  
**Symptom:** Route-hijacking controllers with explicit scheme worked; home page using default scheme always showed "Sign In" (Umbraco's fallback scheme didn't decrypt `PrismMemberCookie`)  
**Fix:** Unconditional auth scheme registration — VaultUri is infrastructure detail, not feature flag  
**Result:** Auth state consistent across all routes

### CHANGELOG + Workflow Guard (Commits `da5d29d`, `8809c64`)

**Mabel's parallel work:** Added v1.8.0 CHANGELOG entry consolidating 11 security findings. Fixed Squad Release workflow regex to accept optional `v` prefix in version tags (`[v1.8.0]`).

---

## Policy Validation

### ✅ Feature-Branch + PR + Green-CI Worked

- **Branch isolation:** `fix/ci-green` allowed Blathers and Brewster to iterate without destabilizing `main`
- **PR gate caught regression:** Round 2's polling deadlock was caught in CI before merge. This would have landed broken on `main` without the PR requirement
- **Green checks enforced:** All 4 CI checks required green before merge (passed only on round 3)

### Lessons

1. **Green CI ≠ problem solved:** Round 2 passed initial CI start check (app didn't crash anymore) but introduced a deadlock that showed up in Playwright latency. Symptom validation is as important as gate validation. Route-level checks (seed-contract probe) more reliable than app-startup checks.

2. **Optional config values must not gate foundational behaviour:** Using `Prism:VaultUri` presence to determine if auth is enabled conflates two concerns: (a) authentication capability (always needed) and (b) secret-provider configuration (environment-specific). The decoupling revealed that the auth flags should have been unconditional all along.

3. **Umbraco notification handler dispatch is sequential:** This broke Blathers' assumption of concurrent dispatch and led to the polling misfix. For future Umbraco work: always use `[ComposeAfter]` / `[ComposeBefore]` for handler ordering, not timing-based solutions.

---

## Artifacts Merged

- **Inbox decisions merged into `.squad/decisions.md`:**
  - `blathers-ci-green.md` — Round 1 IWorkflowContentSanitizer fix + Round 2 race finding
  - `brewster-auth-vaulturi-flag.md` — Auth flag anti-pattern
  - `brewster-ci-green-round3.md` — Seeding order + auth fixes
  - `mabel-ci-green.md` — CHANGELOG + regex fix

- **History files updated:**
  - `.squad/agents/blathers/history.md` — CI-green session + round 2 lessons
  - `.squad/agents/mabel/history.md` — CHANGELOG + workflow fix session
  - `.squad/agents/brewster/history.md` — Two-root-cause session

---

## Final State

- ✅ PR #38 squash-merged as commit `dc316fb` on `main`
- ✅ All 4 CI checks green
- ✅ 601 Core unit tests pass
- ✅ Seed contract validation passes (home, dashboard, 5 workflow pages)
- ✅ All Playwright specs pass
- ✅ v1.8.0 release gate satisfied

---

## Inbox Status

Deleted after merge:
- `.squad/decisions/inbox/blathers-ci-green.md`
- `.squad/decisions/inbox/brewster-auth-vaulturi-flag.md`
- `.squad/decisions/inbox/brewster-ci-green-round3.md`
- `.squad/decisions/inbox/mabel-ci-green.md`

Inbox is now clean of PR #38 artifacts.
