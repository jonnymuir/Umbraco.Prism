---
name: "generic-oidc-downstream-session-binding"
description: "Validate Prism downstream sessions for generic OIDC tenants without assuming Entra tid claims"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when a Prism request is authenticated by the `PrismMemberCookie`, but backend code still thinks there is no Prism session for a generic OIDC tenant such as local Keycloak. In this repo, the cookie principal is created from the validated ID token, so tenant binding must follow the provider's claims model.

## Patterns

### Branch tenant binding by provider type

- If `CurrentTenant.EntraTenantId` is populated, keep the existing Entra isolation rule based on the user's `tid` claim.
- If `CurrentTenant.OidcAuthority` is populated, treat the session as generic OIDC and validate against OIDC-native claims instead.

### Use issuer plus client identity for generic OIDC

- Compare the principal `iss` claim to `CurrentTenant.OidcAuthority`, normalizing trailing slashes.
- Compare the tenant client id to either an `aud` claim or the `azp` claim, since providers like Keycloak can vary which claim carries the client identity.
- Only release downstream bearer tokens after those checks pass.

### Test the session gate directly

- Add unit tests around `PrismContext.GetAuthorizationHeaderAsync()` so the regression is caught before UI flows fail.
- Cover both a matching generic OIDC principal and a mismatched one.

## Examples

- `src/UmbracoPrism.Core/Models/PrismContext.cs` now branches between Entra `tid` validation and generic OIDC issuer/audience validation.
- `src/UmbracoPrism.Core.Tests/PrismContextTests.cs` covers the localhost Keycloak-style principal shape used by the TestSite demo tenant.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` is the reference generic OIDC tenant setup in this repo.

## Anti-Patterns

- Assuming every Prism session includes an Entra `tid` claim.
- Treating a valid generic OIDC cookie as anonymous just because `EntraTenantId` is null.
- Re-implementing provider-specific tenant binding separately in each controller or service.
