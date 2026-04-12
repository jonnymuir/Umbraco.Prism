---
name: "local-oidc-https-proxy"
description: "Fix localhost OIDC sign-in failures caused by secure cookie policy and mixed app/IdP schemes"
domain: "authentication"
confidence: "high"
source: "earned"
tools:
  - name: "bash"
    description: "Probe IdP headers, cookies, and local dev endpoints"
    when: "Use when reproducing localhost OIDC failures or comparing HTTP vs HTTPS IdP behavior."
---

## Context

Use this pattern when a local app runs on HTTPS but its OIDC provider is exposed to the browser on plain HTTP. Modern browsers, especially Safari/WebKit, can drop or refuse to send IdP auth-session cookies when the provider emits `Secure; SameSite=None` cookies on an HTTP origin, which often appears as an IdP-side `cookie not found` or restart-loop error after the login form submits.

## Patterns

### Prefer HTTPS for the browser-facing IdP origin

- Keep browser navigations to the IdP on HTTPS even in local development.
- If the IdP itself only listens on HTTP inside orchestration, front it with a local HTTPS proxy/endpoint instead of downgrading cookie policy.

### Preserve external scheme awareness

- When a proxy fronts the IdP, enable forwarded-header handling so the IdP builds issuer, login action, and cookie behavior from the external HTTPS scheme rather than the internal HTTP hop.
- For Keycloak in this repo, that means passing `--proxy-headers xforwarded` when AppHost fronts the container.

### Keep app config aligned with the browser origin

- Do not hardcode the IdP's internal container URL into browser-facing tenant config.
- Seed local tenant authority from the orchestrator-provided external URL (for example `KEYCLOAK_URL`) so redirects, issuer checks, and discovery all use the same origin the browser sees.

### Keep HTTP only for direct/internal use

- If an internal HTTP endpoint still exists for diagnostics or non-browser access, document it as non-browser/internal only.
- Browser auth should consistently use the HTTPS entry point.

## Examples

- `src/UmbracoPrism.AppHost/Program.cs` exposes Keycloak on HTTPS `8443`, keeps HTTP `8080` for direct access, and enables `--proxy-headers xforwarded`.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` builds `OidcAuthority` from `KEYCLOAK_URL` with a fallback to standalone `http://localhost:8080`.
- `ASPIRE_DEV.md` documents `https://localhost:8443` as the browser sign-in route.

## Anti-Patterns

- **Weakening realm security for browser HTTP** — changing the IdP to accommodate insecure browser traffic is a larger blast-radius change than adding a local HTTPS front door.
- **Hardcoding the internal container origin** — this creates issuer/base-url drift between the browser route and the app's configured authority.
- **Ignoring forwarded headers** — the IdP may continue emitting HTTP-based action URLs or cookie policy even when the browser entered over HTTPS.
