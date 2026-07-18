---
name: "manual-oidc-logout-id-token"
description: "Persist the OIDC ID token whenever Prism handles the code exchange itself so RP-initiated logout can send id_token_hint"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when Prism or another app bypasses the framework's default token redemption flow and manually exchanges the authorization code for tokens. If you only persist access/refresh tokens, browser login can work while RP-initiated logout later fails against providers such as Keycloak because the app has no `id_token` available for `id_token_hint`.

## Patterns

### Treat `id_token` as part of the session token set

- When manual code exchange succeeds, store `id_token` alongside `access_token`, `refresh_token`, and `expires_at` in the authentication properties written to the cookie.
- Do not assume `SaveTokens = true` will rescue you if you call `HandleResponse()` and take over the sign-in flow yourself.

### Backfill logout hints from the auth cookie

- During `OnRedirectToIdentityProviderForSignOut`, read the persisted `id_token` from the sign-in cookie if `context.ProtocolMessage.IdTokenHint` is still empty.
- This is especially important when custom Prism logic overrides the logout issuer address for generic OIDC providers.

### Keep logout callbacks registered with the provider

- Register the application's sign-out callback URI with the OIDC provider in addition to the sign-in callback URI.
- For the local TestSite + Keycloak flow in this repo, the callback is `/signout-callback-oidc`.

## Examples

- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` now creates the stored token set with `id_token` included and reuses that persisted token for Keycloak logout.
- `keycloak/realm-export.json` registers `http://localhost:9250/signout-callback-oidc` and `https://localhost:44345/signout-callback-oidc` so the local provider accepts the post-logout redirect.
- `src/UmbracoPrism.Core.Tests/PrismOidcConfigurationTests.cs` covers both token persistence and sign-out hint population.

## Anti-Patterns

- **Persisting only access/refresh tokens after manual redemption** — login succeeds, but RP-initiated logout breaks later.
- **Assuming the provider logout URL is wrong just because logout says `id_token_hint` is missing** — first verify the app actually kept the ID token.
- **Registering only `/signin-oidc` with the provider** — post-logout redirects can still fail even after the hint issue is fixed.
