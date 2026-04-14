# Tangy — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Tom Nook: Architecture, scope, code review, leadership
- Isabelle: Web Components, Storybook, UI logic, accessibility
- Blathers: C# backend, services, databases, auth
- Scribe: Session logging, decisions, team memory



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

## Tasks — 2026-04-13 — Dashboard Route Contract Validation (parallel spawn batch)

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T23:42:20Z-tangy.md`

**Spawned:** Brewster, Blathers, Tangy for parallel investigation of dashboard redirect behavior

**Task Summary:**
- Brewster: Confirm `/dashboard` route validity and auth challenge behavior ✅
- Blathers: Inspect auth/session redirect flow ⏳
- Tangy: Complete dashboard navigation trace and identify test readiness signals ✅

**Tangy Findings:**
- Identified that home page and dashboard both render `Welcome back, Demo User` heading
- This shared heading is NOT a safe readiness signal for dashboard tests
- Dashboard-only affordances: `View Workflows` and `Call Mock Business App API` are the correct test readiness signals
- If those elements never appear, report an app routing break rather than letting the test hang

**Decision Merged:** Consolidated findings into `.squad/decisions.md` under "📌 2026-04-13: Brewster — Dashboard Route Contract" with sub-section "Tangy — Dashboard navigation trace"

**Contract Impact:**
- Keep desired user contract: signed-in members should reach `/dashboard` and see dashboard-only actions
- In Playwright helpers, treat `View Workflows` and `Call Mock Business App API` as readiness signals
- Report app routing breaks when dashboard-only elements do not appear

## Learnings — 2026-04-14 — Restart API call diagnostics

- Enhanced callBusinessAppApi helper with detailed error diagnostics to expose the actual API response when the 200 OK assertion fails.
- The failing test 'signed-in member can still call the mock business app API after the whole stack restarts' now shows clear failure mode: **401 Request Failed** with message **Your Prism session is no longer valid. Sign in again, then retry the call.**
- The behavioural contract violation is specific: after a restart, the frontend auth state persists (user can access home page, dashboard, and see their profile), but the backend Prism session for downstream API calls is lost.
- Adding await expectSignedInHome(page) before the API call confirms the user is still logged in from the frontend perspective, isolating the failure to the downstream bearer token contract.
- This diagnostic improvement provides actionable signal for Blathers: the restart-stale session detection is working for the frontend, but the downstream API bearer token is not being refreshed or reestablished after restart.
- Test suite now runs reliably with 5/8 passing; the 3 restart-related tests remain red as expected until Blathers lands the downstream refresh fix.

## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Rewrote stale Phase1 regression tests into behavior-based security contracts
- Converted legacy test patterns to modern contract-driven testing
- Comprehensive Phase1 regression test audit and remediation guidance
- Validation: Phase1 tests passed; full Core suite passed; Playwright end-to-end green

**Key Outcomes:**
- Security tests now assert runtime behavior with executable harnesses
- External destinations blocked; safe local destinations round-trip verified
- Missing state falls back safely; production debug output renders nothing
- Avoid source inspection helpers and inert expressions for security regressions

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-tangy.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** Security contracts must be behavior-driven with executable test harnesses.


## 2026-04-14: Release v1.8.0 — Pre-Deployment Validation

**Session:** Release orchestration (v1.7.1 → v1.8.0)

### Work Performed

1. **Solution Build Verification** — `dotnet build UmbracoPrism.sln` passed; no errors, no new warnings
2. **File Consistency Check** — Verified version sync across all 5 release files (CHANGELOG, .csproj, package.json×2, marketplace.json)
3. **Integration Validation** — Frontend artifacts generated, backend NuGet metadata correct, marketplace synchronized
4. **Readiness Assessment** — All pre-deployment checks passed; release ready for git tag creation

### Key Verifications

- ✅ Solution builds cleanly (dotnet build UmbracoPrism.sln)
- ✅ Version consistency: 1.8.0 across CHANGELOG, .csproj, package.json (root + client), marketplace.json
- ✅ No orphaned version references
- ✅ package-lock.json regenerated cleanly
- ✅ No build artifacts require regeneration before deployment
- ✅ Release date correctly set (2026-04-14)

### Outputs

- Orchestration log: `.squad/orchestration-log/2026-04-14T16:55:12Z-tangy.md`

### Pattern for Future Validation

Pre-deployment validation should:
1. Build the entire solution to catch any compile errors
2. Verify version strings match across all deployment surfaces
3. Confirm no orphaned references to old versions
4. Validate marketplace/CDN metadata in sync with package versions
5. Generate clean build artifacts with no warnings introduced
