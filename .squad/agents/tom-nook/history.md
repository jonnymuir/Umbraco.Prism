# Tom Nook — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Isabelle: Web Components, Storybook, Playwright UI tests
- Blathers: C# backend, services architecture, databases, auth
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory



## 📋 Recent Sessions

History trimmed for readability. Complete history in git.

---

- Defined five concrete answers: tenant fields, raw-secret boundaries, management API contract, demo resolution, documentation obligations
- Established secure-by-default properties (vault-backed for production, repo-owned demo marker)

**Key Learnings:**
- Reference-based secrets is the unifying pattern for multi-provider auth systems
- Inline secrets only acceptable for transient dev-only flows with clear repo ownership
- API contracts should reflect the security model: no secret echo, only metadata and provider state
- Fresh-clone experience and production security are not in conflict when the demo is explicitly tagged

**Status:** ✅ Complete; handed off to implementation team.

---

## Session: 2026-04-14 — Release v1.8.0 Semver Recommendation

**Role:** Lead/Release decision authority.

**Scope Reviewed:**
- 19 new feature commits (OIDC, Keycloak, mobile models, workflow refinements, Backoffice pickers)
- 6 security hardening commits (redirect validation, OIDC secret handling, auth flows)
- Multiple internal refactors (workflow architecture, test coverage)

**Semver Decision:** MINOR bump → v1.8.0
- New public features (OIDC provider fields, models, endpoints) justify minor
- All new fields optional; defaults graceful
- Security hardening is non-breaking (stricter validation on malicious inputs only)
- No breaking changes in public contracts

**Key Principle Applied:**
- New backward-compatible functionality = MINOR (semver.org)
- Stricter validation improves security without breaking legitimate contracts
- No user confirmation required; all changes forward-compatible

**Status:** ✅ Recommendation documented; awaiting bump execution.



## 2026-04-14: Release v1.8.0 — Semver Analysis & Lead Sign-Off

**Session:** Release orchestration (v1.7.1 → v1.8.0)

### Work Performed

1. **Commit Analysis** — Reviewed 92 commits since v1.7.1: ~20 feature, ~14 fix (including security), ~7 refactor, ~51 chore/docs
2. **Semver Recommendation** — MINOR bump justified: workflow forms engine (substantial feature), generic OIDC, mobile models, bearer token forwarding, media picker; no breaking changes
3. **Justification Documentation** — Provided detailed rationale: Why MINOR (not PATCH), Why not MAJOR, change composition breakdown
4. **Release Sign-Off** — High-confidence recommendation: Ready for tag creation and deployment

### Key Decisions

- **MINOR Bump (v1.7.1 → v1.8.0):** Workflow forms engine + multiple new features (OIDC, mobile models) exceed patch scope; no breaking changes support MINOR classification
- **Not PATCH:** Multiple user-facing features; patch reserved for bug fixes/tweaks
- **Not MAJOR:** No contract-breaking changes; all public APIs stable; new fields optional

### Outputs

- Decision records: `tom-nook-semver.md`, `tom-nook-semver-quick.md`
- Orchestration log: `.squad/orchestration-log/2026-04-14T16:55:12Z-tom-nook.md`

### Pattern for Future Semver Assessment

When assessing version bumps:
1. Count commits by type (feat/fix/refactor/chore) to gauge scope
2. Verify no breaking changes to public API contracts
3. Confirm all new features are backward-compatible
4. Document rationale for MAJOR/MINOR/PATCH choice per semver.org
5. High confidence in recommendation ensures smooth release
