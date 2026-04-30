# Session: 2026-04-30 — SEC-004 Closure & Decisions Ledger Merge

**Agent:** Scribe  
**Date:** 2026-04-30  
**Status:** ✅ Complete

## Session Arc

V2 polymorphic component model post-launch security audit and remediation closure day:

1. **Security Review (Copper):** Full-stack audit post-v2.0 rollout identified 11 findings (SEC-001 through SEC-011). Top 3:
   - **SEC-001 (HIGH — PATCHED):** `WorkflowPollController` missing `[Authorize]` attribute
   - **SEC-002 (CRITICAL CVE):** `Microsoft.AspNetCore.DataProtection 10.0.0` advisory
   - **SEC-004 (HIGH — PATCHED):** Committed HMAC key in TestSite appsettings.json

2. **Code Remediation (Blathers):** Patched SEC-001, SEC-004, SEC-009, SEC-011; tested clean (547/547 passing)
   - SEC-004: Removed secrets from tracked files; wired `appsettings.Local.json` (gitignored) into Program.cs before Umbraco builder
   - Commit: `b6336fd`

3. **Pattern Decision (Blathers + Copper):** `appsettings.Local.json` over `dotnet user-secrets` because Umbraco's `IJsonSettingsEditor` writes the regenerated HMAC key directly to `appsettings.json` (not to user-secrets store). The Local.json pattern self-documents the bootstrap: dev creates it with the key, Umbraco sees a non-null value in the config chain and skips regeneration on subsequent runs.

4. **Scribe Work (Today):**
   - Merged Blathers' inbox decision (blathers-sec-004-fix.md) into decisions.md with full technical context
   - Updated security-review-2026-04-30.md: marked SEC-004 as CLOSED with implementation details and caveats
   - Cross-referenced SEC-004 closure in Copper's history.md
   - Created this session log summarizing the arc
   - Logged tracked-bin-outputs hygiene item (build artifacts in git — separate from SEC-004, not fixed today)

## Key Outcomes

**Security Posture:**
- 4/11 findings closed (SEC-001, SEC-004, SEC-009, SEC-011) ✅
- 2/11 patched but pattern work deferred (SEC-003 sanitiser design, SEC-006 cookie policy)
- 5/11 open (SEC-002 CVE, SEC-003, SEC-005, SEC-006, SEC-007, SEC-008, SEC-010)
- Phase1SecurityRegressionTests.cs: 547/547 passing (0 new failures from today's work)

**Decisions Recorded:**
- SEC-004 pattern decision: `appsettings.Local.json` → locked in decisions.md
- Rationale: Umbraco HMAC bootstrap flow incompatible with user-secrets

**Documentation Updated:**
- `.squad/security-review-2026-04-30.md` now has closure note for SEC-004 with caveat: committed value burned (in git history), but rotated key value matters going forward
- `.squad/agents/copper/history.md` cross-references SEC-004 closure
- `.squad/agents/blathers/history.md` entry already complete (written during remediation session)

**Hygiene Item Flagged (Not Fixed):**
- Tracked-bin-outputs: `src/UmbracoPrism.TestSite/bin/{Debug,Release}/net10.0/appsettings.json` are tracked in git (build outputs in source control — separate hygiene violation, low priority, deferred)

## Commits

All `.squad/` changes staged for single commit with Copilot co-author trailer.

**Files Modified:**
1. `.squad/decisions.md` — added SEC-004 decision entry
2. `.squad/security-review-2026-04-30.md` — updated SEC-004 status line + added closure details to SEC-004 section
3. `.squad/agents/copper/history.md` — added SEC-004 closure cross-reference entry
4. `.squad/log/20260430-sec-004-closure-and-decisions-merge.md` — this session log

**Next Priorities:**
1. SEC-002 (CRITICAL CVE) — Microsoft.AspNetCore.DataProtection upgrade required
2. SEC-003 (HIGH XSS) — sanitiser design + implementation
3. SEC-005 (HIGH CVE) — npm audit fix on client package.json

