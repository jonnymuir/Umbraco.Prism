---
name: "generic-oidc-downstream-bearer-validation"
description: "Keep downstream API bearer validation aligned with generic OIDC issuers and Keycloak-style azp claims"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when a Prism downstream API receives bearer tokens from a generic OIDC tenant such as the local Keycloak demo. The browser sign-in may already work, but a second-hop API can still reject the forwarded token if its trusted issuer or client-binding logic does not match the actual token shape.

## Patterns

### Trust the same external issuer the browser used

- For local Keycloak in this repo, downstream services should trust the HTTPS proxy authority (`https://localhost:8443/realms/prism-dev`), not the container's internal `http://localhost:8080` URL.
- Keep Aspire overrides and standalone appsettings aligned so the same issuer is trusted in both launch modes.

### Bind generic OIDC access tokens by `aud` or `azp`

- Continue validating issuer, lifetime, and signing keys.
- For generic OIDC tenants, accept the client identity from either an `aud` claim or the `azp` claim because providers like Keycloak often put the calling client in `azp`.

### Read optional JsonWebToken claims safely

- In the ASP.NET Core JWT bearer pipeline, generic OIDC access tokens commonly arrive as `Microsoft.IdentityModel.JsonWebTokens.JsonWebToken`, not `JwtSecurityToken`.
- Do not call `JsonWebToken.GetClaim(...)` for optional claims like `tid` or `azp`; when the claim is absent it can throw and short-circuit validation with a 401 before the generic OIDC issuer fallback runs.
- Prefer enumerating `jsonWebToken.Claims` (or another non-throwing lookup) and then fall back to the token `Issuer` property when needed.

### Lock the behavior with validator-level tests

- Add tests around `PrismAuthExtensions` issuer and audience validators rather than relying only on UI repro steps.
- Cover the localhost HTTPS authority and an `azp`-only access token shape.

## Examples

- `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`
- `src/UmbracoPrism.Core.Tests/PrismAuthExtensionsSecurityTests.cs`
- `src/UmbracoPrism.MockBusinessApp/appsettings.json`
- `src/UmbracoPrism.MockBusinessApp/Properties/launchSettings.json`
- `src/UmbracoPrism.AppHost/Program.cs`

## Anti-Patterns

- Trusting Keycloak's internal container URL as the downstream issuer while the browser uses the HTTPS proxy.
- Requiring generic OIDC access tokens to expose the client id only in `aud`.
- Calling `JsonWebToken.GetClaim(...)` for optional generic OIDC claims and treating "claim missing" as an exception path.
- Debugging downstream 401s only from the UI without adding direct validator tests.
