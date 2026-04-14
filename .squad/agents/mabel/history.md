# Mabel — History

## Project Seed

- **Project:** Umbraco.Prism — a syntax highlighting package for Umbraco CMS using Prism.js
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components/Storybook, Playwright, xUnit
- **User:** Jonny Muir
- **My scope:** Public-facing documentation — README, /docs/, marketplace listing, changelogs



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

- Created docs/secret-management.md for DevOps/SRE operational guidance
- All documentation aligns with implementation, security review, and contract

**Key Learnings:**
- Multi-level documentation is essential: architects need README; developers need ASPIRE_DEV; operators need dedicated guidance
- Field names and exact API contracts should be documented explicitly (not vaguely)
- Distinction between secret masking (no echo) and secret presence (HasOidcClientSecret) is subtle but important to explain

**Status:** ✅ Complete; admin knowledge gap closed.



## 2026-04-14: Redirect Hardening Sprint — COMPLETE

**Session:** Redirect Hardening Work (2026-04-14T12:39:42Z)

**Delivered:**
- Phase1 regression test failure diagnosis and cross-team assessment
- Comprehensive test execution guidance for redirect hardening sprint
- Identification of working tests vs. tests requiring focused remediation

**Key Outcomes:**
- Documented legacy Phase1 test failure patterns
- Provided targeted remediation guidance for behavior-based security contracts
- Cross-cutting assessment of Phase1 test suite against new auth hardening
- Test results: Phase1 passed; full Core suite passed; Playwright end-to-end green

**Orchestration Log:** `.squad/orchestration-log/2026-04-14T12:39:42Z-mabel.md`
**Session Log:** `.squad/log/2026-04-14T12:39:42Z-redirect-hardening.md`

**Team Consensus:** Security test diagnostics must be transparent and actionable for cross-team coordination.

## 2026-04-14: Security Hardening Commit — COMPLETE

**Task:** Commit redirect hardening work from active development branch.

**Delivered:**
- Scoped commit (SHA: `64419c6`) isolating security hardening files only
- Excluded unrelated TestSite auto-generated model changes
- Proper conventional commit message with security focus and test coverage notes
- Copilot co-author trailer included per team requirements

**Commit Contents (6 files):**
- `PrismReturnUrl.cs` (new) — callback-safe URL validation utility
- `AccountController.cs` (modified) — logout and callback endpoint hardening
- `PrismOidcConfiguration.cs` (modified) — explicit returnUrl validation support
- `AccountControllerTests.cs` (new) — 0-to-49 test coverage
- `PrismReturnUrlTests.cs` (new) — utility-layer test cases
- `Phase1SecurityRegressionTests.cs` (modified) — comprehensive regression coverage

**Left Unstaged (intentionally):**
- 5 TestSite auto-generated model changes (unrelated to security work)

### Release v1.8.0 (2025-01-15)

**Synchronized version surfaces:**
- Updated all version fields in lock-step: C# `.csproj` `<Version>`, Node `package.json` (both client and root `package.json`), and marketplace JSON `"version"` field
- Root `package-lock.json` carries minimal structure — no version update required
- Always bump versions before creating CHANGELOG entry

**CHANGELOG entry strategy:**
- v1.8.0 includes four sections: New Features (OIDC, Tenant API, Workflow/Forms, UI), Security Enhancements (JWKS/nonce, auth logging, Key Vault), Bug Fixes & Improvements (Android, scoping, modals, design system)
- Translated Tom Nook's feature summary into plain developer-friendly language with context for each item
- Omitted empty sections (breaking changes, known issues) per documentation standards
- Each bullet entry includes bold title, then explanation of "why it matters," then technical detail if relevant
- Maintained active voice and present tense throughout
- Multi-sentence bullets are acceptable when clarity requires it

**Style consistency note:**
- v1.7.1 used "Security Improvements"; v1.8.0 uses "Security Enhancements" to vary language and match "Bug Fixes & Improvements" pattern
- Settled on this grouping after reviewing prior release style — titles should differentiate sections clearly

**Pattern Observed:** When staging security-critical changes, it's important to isolate the scope and leave unrelated auto-generated files for separate handling—this keeps the git history clean and makes security audits easier.

## 2026-04-14: Release v1.8.0 — Technical Writer & Release Lead

**Session:** Release orchestration (v1.7.1 → v1.8.0)

### Work Performed

1. **Release Audit** — Comprehensive version alignment check across package.json, .csproj, marketplace.json, CHANGELOG
2. **Version Sync** — Updated 5 files: CHANGELOG (new v1.8.0 entry), Core.csproj (1.8.0), Client/package.json (1.8.0), root package.json (1.8.0), marketplace.json (1.8.0, fixed from stale 1.6.1)
3. **Changelog Preparation** — v1.8.0 section with three-part structure: New Features, Security Enhancements, Bug Fixes & Improvements
4. **Marketplace Sync Decision** — Fixed 2-version lag to prevent future confusion
5. **Security Commit Scoping** — Documented isolation pattern for redirect hardening work (commit 64419c6)

### Key Decisions

- **Three-Section Changelog:** New Features → Security Enhancements → Bug Fixes & Improvements; omit empty sections (no breaking changes or known issues for v1.8.0)
- **Marketplace Version Fix:** Synchronize all surfaces (1.6.1 → 1.8.0) to eliminate support confusion and marketplace stale-version display
- **Security Commit Scope:** Isolate security-critical changes in focused commits for auditability and backport clarity

### Outputs

- Decision records: `mabel-release-prep.md`, `mabel-release-notes-prep.md`, `mabel-release-prep-quick.md`, `mabel-commit-security-hardening.md`
- Orchestration log: `.squad/orchestration-log/2026-04-14T16:55:12Z-mabel.md`

### Pattern for Future Releases

When releasing, always:
1. Audit version consistency across all surfaces (including marketplace metadata that may lag)
2. Use three-section changelog structure when applicable (New Features, Security, Bug Fixes)
3. Isolate security-critical commits by scope for audit clarity
4. Fix stale metadata (e.g., marketplace version) during version bump to prevent future drift
