# SKILL: Antiforgery Exemption Policy Comment

**Author:** Scribe (on behalf of Blathers)  
**Date:** 2026-04-30  
**Source:** PR #40 (SEC-PT2-009) — Capacitor JSON API exemptions + lessons learned

## Overview

When exempting an endpoint from antiforgery validation via `[IgnoreAntiforgeryToken]`, document the policy reason in a comment immediately above the attribute. This prevents future reviewers from "fixing" the exemption — undoing an intentional security decision and introducing regressions.

## Problem

Antiforgery exemptions are legitimate for certain endpoint classes (bearer-token APIs, native-app clients, server-to-server flows), but a developer reading the code later may see `[IgnoreAntiforgeryToken]` without context and assume it's a bug:

```csharp
[IgnoreAntiforgeryToken]  // ❌ No explanation
public async Task<IActionResult> Register([FromBody] BiometricRegisterRequest req)
{
    // Mobile app may attempt to "fix" this by adding [ValidateAntiForgeryToken]
}
```

**Risk:** Innocent "fix" commits that break the mobile app and trigger regressions.

## Solution

Add a policy comment explaining why the exemption is correct:

```csharp
[IgnoreAntiforgeryToken]  // Intentional: Capacitor native-app endpoint; no browser cookie jar
public async Task<IActionResult> Register([FromBody] BiometricRegisterRequest req)
{
    // Native apps cannot supply the ASP.NET Core antiforgery cookie+header pair.
    // CSRF protection remains via bearer-token auth + origin checks.
}
```

## CSRF Protection Checklist for Bearer-Token Endpoints

When exempting from antiforgery, ensure at least one of the following holds:

- ✅ **Bearer-token or API-key auth** — request must include a token in the `Authorization` header (not in a cookie)
- ✅ **JSON-only `Content-Type: application/json`** — triggers CORS preflight for cross-origin browser requests
- ✅ **Origin checks** (`Origin` header validation, `IsCapacitorOrigin` helper, custom CORS policy)
- ✅ **`SameSite=Strict` cookie** — blocks all cross-site cookie sends (note: this blocks same-site forms, rarely used)

**Example: Capacitor JSON API (all three checks combined)**

```csharp
[IgnoreAntiforgeryToken]  // Intentional: Capacitor native-app endpoint
public async Task<IActionResult> Register([FromBody] BiometricRegisterRequest req)
{
    // CSRF protection via:
    // 1. Bearer-token auth (non-cookie credential)
    // 2. JSON Content-Type requirement (browser CORS preflight)
    // 3. IsCapacitorOrigin origin check (on unauthenticated endpoints)
}
```

## Naming Convention for Regression Tests

Pair exemptions with regression tests that explicitly signal the policy:

```csharp
[Fact]
public void BiometricController_Register_IgnoresAntiforgery_ByDesign()
{
    // Arrange
    var controller = new BiometricController(…);
    
    // Act: Verify [IgnoreAntiforgeryToken] is present
    var method = typeof(BiometricController).GetMethod("Register");
    var hasIgnoreAttr = method.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() != null;
    
    // Assert
    Assert.True(hasIgnoreAttr, "Capacitor endpoints intentionally bypass antiforgery; native apps cannot supply cookie+header pair.");
}
```

**Test naming rule:** `{Controller}_{Method}_IgnoresAntiforgery_ByDesign` — the `_ByDesign` suffix signals to future readers that this is intentional, not a TODO.

## Related Patterns

- **CSP Report-Only:** Similar "observation mode" pattern for defense-in-depth headers; document why enforcement is deferred
- **CORS exemptions:** Use `[AllowAnonymous]` + policy comment explaining why; pair with origin/bearer-token checks
- **HTTPS exemptions:** Document why a local-only endpoint is HTTP; include environment guard if needed

## Implementation Checklist

- [ ] Add `[IgnoreAntiforgeryToken]` attribute
- [ ] Add policy comment with reason (1–2 lines)
- [ ] Document CSRF protections in place (bearer-token, JSON Content-Type, origin checks, etc.)
- [ ] Add regression test with `_ByDesign` suffix
- [ ] Link decision record in PR description or commit message if policy is non-obvious

## Applicability

- **Backend:** ASP.NET Core controllers, MVC actions, API endpoints
- **Frameworks:** Any framework with CSRF exemption attributes
- **Scope:** Bearer-token APIs, native-app endpoints, server-to-server flows, webhook receivers
- **NOT for:** Legitimate bugs or oversights (these should be fixed, not documented as intended)

## Related Decisions

- **Source:** PR #40 — SEC-PT2-009 (Blathers)
- **Findings:** `BiometricController.Register`, `PrismNotificationController` (POST/DELETE), `PrismVinylNotificationController` (POST)
- **Test regression:** 3 new `_ByDesign` tests added; all passing

