---
name: "test-discipline"
description: "Update tests when changing APIs — no exceptions"
domain: "quality"
confidence: "high"
source: "earned (Fenster/Hockney incident, test assertion sync violations)"
---

## Context

When APIs or public interfaces change, tests must be updated in the same commit. When test assertions reference file counts or expected arrays, they must be kept in sync with disk reality. Stale tests block CI for other contributors.

## Patterns

- **API changes → test updates (same commit):** If you change a function signature, public interface, or exported API, update the corresponding tests before committing
- **Test assertions → disk reality:** When test files contain expected counts (e.g., `EXPECTED_FEATURES`, `EXPECTED_SCENARIOS`), they must match the actual files on disk
- **Add files → update assertions:** When adding docs pages, features, or any counted resource, update the test assertion array in the same commit
- **CI failures → check assertions first:** Before debugging complex failures, verify test assertion arrays match filesystem state
- **Never use concrete `CancellationToken` values as Moq matchers for `HttpContext.RequestAborted`:** `DefaultHttpContext.RequestAborted` is lazily initialised via `IHttpRequestLifetimeFeature`; the token captured at setup-time can differ from the token used at call-time on Linux (CI/Ubuntu) even when both come from the same `DefaultHttpContext` instance. Always use `It.IsAny<CancellationToken>()` when the token is just being passed through.

## Examples

✓ **Correct:**
- Changed auth API signature → updated auth.test.ts in same commit
- Added `distributed-mesh.md` to features/ → added `'distributed-mesh'` to EXPECTED_FEATURES array
- Deleted two scenario files → removed entries from EXPECTED_SCENARIOS
- Moq setup for `RefreshAsync` uses `It.IsAny<CancellationToken>()` — not `httpContext.RequestAborted`

✗ **Incorrect:**
- Changed spawn parameters → committed without updating casting.test.ts (CI breaks for next person)
- Added `built-in-roles.md` → left EXPECTED_FEATURES at old count (PR blocked)
- Test says "expected 7 files" but disk has 25 (assertion staleness)
- Moq setup uses `httpContext.RequestAborted` — passes on macOS, fails on Ubuntu with NullReferenceException

## Anti-Patterns

- Committing API changes without test updates ("I'll fix tests later")
- Treating test assertion arrays as static (they evolve with content)
- Assuming CI passing means coverage is correct (stale assertions can pass while being wrong)
- Leaving gaps for other agents to discover
- Using concrete `HttpContext.RequestAborted` as Moq value matcher — platform-specific laziness causes silent mock mismatch
