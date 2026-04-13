---
name: "dev-session-contract-probe"
description: "Expose a dev-only auth session contract probe so end-to-end tests can distinguish broken cookies from stale downstream runtimes"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when a local auth flow can fail either because the app lost its server-side session contract or because a downstream dependency is stale. Browser tests need a deterministic way to tell those apart.

## Patterns

### Expose contract metadata, never raw tokens

- Return booleans and high-level metadata only: token presence, expiry presence, tenant mode, logout-hint readiness, and whether Prism can produce a downstream authorization header.
- Do not emit token strings, refresh tokens, ID tokens, or secrets.

### Gate probes to local/dev usage

- Keep the probe behind existing development-only/demo-only guards.
- If unauthenticated, still return a useful contract snapshot rather than forcing a redirect, so tests can assert that the cookie disappeared.

### Align the probe with the real backend contract

- For generic OIDC, report whether the cookie contains `access_token`, `refresh_token`, `id_token`, and `expires_at`.
- Report whether logout can restore `id_token_hint` and whether downstream bearer forwarding is available.

## Examples

- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` exposes `GET /api/prism/downstream-demo/session-contract`.
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` asserts both the authenticated and signed-out snapshots.

## Anti-Patterns

- Dumping raw cookies or tokens into a debug endpoint.
- Making Playwright infer session integrity indirectly from a downstream 401 alone.
- Disabling logout-hint or refresh-token requirements just to make flaky local tests pass.
