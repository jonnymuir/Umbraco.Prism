# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## Session: PR #38 CI Green Root Causes — Round 3 Seeding Fix + Auth Flag Bug (2026-04-30)

**Status:** ✅ Complete — Commits `42b85e5`, `ffa1034` on `fix/ci-green` (merged as `dc316fb` on main)

**Scope:** Investigate and resolve CI green failures in `localhost-auth-playwright` lane. Identified two independent root causes.

### Root Cause 1: Notification Handler Registration Order (Commit `ffa1034`)

**Finding:** Umbraco's `INotificationAsyncHandler` dispatch is sequential (not concurrent, as Blathers assumed in round 2). Assembly load order meant `TestSiteComposer` ran before `PrismComposer`, registering `WorkflowPageSeeder` before `PrismContentTypeSeeder`. On fresh CI, seeder ran first → found no types → skipped seeding.

Blathers' round 2 polling fix made it worse: async `Task.Delay` loop held the dispatch chain for 90 seconds, preventing type-creating seeder from running — deadlock.

**Fix:** Add `[ComposeAfter(typeof(PrismComposer))]` to `TestSiteComposer` for explicit composer ordering. Now `PrismContentTypeSeeder` runs first (creates types), `WorkflowPageSeeder` runs second (seeds content).

**Impact:** All 5 workflow pages publish on fresh CI; home and dashboard routes work; `/my-workflows`, `/apply-for-planning-permission` routes unblocked.

### Root Cause 2: Auth Scheme Defaults Gated on VaultUri (Commit `42b85e5`)

**Finding:** `PrismComposer` used presence of `Prism:VaultUri` as a feature flag: `isAuthEnabled = !string.IsNullOrEmpty(vaultUri)`. Security commit `b6336fd` removed `VaultUri` from `appsettings.json` (it's a secret). This silently set `isAuthEnabled = false`, so `DefaultAuthenticateScheme`, `DefaultSignInScheme`, `DefaultChallengeScheme` were never registered.

Route-hijacking controllers with explicit scheme worked (`[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`), but home page using default scheme always showed signed-out (Umbraco's fallback scheme didn't decrypt `PrismMemberCookie`).

**Fix:** Auth scheme defaults are unconditional. Removed `isAuthEnabled` gate; always register:
```csharp
options.DefaultAuthenticateScheme = "PrismMemberCookie";
options.DefaultSignInScheme = "PrismMemberCookie";
options.DefaultChallengeScheme = "PrismEntraID";
```

**Impact:** Home page correctly reflects authenticated state; all routes work consistently.

**Architectural Lesson:** Optional config values (especially secrets/URIs) must never gate foundational subsystems. Decoupling was the fix, not feature-flagging.

### Test Results

- 601 Core unit tests pass
- All Playwright specs green
- Seed contract validation passes (home, dashboard, all 5 workflow pages)

---

## 📌 2026-04-30: Cross-Agent Note — Umbraco Route Review Pending

**Note:** No new work on Brewster's roadmap as of 2026-04-30, but architectural debt flagged in prior work (2026-04-14 review):
- Missing `[ModelType("alias")]` on route-hijack controllers
- Unfinished `workflowDemoPage` surface area (placeholder bundle)
- Custom `Prism.Section` unused in backoffice (conditioned into `Umb.Section.Content`)

**Status:** Documented for future sprint review; no blocking issues on v2.0 rollout.

---

## Prior Work Summary (2026-04-14 — Umbraco v17 Solution Review)

**Architecture Validation:**

✅ **Route Hijacking Pattern:** Prism strongest when Umbraco owns authored route + page shell; business app owns workflow state. Current `workflowPage`/`workflowHub` pattern is close to idiomatic v17.

✅ **Route Contract:** `/dashboard`, `/get-in-touch`, `/my-workflows` are valid published routes with correct auth challenge behavior.

⚠️ **Document Type Design:** `PrismContentTypeSeeder` minimal (no richer page fields, thin editor affordances). `workflowPage` seeded as root (should be under Home).

⚠️ **Umbraco-Specific Risks:**
- Missing `[ModelType("alias")]` on hijacked controllers
- `MemberDashboardController` hardcodes `/dashboard`, bypasses `CurrentTemplate()`
- `HomePage.cshtml` untyped, reads non-existent model properties

⚠️ **Backoffice Dashboard:** v17-native extension stack (Lit/UUI) but custom `Prism.Section` unused; dashboard conditioned into `Umb.Section.Content`.

⚠️ **Unfinished Surfaces:** `workflowDemoPage` points at placeholder bundle (doesn't exist); instance-picker UI in Razor but never activated.

**Auth Flow Validation:**

✅ **Auth Cookie Behavior:** Fixed restart-recovery by detecting IssuedUtc < ProcessStartedUtc; force token refresh on runtime restart.

✅ **OIDC Scope Strategy:** Localhost demo requests offline_access for restart tolerance; generic OIDC tenants default to "openid profile" only.

✅ **Keycloak Refresh:** Refresh calls omit scope parameter when using offline_access-issued tokens.

✅ **Route-Readiness Strategy:** Tests should wait for seed-contract-ready (`GET /api/prism/downstream-demo/seed-contract-ready`), not cold-start transient `/` fallbacks.

**Cold-Boot Convergence Insight:**

Seeded routes briefly resolving to `/` on first boot is a cold-start artifact of this app's runtime pattern (reset DB → install → publish demo tree → eager route consumption). Warm, settled Umbraco shouldn't persist this.

---

## Key Learnings Preserved

1. **Umbraco owns routes; Prism owns plumbing** — clear separation keeps architecture clean
2. **Cold-start readiness probes > page copy checks** — route contract is authoritative signal
3. **Don't persist one-off OIDC redirects in auth cookies** — causes false fallbacks on later navigation
4. **Behavior tests should never absorb cold-start quirks** — wait for settled contract, then assert final state
5. **Auth scope strategy differs by tenant** — offline_access for demo, standard scopes for production

---
