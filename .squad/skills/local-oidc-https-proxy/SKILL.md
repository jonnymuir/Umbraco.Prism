---
name: "local-oidc-https-proxy"
description: "Require real TLS for localhost OIDC IdP routes; Aspire endpoint naming alone is not enough"
domain: "authentication"
confidence: "high"
source: "earned"
tools:
  - name: "bash"
    description: "Probe IdP headers, cookies, and local dev endpoints"
    when: "Use when reproducing localhost OIDC failures or comparing HTTP vs HTTPS IdP behavior."
---

## Context

Use this pattern when a local app runs on HTTPS and you want the browser-facing OIDC provider route to be HTTPS as well. Modern browsers, especially Safari/WebKit, can drop or refuse to send IdP auth-session cookies when the provider emits `Secure; SameSite=None` cookies on an HTTP origin, which often appears as an IdP-side `cookie not found` or restart-loop error after the login form submits. In Aspire, do not assume `WithHttpsEndpoint(...)` on an HTTP-only container gives you real TLS.

## Patterns

### Prefer HTTPS for the browser-facing IdP origin

- Keep browser navigations to the IdP on HTTPS even in local development.
- If the IdP itself only listens on HTTP inside orchestration, front it with a real local HTTPS reverse proxy or enable the IdP's native TLS instead of downgrading cookie policy.

### Verify transport, not just endpoint names

- Probe the advertised HTTPS route with `curl`/`openssl` before trusting it.
- In this repo, `WithHttpsEndpoint(port: 8443, targetPort: 8080)` on Keycloak's HTTP `start-dev` container exposed plain HTTP on port 8443, not TLS.
- Treat Aspire endpoint scheme metadata and browser-usable TLS as separate concerns.

### Preserve external scheme awareness

- When a proxy fronts the IdP, enable forwarded-header handling so the IdP builds issuer, login action, and cookie behavior from the external HTTPS scheme rather than the internal HTTP hop.
- For Keycloak in this repo, that means passing `--proxy-headers xforwarded` when a real HTTPS proxy fronts the container.

### Keep app config aligned with the browser origin

- Do not hardcode the IdP's internal container URL into browser-facing tenant config.
- Seed local tenant authority from the orchestrator-provided external URL (for example `KEYCLOAK_URL`) so redirects, issuer checks, and discovery all use the same origin the browser sees.

### Keep HTTP only for direct/internal use

- If only HTTP is actually available, document it honestly and seed `KEYCLOAK_URL` from the HTTP endpoint rather than a fake HTTPS origin.
- Browser auth should only use HTTPS once a real TLS listener exists.

## Examples

- `src/UmbracoPrism.AppHost/Program.cs` now seeds `KEYCLOAK_URL` from Keycloak's real HTTP endpoint because the previous fake HTTPS route was not TLS-capable.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` builds `OidcAuthority` from `KEYCLOAK_URL` with a fallback to standalone `http://localhost:8080`.
- `ASPIRE_DEV.md` documents the current HTTP-only limitation and the need for a real TLS proxy for Safari/WebKit-friendly browser auth.

## Anti-Patterns

- **Assuming `WithHttpsEndpoint(...)` means real browser TLS** — on an HTTP-only container, Aspire can still expose plain HTTP behind an `https` endpoint label.
- **Hardcoding the internal container origin** — this creates issuer/base-url drift between the browser route and the app's configured authority.
- **Ignoring transport verification** — if `curl https://...` fails but `curl http://...` succeeds on the same port, you do not have a usable HTTPS route yet.
