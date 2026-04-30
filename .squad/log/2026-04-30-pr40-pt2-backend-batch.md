# Session Log: PR #40 — PT2 Backend Security Batch

**Date:** 2026-04-30  
**Status:** ✅ Complete  
**PR:** #40 (merged; squashed as `83eb30e` on `main`)  
**Agent:** Blathers (Backend Specialist)

## Summary

Blathers shipped 5 security hardening commits closing five of eight open PT2 findings. Backend infrastructure now hardens logout (CSRF), security headers (CSP/HSTS/XFO/etc.), DataProtection key persistence, Capacitor JSON API antiforgery policy, and origin restrictions. Test count: 601 → 618 (+17 new, all passing).

---

## Findings × Commits Mapping

| Commit | Finding | Severity | Title | Changes |
|--------|---------|----------|-------|---------|
| `828b5d4` | SEC-PT2-003 | Medium | Logout-CSRF | `[HttpGet]` → `[HttpPost] + [ValidateAntiForgeryToken]`; Razor forms converted to POST; 3 reflection tests |
| `9f1f34e` | SEC-PT2-004 | Medium | Missing security headers | `PrismSecurityHeadersMiddleware` + `PrismSecurityHeadersOptions`; CSP Report-Only, HSTS, XFO, XCTO, Referrer-Policy, Permissions-Policy; `/umbraco/` excluded by default; 7 regression tests in `PrismSecurityHeadersMiddlewareTests.cs` |
| `6c0e8e9` | SEC-PT2-006 | Low | DataProtection ephemeral | `TestSiteRuntimeLayout.cs`: always call `PersistKeysToFileSystem` with fallback `{ContentRoot}/App_Data/prism-keys/`; keys no longer ephemeral; no compile-time breaking change |
| `7a3b0ef` | SEC-PT2-009 | Low | Antiforgery missing on JSON APIs | `[IgnoreAntiforgeryToken]` on `BiometricController`, `PrismNotificationController`, `PrismVinylNotificationController`; policy comments documenting deliberate exemption (native-app JSON endpoints); CSRF protection via SameSite=Lax + JSON Content-Type + origin checks; 3 reflection tests |
| `11b8cbb` | SEC-PT2-010 | Info | IsCapacitorOrigin accepts http://localhost | `IWebHostEnvironment` injected; `http://localhost` restricted to Development; `capacitor://localhost` always allowed; 3 CORS header regression tests |

**Test Impact:**
- Baseline: 601 passing
- After batch: 618 passing
- New tests: 7 (middleware) + 3 (logout) + 3 (CORS) + 4 other = 17 total
- Status: All green, no regressions

---

## Three Deferred Follow-Ups

### 1. CSP Enforcement (SEC-PT2-004)

**Current:** `Content-Security-Policy-Report-Only` (non-enforcing)

**Why deferred:** Umbraco backoffice + TestSite Razor views use inline scripts and styles (`@Html.Raw` CSS) that strict CSP would block. Enforcement requires:
- Audit of inline-script/style usage
- Nonce rollout plan
- Testing in Umbraco backoffice context

**Pattern:** Report-Only is a legitimate ship-now-tighten-later defense-in-depth pattern. Violations logged to report collector (configured via `report-uri` / `report-to`); deployment path clear for enforcement post-audit.

### 2. DataProtection Encryption-at-Rest (SEC-PT2-006)

**Current:** Keys persisted to filesystem (plaintext)

**Missing:**
- `ProtectKeysWith*` configuration (DPAPI on Windows, certificate, or Key Vault)
- Production guidance for consumers wiring Prism into their own host

**Why deferred:** Requires ops/infrastructure input; TestSite is local-only (low operational risk); Core library cannot mandate encryption without forcing consumers into a dependency choice.

### 3. Multi-Instance DataProtection (SEC-PT2-006)

**Current:** Each instance has its own isolated key ring

**Missing:**
- Azure Blob / Redis key ring providers
- Shared persistent location for cluster deployments

**Why deferred:** Requires infrastructure setup (Blob storage / Redis endpoint); seam documented for follow-up; TestSite development baseline doesn't need shared rings.

---

## Lessons Learned

### Report-Only CSP as Legitimate Pattern

Report-Only CSP is a standard defense-in-depth practice, not a compromise:
- Allows safe rollout without breaking existing inline scripts/styles
- Violations logged and monitored
- Upgrade path clear: nonce/hash rollout → promote to enforced CSP
- Industry standard (e.g., GitHub, Stripe use Report-Only during rollout)

**Takeaway:** Future security features that conflict with legacy patterns should consider Report-Only / observation modes as first-pass shipping strategy.

