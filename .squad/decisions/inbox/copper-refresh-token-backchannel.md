# Decision: Route OIDC token refresh through backchannel in Codespaces

**Date:** 2026-05-02  
**Author:** Copper (Security Engineer)  
**Status:** Implemented — PR #44 (draft), branch `fix/codespaces-401-downstream-auth`, commit `e0e8ee3`

---

## Problem

`PrismContext.RefreshTokenAsync` POSTed the refresh-token grant to the **public** Codespaces Keycloak URL (`https://{name}-8443.app.github.dev/realms/prism-dev/protocol/openid-connect/token`). The GitHub port-forwarding proxy rejects unauthenticated server-side calls to this URL with `HTTP 401 / www-authenticate: tunnel`. Once access tokens expire in a Codespaces session, every downstream API call would 401.

The existing `KEYCLOAK_BACKCHANNEL_URL` mechanism already solved this for OIDC discovery document fetches and the initial login token exchange — token refresh was the remaining gap.

## Decision

When `KEYCLOAK_BACKCHANNEL_URL` is set **AND** `ASPNETCORE_ENVIRONMENT == Development`, rewrite the `tokenEndpoint` from the public OidcAuthority URL to the internal backchannel URL immediately before `IPrismTokenRefreshService.RefreshAsync` is called.

This rewrite is **transport only**. The returned tokens are validated with the same strict issuer/audience rules against the public OidcAuthority. The rewrite does not affect token trust, signing key validation, or tenant binding.

## Implementation

**File:** `src/UmbracoPrism.Core/Models/PrismContext.cs` — `RefreshTokenAsync`

```csharp
var backchannelBase = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
var isDevelopment = string.Equals(
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
    "Development",
    StringComparison.OrdinalIgnoreCase);
if (isDevelopment && !string.IsNullOrEmpty(backchannelBase))
{
    var oidcPath = new Uri(CurrentTenant.OidcAuthority!.TrimEnd('/')).AbsolutePath.TrimEnd('/');
    tokenEndpoint = $"{backchannelBase.TrimEnd('/')}{oidcPath}/protocol/openid-connect/token";
    Console.WriteLine($"[PRISM] RefreshTokenAsync: rewriting token endpoint to backchannel → {tokenEndpoint}");
}
```

Guard reads `ASPNETCORE_ENVIRONMENT` directly (rather than injecting `IWebHostEnvironment`) to preserve the existing 3-parameter primary constructor and avoid breaking any of the 631 existing tests.

## Bedrock Guarantees

- ❌ NO `RequireHttpsMetadata = false`
- ❌ NO `ValidateIssuer = false` / `ValidateAudience = false`
- ❌ NO `IsPrincipalBoundToCurrentTenant` relaxation
- ❌ NO `ServerCertificateCustomValidationCallback => true`
- ❌ NO suffix-trust of `*.app.github.dev`
- ❌ NO Development-only "skip tenant binding" branch
- ✅ Rewrite gated by BOTH `KEYCLOAK_BACKCHANNEL_URL` AND `IsDevelopment`
- ✅ Issuer/audience validation on refreshed tokens remains strict
- ✅ Production startup guards at `MockBusinessApp/Program.cs:38-41` and `TestSite/Program.cs:29-31` untouched (throw if env var set in non-Development)

## Why `ASPNETCORE_ENVIRONMENT` not `IWebHostEnvironment`

The startup-level throw at both `MockBusinessApp/Program.cs` and `TestSite/Program.cs` already ensures `KEYCLOAK_BACKCHANNEL_URL` can never be present in non-Development environments. The `ASPNETCORE_ENVIRONMENT` check in `RefreshTokenAsync` adds belt-and-suspenders defence without requiring a constructor signature change that would break all existing test instantiations.

## Consistency with Existing Patterns

This follows the same guard pattern used in:
- `PrismOidcConfiguration.cs` lines 291–296 (initial token exchange backchannel rewrite)
- `PrismOidcConfiguration.cs` lines 395–405 (JWKS backchannel fetch)
- `PrismAuthExtensions.ResolveSigningKeys` lines 233–236 (signing key metadata fetch)

## Follow-up

This PR is intentionally draft. Two further commits required before merge:
1. **Blathers:** JWKS fetch rewrite — `PrismSigningKeyCache.WarmAsync` must also rewrite `jwks_uri` through the backchannel
2. **Tester:** Regression tests covering the refresh-token backchannel rewrite
3. **Copper:** Final security review of all three commits before merge
