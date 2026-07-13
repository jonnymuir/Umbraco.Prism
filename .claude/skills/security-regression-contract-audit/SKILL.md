---
name: "security-regression-contract-audit"
description: "Audit failing security regressions by separating real product gaps from stale white-box placeholder tests"
domain: "testing"
confidence: "high"
source: "earned"
---

## Context

Use this when a security regression suite starts failing after the product or test harness has moved on, especially when the failures mix genuine vulnerabilities with documentary placeholder tests.

## Patterns

### Trace the whole security boundary

- Follow the user-controlled value from entrypoint to sink.
- In this repo, `returnUrl` safety is not just an `AccountController` concern; it continues through `AuthenticationProperties.RedirectUri` into `PrismOidcConfiguration.OnAuthorizationCodeReceived`.

### Separate behavioural contracts from source-shape assertions

- Keep tests that can prove what a user or attacker can actually trigger.
- Replace tests that only check comments, helper return values, or specific implementation patterns like `#if DEBUG`.

### Delete or replace placeholder tests that hardcode failure

- A test helper that always returns `false`, or a body that never invokes production code, is not a regression test.
- Preserve the underlying requirement, but rewrite the coverage around executable behaviour.

### Default/fallback safety is a runtime contract

- For redirect safety, cover malicious, null, and empty values explicitly.
- In C#, `value ?? "/"` does not normalize `""` or whitespace. If the safe default is root, tests should prove explicit canonicalization rather than assuming null-coalescing covers blank input.
- Assert the final redirect target or suppressed output, not the exact internal line of code used to get there.

## Examples

- `src/UmbracoPrism.Core.Tests/Phase1SecurityRegressionTests.cs` currently mixes:
  - a real open-redirect concern,
  - a fake null-coalescing assertion (`string.Empty ?? "/"`),
  - a debug-tag-helper check that hardcodes failure instead of executing `ProcessAsync`.

## Anti-Patterns

- **Calling a test “behavioural” while asserting `LocalRedirect` or `#if DEBUG` usage** — that is still implementation coupling.
- **Accepting a failing placeholder as proof of a bug** — the failure may only prove the test never exercised runtime behaviour.
