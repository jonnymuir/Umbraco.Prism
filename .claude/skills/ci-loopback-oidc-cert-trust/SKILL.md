---
name: "ci-loopback-oidc-cert-trust"
description: "Diagnose CI-only failures when localhost HTTPS OIDC test doubles use certificates the runner does not trust"
domain: "testing"
confidence: "high"
source: "earned"
---

## Context
Use this when Prism tests pass locally but fail in GitHub Actions while talking to a loopback OIDC provider hosted on `https://localhost`. In this repo, the failure shows up in redirect-round-trip security tests before any redirect assertions run.

## Patterns
### Look for transport failure before behavior failure
- If every case in the same OIDC-backed test group fails with `HttpRequestException` and inner `AuthenticationException: UntrustedRoot`, treat it as certificate trust first.
- When the stack trace points at `HttpClient.PostAsync(...)` in `PrismOidcConfiguration.OnAuthorizationCodeReceived`, the token exchange never completed, so redirect assertions are not authoritative.

### Check whether the test harness booted HTTPS localhost implicitly
- `LoopbackOidcProvider` uses `builder.WebHost.UseUrls($"https://localhost:{port}")`, which relies on Kestrel's development certificate.
- A bare `new HttpClient()` will validate that certificate against the machine trust store.

### Prefer transport-light loopback when TLS is not the assertion
- If the test's purpose is callback behavior (token exchange, discovery, nonce validation, cookie sign-in, redirect sink) rather than certificate trust, keep the loopback OIDC provider but serve it on `http://127.0.0.1`.
- This preserves executable regression coverage while removing the CI dependency on a trusted localhost development certificate.

### Mirror metadata HTTPS requirements to the authority scheme
- If Prism callback code performs OIDC discovery directly, create the `ConfigurationManager<OpenIdConnectConfiguration>` with an `HttpDocumentRetriever` whose `RequireHttps` flag matches the metadata URL scheme.
- This keeps real HTTPS authorities strict while allowing explicit HTTP loopback test doubles to fetch discovery metadata.

### Compare CI with local trust posture
- Local success plus `dotnet dev-certs https --check` finding a valid/trusted localhost certificate is strong evidence that the failure is environment-specific.
- GitHub Actions workflows that only run `actions/setup-dotnet` do not automatically prove the runner trusts the dev cert used by an in-process test server.

### On GitHub Ubuntu, `dotnet dev-certs https --trust` needs OpenSSL trust wiring
- If a GitHub-hosted Linux runner fails immediately in the certificate bootstrap step with exit code `4`, and the log says `$HOME/.aspnet/dev-certs/trust` must be listed in `SSL_CERT_DIR`, classify that as workflow setup, not app behavior.
- In that case the lane has not yet told you anything about Playwright, Aspire, Docker, or auth behavior; the trust bootstrap itself is the blocker.
- The next action is to export `SSL_CERT_DIR` so it includes `$HOME/.aspnet/dev-certs/trust` (alongside the system cert directories) before calling `dotnet dev-certs https --trust`, or use another explicit test-only trust mechanism.
- In GitHub Actions, persist that environment variable to later steps (for example via `$GITHUB_ENV`), because the localhost auth lane starts multiple .NET processes after the trust step and they also need the OpenSSL trust directory.
- In this repo, a workflow step like `echo "SSL_CERT_DIR=$HOME/.aspnet/dev-certs/trust:/etc/ssl/certs:/usr/lib/ssl/certs" >> "$GITHUB_ENV"` before the trust step is a reasonable minimal Ubuntu wiring.

### Add manual repro without widening automatic gates
- If maintainers need a safe way to rerun the localhost auth lane on demand, add top-level `workflow_dispatch:` to the same workflow that already has path-filtered `pull_request` and `push` triggers.
- This does not weaken the existing automatic gates; it only adds an explicit manual trigger for diagnostics or post-fix verification on a chosen ref.

### Bootstrap the full localhost lane explicitly in GitHub Actions
- For the real Aspire-backed auth/session lane in this repo, the minimal Ubuntu recipe is:
  1. `actions/setup-node` with Node `22.17.1`,
  2. `actions/setup-dotnet` with `.NET 10`,
  3. `npm ci` in `src/UmbracoPrism.Client`,
  4. `npx playwright install --with-deps chromium`,
  5. `dotnet dev-certs https`,
  6. export/persist `SSL_CERT_DIR` to include `$HOME/.aspnet/dev-certs/trust` plus the system cert directories, then run `dotnet dev-certs https --trust`,
  7. `node ../../scripts/validate-aspire-prereqs.mjs --localhost-auth-suite`,
  8. `npm run test:playwright:localhost-auth`.
