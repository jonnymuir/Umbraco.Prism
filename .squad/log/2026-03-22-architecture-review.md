# Session Log: 2026-03-22 — Architecture Review

**Who:** Tom Nook (Lead)  
**What:** Comprehensive architecture review of Umbraco.Prism core services, middleware, identity, persistence, and frontend integration  
**Date:** 2026-03-22

## Key Findings

### Strengths
- Stateless OIDC architecture elegantly supports horizontal scaling; no session affinity required
- Consistent naming conventions (IPrismXxx, XxxService, PrismXxxMiddleware) across codebase
- Secure secret handling via Azure Key Vault with 1-hour cache TTL
- Good test coverage in Core (XUnit, Moq, FluentAssertions)
- Clean separation of concerns (tenant resolution ≠ branding ≠ auth)
- Mobile feature well-isolated; can be reused standalone

### P0 Risks
1. **Blocking async in OIDC config** — `IssuerSigningKeyResolver` and `OnAuthorizationCodeReceived` use `.GetAwaiter().GetResult()` to call Azure synchronously; under load, creates bottleneck if CIAM endpoint slow (~500ms per request)
2. **Token refresh without retry** — No Polly retry logic for CIAM token endpoint; transient outages cause all refresh attempts to fail; users logged out on next page
3. **Authorization inconsistency** — `PrismTenantHandler` checks Azure tenant ID (Entra source of truth); `PrismAdminHandler` checks local Umbraco group membership; drift between systems causes unexpected 403 errors

### Scaling Concerns
- **Tenant cache:** 30-min TTL → ~50 DB queries/sec at expiry on active system
- **Branding Service:** CSS file scan on first call blocks request (100-500ms on monoliths)
- **Secret cache:** 1-hour TTL → first request to new tenant pays Azure latency (~100-200ms)
- **Scale ceiling:** ~1K cached tenants; 10K+ needs read replicas or tenant clustering

### OIDC Metadata Cache
- Static cache lives for app lifetime; CIAM key rotations require restart
- Need fallback on 401 (`kid` not found) to refresh metadata
- Recommend shorter TTL (e.g., 12 hours)

### Silent Failures
- Unknown tenant domain: middleware logs warning but continues with null tenant; risky (unclear intent)
- SecretVaultService returns empty string on 404; obscures auth failures

### Mobile Bundle Security
- No rate limiting on `/produce-mobile` endpoint (vulnerable to DoS)
- No validation that generated Capacitor.ts is syntactically correct
- Accepts arbitrary URLs (SSRF risk if app proxies)

### Test Coverage Gaps
- Missing OAuth redirect → token exchange → cookie set (happy path)
- Missing token refresh failure scenarios
- Missing OIDC key rotation tests
- Missing mobile bundle edge cases (special chars, concurrent generation)

## Decisions In Inbox

**3 decisions to review:**
1. Extract TokenRefreshService with Polly retry/circuit breaker (P0) — Owner: Blathers
2. Standardize authorization on Entra groups (P0) — Owner: Blathers
3. Explicit tenant rejection policy in middleware (P0) — Owner: Tom Nook

**Phase 2 decisions (High Impact):**
- OIDC metadata cache invalidation + shorter TTL
- Lazy-load BrandingService (move CSS scan from startup)
- Mobile bundle validation + rate limiting + same-domain StartUrl check
- Tenant cache pre-warming (background task)

**Phase 3 decisions (Technical Hygiene):**
- Structured logging (Seq, ApplicationInsights)
- Expanded test coverage (integration, chaos, edge cases)
- Transaction safety in tenant CRUD
- Appsettings schema validation

## Recommendations Summary

| Priority | Action | Owner | Status |
|----------|--------|-------|--------|
| P0 | Extract TokenRefreshService + Polly retry | Blathers | Inbox |
| P0 | Standardize auth on Entra groups | Blathers | Inbox |
| P0 | Document tenant rejection policy | Tom Nook | Inbox |
| P1 | OIDC metadata cache invalidation | Blathers | — |
| P1 | Lazy-load BrandingService | Blathers | — |
| P1 | Mobile bundle validation + rate limit | Blathers | — |
| P1 | Tenant cache pre-warming | Blathers | — |
| P2 | Structured logging | Blathers | — |
| P2 | Expand test coverage | Tangy | — |
| P2 | Transaction safety in CRUD | Blathers | — |
| P2 | Appsettings schema validation | Blathers | — |

## Handoff Summary

- **Isabelle:** Branding UI integration; ensure WCAG compliance for color overrides; document CSS class names
- **Blathers:** Token resilience + auth standardization (P0); then cache strategy + mobile security (P1)
- **Tangy:** Edge case test suite (unknown domains, token refresh failures, key rotation, concurrent updates)

