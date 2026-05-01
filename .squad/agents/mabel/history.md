# Mabel — History

## Project Seed

- **Project:** Umbraco.Prism — a syntax highlighting package for Umbraco CMS using Prism.js
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components/Storybook, Playwright, xUnit
- **User:** Jonny Muir
- **My scope:** Public-facing documentation — README, /docs/, marketplace listing, changelogs



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

## Session: Rams-Grade Docs & Onboarding Review (2026-05-01)

**Status:** ✅ Complete — Output at `.squad/reviews/2026-05-01-prism-reflection/06-mabel-onboarding.md`

**Task:** Deep review of the full documentation and onboarding surface through Dieter Rams' 10 principles, for 6 named personas: developers, content creators, designers, editors, business users, service designers.

### Findings

- **Developer:** Only persona with a clear entry door. README, umbraco-setup.md, authoring walkthrough all solid.
- **Content creators, editors, business users:** No door at all. Completely absent from docs surface.
- **Service designers:** Accidental door — end-user walkthroughs exist but pivot mid-step into OIDC/JSON implementation detail.
- **Designers:** Partial door via branding-design-system.md, but design-system walkthrough is skeletal (all screenshots TODO).
- **Walkthroughs:** 4 fully automated (community-enquiry, payment-demo, planning-notification, information-request); 5 skeletal (creating-a-tenant, design-system, push-notifications, building-a-mobile-app, authoring-a-workflow has narrative but no screenshots).
- **Honesty gap:** Five skeletal walkthroughs listed in README table without incompleteness markers.
- **Surfacing internal docs:** `docs/design/` (6 architecture decision docs) appear in public README navigation table — contributor-facing, not user-facing.
- **ASCII diagram violation:** `push-notifications.md` contains ASCII flow diagram; charter mandates Mermaid.
- **R5 compliance:** Not consistently applied across all walkthroughs.

### Three Prioritised Recommendations

1. Add "Start here by role" section to `README.md` — six bullet points, one per persona, with links.
2. Strip `docs/design/` entries from the public README docs table; replace with single contributor link.
3. Mark skeletal walkthroughs with `🚧 In progress` in `docs/walkthroughs/README.md`.

### Decisions Written

`.squad/decisions/inbox/mabel-docs-reflection.md`

---

## Session: PR #38 CI Green — v1.8.0 CHANGELOG + Workflow Regex Fix (2026-04-30)

**Status:** ✅ Complete — Commits `da5d29d`, `8809c64` on `fix/ci-green` (merged as `dc316fb` on main)

**Scope:** Add CHANGELOG entry for 1.8.0 release milestone and fix Squad Release workflow version guard.

### Changes

1. **CHANGELOG entry (commit `da5d29d`):**
   - Added `## [v1.8.0] — 2026-04-30` section
   - Consolidated security review findings (SEC-001 through SEC-011)
   - Listed feature additions, behavioral changes, fixes, and security CVE bumps

2. **Workflow regex fix (commit `8809c64`):**
   - Fixed `squad-release.yml`, `squad-preview.yml`, `squad-promote.yml`
   - Changed version check from `grep -q "## \[$VERSION\]"` to `grep -qE "^## \[v?$VERSION\]"`
   - Now accepts both `[1.8.0]` and `[v1.8.0]` formats

### Impact

- Release gate satisfied: CI version consistency check passes
- Squad Release/Preview/Promote workflows unblocked for all version formats
- Prevents future release regressions

### Architectural Note

The optional `v` prefix in release tags (`[v1.8.0]`) is documented but the gate didn't validate it. This was a silent hole — regex fix catches it for future releases.

---

## 2026-04-30: Full Documentation Review & v2 Schema Cleanup — COMPLETE

**Session:** Comprehensive documentation audit, v2 schema terminology cleanup, and walkthrough consolidation

---

## 📦 Archived Sessions (2026-04-22 and earlier)

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