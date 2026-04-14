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

### Compare CI with local trust posture
- Local success plus `dotnet dev-certs https --check` finding a valid/trusted localhost certificate is strong evidence that the failure is environment-specific.
- GitHub Actions workflows that only run `actions/setup-dotnet` do not automatically prove the runner trusts the dev cert used by an in-process test server.

### Report the smallest credible hypothesis
- State that the product/auth logic is not yet implicated when the HTTPS handshake fails first.
- Recommend fixing CI trust or using an explicit test-only certificate/handler strategy before treating the failures as app regressions.

## Examples
- Failing workflow: `.github/workflows/ci-tests.yml`
- Failing tests: `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`
- HTTPS loopback host: `LoopbackOidcProvider.StartAsync()` at `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs`
- Token exchange call site: `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`

## Anti-Patterns
- Assuming an auth redirect regression just because all redirect-contract tests failed together.
- Weakening the assertions when the real problem is that CI never trusted the loopback HTTPS endpoint.
- Treating local pass + CI `UntrustedRoot` as flaky behavior without checking certificate setup.
