---
name: "keycloak-refresh-scope"
description: "Separate normal refresh-token scopes from Keycloak offline-token scopes in generic OIDC flows"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when Prism or another ASP.NET Core app supports both Entra and generic OIDC providers, especially for local Keycloak development. The same `offline_access` scope does not mean the same thing across providers.

## Patterns

### Do not copy Entra scopes into generic OIDC blindly

- Entra commonly uses `openid profile offline_access {clientId}/.default` for code exchange and refresh flows.
- Generic OIDC providers should not automatically inherit that scope string.

### Treat Keycloak `offline_access` as an explicit offline-token request

- In Keycloak, `offline_access` asks for an offline token, not just a normal session refresh token.
- That request can fail with `error=not_allowed` unless the realm, client, and/or user are configured for offline token use.

### Prefer standard scopes for fresh-clone local auth (with exceptions for restart-tolerant demos)

- For local Keycloak demo flows, request `openid profile` unless the app explicitly needs offline-token semantics.
- Standard confidential-client authorization code flow can still return a normal refresh token for the active session.
- **Exception:** The repo-owned localhost demo tenant (`localhost`, `prism-client`, `localhost:8443/realms/prism-dev`) requests `openid profile offline_access` to support full-stack restart scenarios in development. This is special-cased via `PrismOidcConfiguration.IsRepoOwnedLocalDemoTenant()`.
- Other generic OIDC tenants default to `openid profile` only to prevent production deployments from accidentally requesting long-lived refresh tokens without explicit product requirements.

### Make token storage tolerant of provider differences

- Generic OIDC callback handling should only persist `refresh_token` when the provider actually returns one.
- Do not fail the whole login exchange just because a provider omits a refresh token.

### Omit scope parameter from refresh calls when using offline_access tokens

- When the initial login requested `offline_access`, the refresh token is already bound to those original scopes.
- For Keycloak, restating scopes in the refresh call (especially without `offline_access`) can cause rejection.
- The correct pattern: return `null` from `GetRefreshScope()` for tenants using `offline_access`, which signals "omit the scope parameter entirely from the refresh request."
- For tenants that did **not** request `offline_access` initially, you can restate the requested scope in the refresh call.

## Examples

- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` now uses `GetRequestedScope(tenant)` so generic OIDC tenants request `openid profile`, while Entra tenants keep `openid profile offline_access {clientId}/.default`.
- `src/UmbracoPrism.Core.Tests/PrismOidcConfigurationTests.cs` asserts the scope split directly.
- `docs/ASPIRE_DEV.md` documents that the local Keycloak demo does not require offline-token grants.

## Anti-Patterns

- **Reusing Entra scope strings for all providers** — this is what triggered Keycloak's offline-token denial.
- **Fixing local auth by manually enabling offline tokens in Keycloak** — that hides the real bug and makes fresh clones harder.
- **Assuming every provider always returns `refresh_token`** — generic OIDC behavior varies.
