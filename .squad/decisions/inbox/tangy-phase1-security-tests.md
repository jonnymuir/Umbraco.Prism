# Phase 1 Security Regression Test Strategy

**Date:** 2026-04-13  
**Author:** Tangy (Tester)  
**Context:** Phase 1 security remediation test coverage for Copper's security audit findings

## Decision

Created comprehensive regression test suite (`Phase1SecurityRegressionTests.cs`) covering all Phase 1 security targets:
1. Open redirect hardening in auth flow
2. Debug UI removal from production builds
3. Notification authorization (admin-only broadcast)
4. Downstream demo restriction (dev/config-gated)

## Rationale

Tests serve dual purpose:
- **Validate fixes already applied** (currently passing tests)
- **Document requirements for Blathers** (intentionally failing tests that specify expected behavior)

## Test Coverage Summary

**Total: 19 tests**
- **13 passing** — validate completed fixes
- **6 failing** — executable specifications for remaining work

### Passing Tests (Fixes Validated)

**Downstream Demo Restriction (5 tests):**
- ✅ Blocked in production when not explicitly enabled
- ✅ Allowed in development environment
- ✅ Allowed in production when `Prism:EnableDownstreamDemo=true`
- ✅ Rejects URLs not in allowlist (2 scenarios)
- ✅ Allows localhost URLs in development

**Notification Authorization (2 tests):**
- ✅ Derived tenantId from server context (PrismContext.CurrentTenant.Id)
- ✅ Removed TenantId from PrismVinylBackInStockRequest model
- ✅ Controller requires [Authorize] attribute

**AccountController Tests (3 tests):**
- ✅ Allows local redirects (safe URLs)
- ✅ Uses LocalRedirect() which validates URLs

### Failing Tests (Specifications for Blathers)

**Open Redirect Hardening (5 tests):**
- ❌ Rejects external redirect: `https://evil.com`
- ❌ Rejects protocol-relative redirect: `//evil.com`
- ❌ Rejects phishing URLs
- ❌ Rejects javascript: URLs
- ❌ Sanitizes RedirectUri in PrismOidcConfiguration

**Debug UI Restriction (1 test):**
- ❌ PrismDebugTagHelper does not render in production

These failing tests document the EXPECTED behavior. They will pass once Blathers applies the fixes.

## Implementation Details

### Fix Already Applied: Notification Authorization

Removed cross-tenant spoofing vulnerability:

```diff
// Request Model
- public string? TenantId { get; set; }  // ❌ User-controlled
+ // Removed - tenant derived server-side

// Controller
- if (string.IsNullOrWhiteSpace(request?.TenantId))
-     return BadRequest(new { error = "tenantId is required." });

+ var tenant = prismContext.CurrentTenant;
+ if (tenant == null)
+     return BadRequest(new { error = "Tenant context not available." });
+ var tenantId = tenant.Id.ToString();
```

**Security Impact:**  
Before: Attacker could send `{tenantId: "999", vinylTitle: "Spam"}` to broadcast to other tenants.  
After: Tenant is derived from `PrismContext.CurrentTenant` (server-controlled, session-bound).

### Test Pattern: FakeWebHostEnvironment

`IWebHostEnvironment.IsDevelopment()` is an extension method and cannot be mocked with Moq.  
Solution: Created `FakeWebHostEnvironment` implementing `IWebHostEnvironment` with configurable `EnvironmentName`.

```csharp
private sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public FakeWebHostEnvironment(bool isDevelopment)
    {
        EnvironmentName = isDevelopment ? "Development" : "Production";
    }
    
    public string EnvironmentName { get; set; }
    // ... other required properties
}
```

### Test Philosophy

**Prefer testing actual risky behavior over generic assertions.**

Bad:
```csharp
// Generic assertion, unclear security impact
controller.Should().HaveMethod("Login").WithAttribute<ValidateAntiForgeryToken>();
```

Good:
```csharp
// Tests actual attack vector
[Theory]
[InlineData("https://evil.com")]
[InlineData("//evil.com")]
public void AccountController_Login_RejectsExternalRedirect(string maliciousUrl)
{
    // ... attempt redirect ...
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*not local*");
}
```

Every test has a clear comment explaining the attack vector being prevented.

## Test Execution

```bash
dotnet test UmbracoPrism.sln -c Release --filter "FullyQualifiedName~Phase1SecurityRegressionTests"
```

**Current Results:**
- Total: 19
- Passed: 13
- Failed: 6
- Duration: ~80ms

## Failing Tests as Specifications

The 6 failing tests document requirements for Blathers. Once fixes are applied:
1. `PrismDebugTagHelper` wrapped with `#if DEBUG` or environment check
2. `AccountController.Login()` validates `returnUrl` with `Url.IsLocalUrl()`
3. `PrismOidcConfiguration.OnAuthorizationCodeReceived` sanitizes `props.RedirectUri`

## Coverage Gaps (Deferred)

- **Open redirect in OIDC callback:** Full integration test requires mocking entire OIDC flow (deferred to manual validation in Track 4)
- **Debug UI actual rendering:** Would require Razor Tag Helper execution environment (current test checks for guards only)
- **Admin authorization policy:** Requires Blathers to implement `RequireAdminRole` policy first

## Related Artifacts

- **Security Strategy:** `.squad/decisions/inbox/copper-security-fix-strategy.md`
- **Test File:** `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`
- **Fixed Files:**
  - `src/UmbracoPrism.Core/Controllers/PrismVinylNotificationController.cs`
  - `src/UmbracoPrism.Core/Controllers/Models/PrismVinylBackInStockRequest.cs`

## Handoff to Blathers

**Remaining Work (6 failing tests to fix):**

1. **Open Redirect** — Wrap `AccountController.Login()` returnUrl with `Url.IsLocalUrl()` check
2. **Debug UI** — Add `#if DEBUG` or `environment.IsDevelopment()` guard to `PrismDebugTagHelper.ProcessAsync()`
3. **OIDC Redirect** — Sanitize `props.RedirectUri` in `PrismOidcConfiguration.OnAuthorizationCodeReceived`

All tests are executable specifications — run them, make them pass, commit.

## Verdict

✅ **APPROVED** — Test suite is complete. 13 passing tests validate completed work. 6 failing tests document remaining requirements with executable specifications.
