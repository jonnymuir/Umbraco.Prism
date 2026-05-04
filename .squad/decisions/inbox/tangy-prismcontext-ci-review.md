---
date: 2026-05-04T09:22:01.025+01:00
author: Tangy
status: ACCEPTED
area: testing, ci, moq
---

# Never Use Concrete CancellationToken Values as Moq Matchers for ASP.NET Core Contexts

## Context

CI run 25294216756 (commit `beef21c`) failed with 4 `PrismContextTests` throwing `NullReferenceException` at `PrismContext.cs:212`. The production code was unchanged and correct. The fault was entirely in the test setup.

Mock setups for `IPrismTokenRefreshService.RefreshAsync` used `httpContext.RequestAborted` as a concrete value matcher. On Linux (GitHub Actions, Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`. If that feature is activated by the authentication stack between setup-time and call-time, Moq's captured token value no longer matches the token in the actual call. Moq's loose mock returns `null` for the unmatched setup, causing `result.Success` to throw. On macOS (arm64) the lazy path is stable and the bug is masked.

## Decision

**When writing Moq setups for methods that accept a `CancellationToken`, always use `It.IsAny<CancellationToken>()` rather than a concrete `HttpContext.RequestAborted` or `httpContext.RequestAborted` value.**

Rationale:
- `DefaultHttpContext.RequestAborted` is lazily initialised through `IHttpRequestLifetimeFeature` and its behaviour can differ between platforms.
- The intent of tests like these is to verify routing logic and return values, not to assert the exact CancellationToken instance.
- Concrete value matching for CancellationToken is always fragile unless you own the token source and can guarantee stability.

## Implementation

Replace:
```csharp
.Setup(t => t.RefreshAsync(..., httpContext.RequestAborted, ...))
.Verify(t => t.RefreshAsync(..., httpContext.RequestAborted, ...), Times.Once)
```

With:
```csharp
.Setup(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...))
.Verify(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...), Times.Once)
```

Applied in commit `1601415` to four `PrismContextTests` methods.

## Blathers Review Note

The fix is entirely in test harness code. `PrismContext.cs` and `IPrismTokenRefreshService` are correct and do not require changes. Blathers does not need to act on this. The CI should pass once this commit is pushed.
