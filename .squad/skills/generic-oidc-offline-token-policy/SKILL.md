---
name: "generic-oidc-offline-token-policy"
description: "Use standard OIDC scopes for local browser sign-in; do not request offline tokens without an explicit feature need"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when a local or demo OIDC provider rejects `offline_access` during an interactive browser login, especially with Keycloak.

## Patterns

### Default to session-bound browser auth

- For local/demo browser sign-in, request standard OIDC scopes such as `openid profile` unless a product requirement explicitly needs offline sessions.
- Standard authorization-code flows can use normal session-bound refresh tokens; do not assume `offline_access` is required.

### Treat offline tokens as elevated capability

- `offline_access` is not just another scope; it enables longer-lived credentials that outlive the normal SSO session.
- Only enable it after reviewing revocation, rotation, logout, storage, and tenant-isolation consequences.

### Do not widen provider permissions to mask app-scope mistakes

- If the app only needs interactive login, fix the requested scopes instead of granting the demo user/client more power.
- For Keycloak demos, avoid assigning offline-token capability to the default repo-taker user just to unblock localhost auth.

### Preserve existing auth hardening

- Keep HTTPS, exact redirect URI pinning, nonce validation, issuer/audience validation, and tenant-bound signing-key rules intact.
- Never trade away token-validation controls to make local auth "easier".

## Examples

- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` sets the generic OIDC browser/token-exchange scopes to `openid profile` while leaving the Entra path unchanged.
- `keycloak/realm-export.json` removes `offline_access` from the local `prism-client` optional scopes so repo takers do not inherit unnecessary offline-token capability.

## Anti-Patterns

- **Enabling offline tokens by default for demo users** — creates longer-lived credentials without a justified feature need.
- **Using offline tokens to paper over refresh-flow design gaps** — hides architecture problems and expands blast radius.
- **Relaxing issuer/nonce/redirect checks during local auth debugging** — introduces security drift in the exact code path developers copy forward.
