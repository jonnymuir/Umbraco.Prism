## 2026-04-13 — Generic OIDC downstream bearer validation

- Downstream APIs must validate generic OIDC access tokens against the same browser-facing issuer that Prism used for sign-in. For the local Keycloak demo, that issuer is `https://localhost:8443/realms/prism-dev`, not the container's internal `http://localhost:8080` URL.
- Generic OIDC client binding must accept either `aud` or `azp` because Keycloak-style access tokens often carry the calling client in `azp` while leaving `aud` as a shared resource such as `account`.
- In `PrismAuthExtensions`, optional `JsonWebToken` claim reads should use non-throwing claim enumeration instead of `GetClaim(...)` so missing `tid`/`azp`/`iss` values do not short-circuit valid generic OIDC fallback behavior.
