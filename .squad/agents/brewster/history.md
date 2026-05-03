# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## Session: Downstream Demo HTML Validation Fix (2026-05-02)

**Status:** ✅ Complete — Commit `da7ddc9` on `main`

**Scope:** Fix false-positive bug where `DownstreamDemoController` treated HTML/non-JSON responses as success instead of errors. Tangy found that Codespaces port-forwarding pages ("Connecting to the forwarded port...") returned 200 OK with `text/html`, breaking the dashboard UI.

### Problem

The controller checked HTTP status code but not `Content-Type` header. Any 200 response was treated as success, including:
- `text/html` from Codespaces port-forwarding placeholders
- `text/plain` from misconfigured endpoints
- Other non-JSON responses

Dashboard UI expected structured JSON, so HTML responses broke the interface silently.

### Solution

Added `Content-Type` validation before processing response body:

1. **Validate JSON content type** — Only accept `application/json`, `application/problem+json`, `text/json`
2. **Return structured error for non-JSON** — `statusCode: 0`, `statusText: "Invalid Response"`, with clear error message
3. **Preserve Blathers' backchannel fix** — `BUSINESSAPP_BACKCHANNEL_URL` still takes precedence in Codespaces

**Implementation:**
- Added `IsJsonContentType(string)` helper to check for JSON MIME types
- Validate immediately after receiving HTTP response, before parsing
- Include user-friendly hint about Codespaces port-forwarding delays when HTML detected

**Test Coverage:** Tangy's 3 new regression tests:
- `DownstreamDemo_ReturnsError_WhenResponseIsHtml`
- `DownstreamDemo_DetectsCodespacesPortForwardingPage`
- `DownstreamDemo_RejectsNonJsonContentType`

**Test Results:** 653 Core tests pass (including all HTML validation tests)

**Impact:**
- HTML/non-JSON responses now surface as errors with actionable messages
- Dashboard shows clear error instead of breaking on invalid JSON parse
- Preserves all existing functionality (URL allowlisting, token refresh, backchannel URL)

**End-to-End Note:**
The fix ensures clear error messaging when port-forwarding pages appear. The underlying cause (BusinessApp not ready) still requires waiting for Codespaces to forward the port — but users now see an actionable error instead of a broken UI.

**Decision:** `.squad/decisions/inbox/brewster-downstream-html-validation.md`

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

## Session: Multi-tenancy & Editor UX Reflection (2026-05-01)

**Status:** ✅ Complete — Review at `.squad/reviews/2026-05-01-prism-reflection/04-brewster-multitenancy.md`

**Scope:** Deep Rams-grade review of multi-tenancy implementation, editor experience, and tenant isolation honesty.

### Key Findings

**Architectural strengths:**
- Host-based tenant resolution via `PrismTenantMiddleware` is clean, immediate (no restart), and correctly scoped per request via `IPrismContext` (scoped DI)
- 30-minute runtime cache with explicit invalidation on create/update/delete is efficient and operationally sound
- CSS-variable branding pipeline is genuinely excellent — annotation-driven, backoffice-managed, zero-deploy live updates
- Mobile branding via separate `MobileBrandingOverrides` column and Capacitor bundle generation is a differentiator
- `TenantManagementController` correctly gates behind both `BackOfficeAccess` and `PrismAdmins` policies — no privilege escalation path

**Honest gaps found:**
1. **Content tree is not isolated** — all tenants share the same Umbraco content tree. The walkthrough admits this; the product overview does not. A content editor for Tenant A can publish nodes visible on Tenant B's domain. This is the largest Rams #6 (Honest) violation.
2. **`MemberDashboardController` hardcodes `/auth/login?returnUrl=/dashboard`** (line 42) — not tenant-hostname-relative. On a second tenant, the OIDC challenge may resolve via the wrong host.
3. **30-minute deleted-tenant cache gap is undocumented** — `TenantService` caches for 30 minutes; no operator-facing warning exists; no manual flush endpoint.
4. **Email/push notification branding unresolved** — `PrismNotificationService` has access to `IPrismContext.CurrentTenant` (scoped) but no evidence of branding tokens flowing into email templates.
5. **`homePage.cshtml` is untyped** (`UmbracoViewPage` not `UmbracoViewPage<HomePage>`) — violates Brewster's charter rule #3.
6. **Dual auth-model fields on `PrismTenant`** — both Entra-specific (`EntraTenantId`, `EntraClientId`, `SecretKeyName`) and generic OIDC fields coexist in one model, increasing cognitive load for operators.

### Three Improvements Recommended (in priority order)

1. **Content isolation visibility** — tenant tag on content nodes + backoffice header indicator (affects `PrismContentTypeSeeder.cs`, `src/UmbracoPrism.Client/src/backoffice/`)
2. **Fix hardcoded `/dashboard` redirect** in `MemberDashboardController.cs` line 42 — use content-tree lookup
3. **Document and expose cache TTL** — add manual flush endpoint to `TenantManagementController.cs`; warn in `creating-a-tenant.md`

### Rams Scorecard Summary

✅ Innovative, Useful, Unobtrusive, Long-lasting, Environmentally friendly  
⚠️ Aesthetic, Understandable, Honest, Thorough, As little design as possible  
❌ None

---

---

**2026-05-01 — Prism Reflection Review (Rams 10 Principles)**

Delivered multi-tenancy editor lens review applying Rams principles. Four multi-tenancy findings recorded:
1. Content isolation is a known gap — needs roadmap item (tenantTag filter)
2. Hardcoded /dashboard redirect should be replaced with content-tree lookup
3. Tenant cache TTL gap needs operator documentation and flush endpoint
4. Email/push notification branding is unresolved (scope or wire)

No code changes — review-only. Decisions merged to decisions.md by Scribe. Orchestration log written to 2026-05-01T07:57:29Z-brewster.md.

**2026-05-02** — Completed: Implemented content-type validation so HTML/non-JSON responses are surfaced as errors instead of false-positive successes; preserved Blathers' backchannel transport fix; passed 653 core tests; commit da7ddc9 merged to main. Decision recorded in decisions.md.
## 2026-05-03: Team Spawn — Startup Helper Aspire Contract Alignment

**Status Update (Scribe):** Brewster fixed port-3000 startup helper to use current live Codespaces/AppHost contract. Codespaces public URLs from `gh codespace ports`, correct dashboard port, safe legacy endpoint handling. Artifact logging now repo-local (not `/tmp`).
