# Session Log: OIDC Signing Key Cold-Start Fix

**Date:** 2026-03-29  
**Time:** 13:53Z UTC  
**Issue:** IDX10500 — Cold-start 401 unauthorized  

## Summary

Fixed critical cold-start bug in OIDC signing key resolution where missing keys in cache caused 401 unauthorized errors. Replaced fire-and-forget async warmup with synchronous blocking fetch when cache is empty or key is missing.

## Changes

### Core Fix (Copilot)
- **File:** `src/UmbracoPrism.Core/Extensions/PrismAuthExtensions.cs`
- **Method:** `ResolveSigningKeys()`
- **Change:** Blocking synchronous fetch on cache miss
- **Guard:** Added `ContainsRequestedKey` validation

**Key Changes:**
```csharp
// Before: WarmAsync().ConfigureAwait(false) — fire-and-forget
// After: WarmAsync().GetAwaiter().GetResult() — blocking fetch
```

### Security Review (Copper)
✅ **Approved** — No blocking issues  
📋 **Recommendations:** 3 test coverage gaps identified

### Test Implementation (Tangy)
✅ **3 new tests** in `PrismAuthExtensionsSecurityTests.cs`
- Exception propagation in synchronous path
- Cold-start concurrency deduplication
- Case-insensitive tenant ID matching
- **Result:** 168/168 tests passing

## Impact

- **Severity:** High (auth failures)
- **Scope:** OIDC token validation
- **Risk:** Low (blocking fetch, straightforward logic)
- **Coverage:** 100% test pass rate

## Team Involvement

- **Copilot:** Bug fix and implementation
- **Copper:** Security review and recommendations
- **Tangy:** Test coverage and validation

---
**Status:** Ready for merge  
**Timestamp:** 2026-03-29T13:53:47Z UTC
