---
name: "generic-oidc-logout-id-token-hint"
description: "Use the stored ID token as `id_token_hint` for RP-initiated logout with generic OIDC providers such as Keycloak"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when Prism or another ASP.NET Core app performs RP-initiated logout against a generic OIDC provider and the provider rejects logout redirects that include `post_logout_redirect_uri` without an `id_token_hint`.

## Patterns

### Keep logout hints session-bound

- Persist the provider-issued `id_token` only in the existing encrypted authentication cookie/auth properties.
- Reuse that token as `id_token_hint` during logout.
- If the provider still needs a client binding when the hint is missing, send `client_id` as a fallback.

### Preserve existing validation hardening

- Keep issuer, audience, nonce, HTTPS, and exact redirect URI checks intact.
- Do not loosen provider configuration to compensate for missing logout context.

### Minimize token exposure

- Treat `id_token_hint` as logout plumbing, not as a new persistence feature.
- Never move the ID token into browser storage, logs, URLs, or long-lived database records just to support logout.

## Examples

- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` stores `id_token` in auth properties and rehydrates it for generic OIDC logout.
- `src/UmbracoPrism.Core.Tests/PrismOidcConfigurationTests.cs` covers the `id_token_hint` path and the `client_id` fallback path.

## Anti-Patterns

- **Dropping the ID token after sign-in** — breaks RP-initiated logout for providers such as Keycloak.
- **Fixing logout by relaxing realm or redirect validation** — introduces security drift.
- **Persisting logout hints in localStorage or app tables** — expands token exposure without need.
