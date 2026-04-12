---
name: "keycloak-localhost-https"
description: "Fix Safari/WebKit localhost Keycloak cookie failures by keeping the browser-facing auth flow on HTTPS"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when local Keycloak login works in Chromium-like browsers but fails in Safari/WebKit after credential submit with Keycloak’s “Cookie not found” message.

## Patterns

### Reproduce in a WebKit-class browser

- Do not trust a Chromium-only repro for localhost Keycloak auth.
- Verify the behavior with WebKit/Safari because WebKit is stricter about localhost cookie handling.

### Keep the frontchannel HTTPS

- Keycloak 26 marks auth-session cookies as `Secure; SameSite=None`.
- Plain `http://localhost` frontchannel URLs can lose those cookies in WebKit/Safari.
- Fix the browser-facing authority by exposing Keycloak through an HTTPS proxy/endpoint instead of changing the redirect URI model.

### Keep Keycloak proxy-aware

- When Keycloak is behind a local HTTPS proxy, keep `--proxy-headers xforwarded` so generated OIDC URLs and callback metadata stay HTTPS-facing.
- Seed the localhost tenant from a browser-safe base URL such as `KEYCLOAK_URL` instead of hardcoding the container’s internal HTTP address.

### Check image-version flags

- Validate Keycloak runtime flags against the pinned container version before shipping them.
- In this repo’s `quay.io/keycloak/keycloak:26.0.0` image, `--server-async-bootstrap` is not supported even though newer Keycloak docs mention it.

## Examples

- `src/UmbracoPrism.AppHost/Program.cs` exposes Keycloak on both HTTP and HTTPS, injects `KEYCLOAK_URL` from the HTTPS endpoint, and passes `--proxy-headers xforwarded`.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` uses `KEYCLOAK_URL` for the seeded localhost tenant authority, falling back to plain HTTP only outside the AppHost flow.

## Anti-Patterns

- **Testing only in Chromium** — can miss Safari/WebKit cookie failures.
- **Using plain HTTP as the browser authority** — reproduces the missing-cookie login failure in WebKit/Safari.
- **Copying flags from newer Keycloak docs without version-checking** — can stop the pinned local container from starting.
