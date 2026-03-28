# Copper — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**User:** Jonny Muir

## Security Context

- Prism is multi-tenant and security-critical by design.
- User directive: prioritize confidentiality, integrity, and availability.
- Zero tolerance objective: no cross-tenant authentication leakage and no cross-tenant data leakage.
- OAuth must be implemented with tenant-safe boundaries; avoid single-tenancy caching assumptions common in generic MSAL-style designs.

## Learnings

- Entra-first authorization model migration is underway (#4 with child issues #8, #9, #10).
- OIDC and token refresh paths recently hardened (#2, #3) and require ongoing isolation-focused verification.
- Security reviews should include cache keying, token claim scoping, fallback behavior, and failure-mode isolation.
- Repeated unknown-`kid` tokens can trigger forced key-cache refresh loops unless refresh cadence is bounded; a short per-tenant cooldown materially reduces outbound metadata amplification DoS risk while keeping fail-closed signature behavior.

## 2026-03-22 — CIA Hardening Round 1

- Added strict tenant-binding in `PrismContext`: bearer token usage and refresh now require principal `tid` to match resolved `CurrentTenant.EntraTenantId`; mismatch returns null and blocks refresh.
- Added fail-closed guards in token refresh flow for missing tenant OIDC config (`EntraTenantId`, `EntraClientId`, `SecretKeyName`) and empty resolved vault secret.
- Hardened downstream JWT validation in `PrismAuthExtensions`:
	- Issuer must be a valid absolute URI with exact host/path bound to token `tid` (`{tid}.ciamlogin.com/{tid}/v2.0...`).
	- Audience must match the configured `ClientId` for the same token tenant (`tid`), preventing cross-tenant audience acceptance.
	- Signing keys are resolved only for configured tenant IDs.
- Added regression coverage for tenant mismatch and issuer/audience tenant-bound checks in core tests.
- Remaining availability risk: token refresh circuit breaker is still application-wide; outage/failure bursts from one tenant can contribute to shared breaker pressure for all tenants.

## 2026-03-22 — trycloudflare Dev Automation Security Review

- Reviewed `scripts/dev/start-trycloudflare.sh` from CIA + tenant-isolation perspective.
- Added stricter guardrails for local inputs: enforce valid TCP port range (1-65535) and GUID format for Entra app object ID.
- Enforced tunnel hostname trust boundary to `*.trycloudflare.com` before any redirect URI or tenant DB mutation is applied.
- Added clearer operator warning in script output that the flow is local-development only and mutates Entra + local tenant state.
- Added README security notes clarifying dev-only scope, least-privilege Azure access, and local/test database targeting to reduce blast radius.

## 2026-03-28 — Issue #7 Security Gate Review

- Re-reviewed OIDC key rotation fail-closed path in `PrismOidcConfiguration`: unknown or expired `kid` returns no keys synchronously while triggering background warm, preventing fail-open acceptance.
- Added cache test coverage proving forced-refresh cooldown is tenant-scoped (not global), preserving tenant isolation while reducing metadata amplification pressure under unknown-`kid` bursts.
- Added refresh stress coverage proving an open circuit on one token endpoint does not suppress concurrent refresh success on another endpoint.
- Added middleware cancellation coverage proving request-aborted warm operations rethrow `OperationCanceledException` and do not continue the pipeline.
- Security gate outcome for issue #7: pass-with-conditions; residual availability candidate remains downstream synchronous metadata retrieval path in `PrismAuthExtensions`.

## 2026-03-28 — PrismAuthExtensions Mitigation Security Gate

- Reviewed downstream auth key resolution and confirmed tenant allow-list plus tenant-bound issuer/audience checks remain intact in `PrismAuthExtensions`.
- Verified fail-closed behavior in `ResolveSigningKeys`: when the signing-key cache snapshot is expired or does not contain the requested `kid`, resolver returns empty keys and only triggers non-blocking background warm.
- Confirmed tenant isolation invariants remain intact:
	- Signing key lookup is tenant-scoped via tenant-id keyed cache entry and tenant allow-list gate.
	- Unknown tenant IDs return no keys.
	- Unknown/stale key paths fail closed in both downstream resolver and `PrismOidcConfiguration` snapshot path.
	- Background warm trigger in `PrismOidcConfiguration` does not bypass validation because resolver still returns no keys when cache is expired or requested key is absent.
- Updated security tests to target the current cache-snapshot resolver API shape in `PrismAuthExtensions`.
- Focused security tests (exact counts):
	- `PrismAuthExtensionsSecurityTests`: 5 passed, 0 failed.
	- `PrismSigningKeyCacheTests`: 5 passed, 0 failed.
	- `PrismOidcConfigurationTests`: 4 passed, 0 failed.
	- Total: 14 passed, 0 failed.
- Gate outcome: pass.

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.
