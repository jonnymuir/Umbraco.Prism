# Mabel — History Archive

## Archived Sessions (2026-04-22 and earlier)

Complete chronological history available in git. Recent summaries:

**Archived entries include:**
- Keycloak Local Dev Documentation Refactor (2026-04-14)
- GDS Phase 2 Interactive Walkthrough (2026-04-19)
- Workflow Documentation Rewrite (2026-04-21, 2026-04-22)
- Custom Field Types Documentation (2026-04-22)
- Walkthrough Screenshot Runbook (2026-04-22)
- Release v1.8.0 (2026-04-14)
- Security Hardening (2026-04-14, 2026-04-15)
- Redirect Hardening Sprint (2026-04-14)

**Access:** Full session details in git history; `.squad/decisions.md` for decisions.

---

## Session: Rams-Grade Docs & Onboarding Review (2026-05-01)

**Status:** ✅ Complete — Output at `.squad/reviews/2026-05-01-prism-reflection/06-mabel-onboarding.md`

**Task:** Deep review of the full documentation and onboarding surface through Dieter Rams' 10 principles, for 6 named personas: developers, content creators, designers, editors, business users, service designers.

**Summary:** Identified clear entry doors for developers only; found gaps for other personas. Three prioritized recommendations: add role-based starter section to README; strip internal docs from public table; mark skeletal walkthroughs with progress indicators.

---

## Session: PR #38 CI Green — v1.8.0 CHANGELOG + Workflow Regex Fix (2026-04-30)

**Status:** ✅ Complete — Commits `da5d29d`, `8809c64` on `fix/ci-green` (merged as `dc316fb` on main)

**Summary:** Added CHANGELOG entry for v1.8.0 with security review findings. Fixed Squad Release workflow version guard regex to accept both `[1.8.0]` and `[v1.8.0]` formats.

---

## Session: Codespaces Dashboard Port Documentation Fix (2026-05-03)

**Status:** ✅ Complete

**Summary:** Updated CODESPACES.md to clarify correct Aspire Dashboard URL (port 17214 HTTPS forwarded endpoint, not internal 15135 HTTP).

---

## Session: PR #49 Merge — AppHost Dynamic Backchannel Fix (2026-05-03)

**Status:** ✅ Complete — Merged as commit `a8e2d86`

**Summary:** Merged PR #49 with preserved history. Dynamic endpoint discovery via `businessApp.GetEndpoint("http")` now replaces hardcoded backchannel port assumption. Extracted `aspire-dynamic-endpoint-backchannels` skill for reuse.

---

## Session: Post-Merge Branch Reconciliation (2026-05-03)

**Status:** ✅ Complete

**Summary:** Cleaned branch divergence after PR #49. Kept `aspire-dynamic-endpoint-backchannels` skill (reusable pattern with test contracts). Staged Tom Nook's history and rebased feature branch cleanly onto main.

---

## Session: Final Push to Origin (2026-05-03)

**Status:** ✅ Complete

**Summary:** Pushed main branch to origin/main (commit `e1d54e7`). Cleaned up 9 merged feature branches (both remote and local).

---

## Session: Diagnostics Script Landing on Main (2026-05-03)

**Status:** ✅ Complete — Commit `926ca7a` pushed to origin/main

**Summary:** Landed `diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, and updated `CODESPACES.md`. Kept agent notes and `.playwright-cli/` untracked to maintain product/bookkeeping separation.

---

## Session: Diagnostics Runtime Fix — Landing to Main (2026-05-03)

**Status:** ✅ Complete — Commit `fb1b324` pushed to origin/main

**Summary:** Added Python runtime isolation for diagnostics script (`-I` flag, env var scrubbing, preflight validation). Users with other Python toolchains (Conda, Poetry, .venv) can now run diagnostics without `ModuleNotFoundError`.