### Policy Comments Prevent Future "Fix" Reverts

Antiforgery exemptions on bearer-token endpoints (Capacitor JSON APIs) are intentional, but future reviewers may see `[IgnoreAntiforgeryToken]` and attempt to "fix" it by adding validation — breaking the mobile app.

**Solution:** Document the policy in code:
```csharp
[IgnoreAntiforgeryToken]  // Intentional: Capacitor native-app endpoint; no browser cookie jar
public async Task<IActionResult> Register([FromBody] BiometricRegisterRequest req)
```

**Takeaway:** Security exemptions need policy comments to prevent regressive "fixes." Pair with regression test naming that signals the policy (e.g., `BiometricController_IgnoresAntiforgery_ByDesign`).

### TestSite-Layer Configuration Sometimes Wins Over Core Defaults

DataProtection key persistence was fixed at the TestSite layer (`TestSiteRuntimeLayout`), not in `PrismComposer` (Core library).

**Why:** Core library cannot safely double-configure DataProtection — the host application may already own that config. TestSite-specific safety nets (fallback paths, environment checks) don't belong in a reusable library.

**Takeaway:** When hardening defaults, consider the composition boundary. Library-level changes can conflict with host assumptions; application-layer seams are sometimes the safer choice.

---

## Remaining Open PT2 Findings (Dispatched)

| Finding | Title | Severity | Assigned | Branch |
|---------|-------|----------|----------|--------|
| SEC-PT2-005 | Backoffice auth default scheme | Medium | Blathers | `sec/pt2-backoffice-test` |
| SEC-PT2-007 | Unsanitized `accordionSection.Content` in Razor | Low | Isabelle | `sec/pt2-razor-hardening` |
| SEC-PT2-008 | RTE field `@Html.Raw(description)` | Low | Isabelle | `sec/pt2-razor-hardening` |

---

## Files Changed Summary

**Core / Infrastructure:**
- `src/UmbracoPrism.Core/Controllers/AccountController.cs` — logout endpoint conversion
- `src/UmbracoPrism.Core/Controllers/BiometricController.cs` — `[IgnoreAntiforgeryToken]`, origin checks
- `src/UmbracoPrism.Core/Controllers/PrismNotificationController.cs` — `[IgnoreAntiforgeryToken]`
- `src/UmbracoPrism.Core/Controllers/PrismVinylNotificationController.cs` — `[IgnoreAntiforgeryToken]`
- `src/UmbracoPrism.Core/PrismComposer.cs` — `PrismSecurityHeadersMiddleware` registration, antiforgery policy documentation
- `src/UmbracoPrism.Core/Middleware/PrismSecurityHeadersMiddleware.cs` — NEW
- `src/UmbracoPrism.Core/Options/PrismSecurityHeadersOptions.cs` — NEW
- `src/UmbracoPrism.TestSite/TestSiteRuntimeLayout.cs` — DataProtection key persistence
- `src/UmbracoPrism.TestSite/Views/homePage.cshtml`, `memberDashboard.cshtml` — logout form conversion

**Tests:**
- `src/UmbracoPrism.Core.Tests/Middleware/PrismSecurityHeadersMiddlewareTests.cs` — NEW (7 tests)
- `src/UmbracoPrism.Core.Tests/Controllers/*Tests.cs` — 3 logout + 3 CORS + 4 other regression tests added

---

## Verification

```bash
# Build clean
dotnet build UmbracoPrism.sln -c Release
# Result: 0 errors

# Tests passing
dotnet test … --filter "FullyQualifiedName~UmbracoPrism.Core.Tests"
# Result: 618 passed, 0 failed, 0 skipped

# No vulnerable packages
dotnet list package --vulnerable --include-transitive
# Result: 0 vulnerable packages across all projects
```

---

## Artifacts

- **Decisions merged:** `.squad/decisions/inbox/blathers-pt2-backend.md` → `.squad/decisions.md` (2026-04-30 heading)
- **Blathers history:** `.squad/agents/blathers/history.md` appended with PR #40 session
- **This log:** `.squad/log/2026-04-30-pr40-pt2-backend-batch.md`

---

## Next Steps

1. **PR #40 closeout complete** — All bookkeeping recorded
2. **SEC-PT2-005 dispatch** → Blathers on `sec/pt2-backoffice-test` (backoffice auth integration test)
3. **SEC-PT2-007 + SEC-PT2-008 dispatch** → Isabelle on `sec/pt2-razor-hardening` (Razor @Html.Raw hardening)
4. **CSP enforcement follow-up** — Post-audit when inline-script/style nonce plan locked in
5. **DataProtection follow-ups** — Encryption-at-rest + multi-instance sharing seams documented for consumption