- Prefer calling the existing npm script for the lane instead of duplicating AppHost start/stop behavior in YAML; the script already owns the prereq check and the Playwright config.

### After cert trust is fixed, reclassify single-probe Keycloak failures carefully
- If the GitHub Actions localhost auth lane gets past `dotnet dev-certs https --trust` and the prereq script, but `LiveAppHost.waitForReadiness()` times out with **only** the Keycloak discovery probe still failing while Aspire dashboard, TestSite, workflow seed-contract, and MockBusinessApp are all ready, do not keep treating it as a workflow bootstrap/certificate issue.
- In this repo, that pattern means the remaining defect is likely in the AppHost readiness contract for the Keycloak container/proxy path: Aspire may mark `/keycloak` ready before the forwarded HTTP endpoint is actually accepting traffic.
- Strong evidence is an Aspire proxy log like `Could not establish TCP connection to endpoint: dial tcp 127.0.0.1:<ephemeral-port>: connect: connection refused` immediately after `/keycloak` or `/keycloak-proxy` enters Ready state.
- Smallest next fix is to gate downstream startup/readiness on real Keycloak HTTP health/discovery, then rerun before changing auth/product code. Increase the Playwright/AppHost timeout only if logs later prove Keycloak is merely slow rather than falsely marked ready.

### Widen workflow path filters to the full Aspire auth graph
- If a workflow is meant to guard the real localhost auth lane, include more than `src/UmbracoPrism.Client/**` and `src/UmbracoPrism.Core/**`.
- In this repo, changes under `src/UmbracoPrism.AppHost/`, `src/UmbracoPrism.TestSite/`, `src/UmbracoPrism.MockBusinessApp/`, `src/UmbracoPrism.KeycloakProxy/`, `src/UmbracoPrism.Shared/`, `keycloak/`, and `scripts/validate-aspire-prereqs.mjs` can all break the lane and should trigger it.

### Preserve the callback contract while removing transport fragility
- In Prism redirect tests, the user-facing behavior under test is the callback-side cookie sign-in plus the final normalized `Response.Redirect(...)` target.
- The unnecessary dependency is CI trust of the loopback HTTPS transport, not execution of `PrismOidcConfiguration.OnAuthorizationCodeReceived` itself.
- Preferred mitigation order:
  1. keep the callback event and final redirect assertion under test,
  2. remove TLS-trust fragility with an explicit test-only transport/certificate strategy,
  3. avoid downgrading coverage to controller-only or `PrismReturnUrl.Normalize(...)` tests, because those do not exercise the callback sink that previously regressed.

### Report the smallest credible hypothesis
- State that the product/auth logic is not yet implicated when the HTTPS handshake fails first.
- Recommend fixing CI trust or using an explicit test-only certificate/handler strategy before treating the failures as app regressions.

## Examples
- Failing workflow: `.github/workflows/ci-tests.yml`
- Failing tests: `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`
- HTTPS loopback host: `LoopbackOidcProvider.StartAsync()` at `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`
- Token exchange call site: `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`
- CI-safe fix: switch the Phase 1 loopback provider to `http://127.0.0.1` and use `HttpDocumentRetriever(...){ RequireHttps = false }` for that metadata URL.
- Real localhost lane entry point: `src/UmbracoPrism.Client/package.json` script `test:playwright:localhost-auth`
- Prereq guard: `scripts/validate-aspire-prereqs.mjs`
- CI bootstrap location: `.github/workflows/ci-tests.yml`
- GitHub Actions bootstrap failure example: run `24415783660`, job `localhost-auth-playwright`, where `dotnet dev-certs https --trust` failed before Aspire startup because `SSL_CERT_DIR` did not include `$HOME/.aspnet/dev-certs/trust`

## Anti-Patterns
- Assuming an auth redirect regression just because all redirect-contract tests failed together.
- Weakening the assertions when the real problem is that CI never trusted the loopback HTTPS endpoint.
- Treating local pass + CI `UntrustedRoot` as flaky behavior without checking certificate setup.
- Keeping HTTPS in unit-test loopback harnesses when the suite does not care about TLS behavior and the only result is environment-dependent certificate trust failures.
- Adding a live auth lane to CI but leaving workflow path filters scoped to client-only files, which lets AppHost/TestSite/Keycloak changes bypass the gate.
- Re-implementing the localhost lane directly in YAML instead of invoking the repo-owned script and prereq checks.
