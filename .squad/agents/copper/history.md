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
