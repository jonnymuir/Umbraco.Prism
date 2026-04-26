# Orchestration Log — Blathers / Issue #2

**Date:** 2026-03-22  
**Agent:** Blathers  
**Issue:** #2 — Remove blocking OIDC calls from request path (async signing-key cache)  
**Outcome:** ✅ Build passing, 14/14 tests passing

---

## Summary

Blathers implemented an async-warmed signing-key cache to eliminate the blocking `.GetAwaiter().GetResult()` calls that occurred inside `IssuerSigningKeyResolver` on every token validation.

## Files Changed

| File | Change |
|------|--------|
| `src/UmbracoPrism.Core/Services/IPrismSigningKeyCache.cs` | **New** — Interface: `PreWarmAsync(entraTenantId)`, `TryGetKeys(entraTenantId, out keys)` |
| `src/UmbracoPrism.Core/Services/PrismSigningKeyCache.cs` | **New** — Singleton `ConcurrentDictionary`-backed implementation, 12h TTL, uses `IHttpClientFactory` named client `"prism-oidc-metadata"` |
| `src/UmbracoPrism.Core/Extensions/PrismOidcConfiguration.cs` | **Modified** — `PostConfigure` now reads from cache (sync, zero I/O); `async` network path moved to pre-warm only |
| `src/UmbracoPrism.Core/Middleware/PrismTenantMiddleware.cs` | **Modified** — `InvokeAsync` pre-warms signing-key cache immediately after tenant resolution, before pipeline continues |
| `src/UmbracoPrism.Core/PrismComposer.cs` | **Modified** — Registers `IPrismSigningKeyCache` as singleton; registers named `HttpClient` `"prism-oidc-metadata"` |
| `src/UmbracoPrism.Core.Tests/PrismTenantMiddlewareTests.cs` | **Updated** — Existing tests updated for new `InvokeAsync` signature (cache injected); no test count change |

## Key Decisions

- **`IssuerSigningKeyResolver` is a synchronous delegate** — there is no safe async escape. Pre-warming is the only correct pattern.
- **Warm-up site is `PrismTenantMiddleware.InvokeAsync`** — first async gate on every request; runs before any auth validation; tenant already in scope.
- **12h TTL** matches `ConfigurationManager` default; aligns with CIAM rotation expectations.
- **`IHttpClientFactory` named client** prevents socket exhaustion from `new HttpClient()` per refresh.

## Deferred

`PrismAuthExtensions.AddPrismAuthentication` downstream resolver retains sync-blocking pattern. Only blocks on cold-start first-request. Addressed in a future slice.

## Test Result

```
Test run for UmbracoPrism.Core.Tests
Passed: 14 / 14
Failed: 0
```
