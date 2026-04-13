---
name: "playwright-aspire-restart-harness"
description: "Run real Playwright contracts against an Aspire stack when tests must restart the whole localhost application mid-run"
domain: "testing"
confidence: "high"
source: "earned"
---

## Context

Use this when Playwright coverage must hit the real localhost app and at least one scenario needs to stop and restart the full Aspire-managed stack during the test run.

## Patterns

### Give the live suite its own Playwright config

- Keep Storybook/component tests on their existing config.
- Create a dedicated config for the real-app suite with `workers: 1`, `ignoreHTTPSErrors: true`, and the real app `baseURL`.
- Make the default Storybook config ignore the live spec so routine component runs do not accidentally boot the whole stack.

### Let the test own AppHost lifecycle, not `webServer`

- Manage `dotnet run --project src/UmbracoPrism.AppHost` inside the test process (for example with a helper under `tests/support/`).
- Poll real readiness endpoints such as the Aspire dashboard, TestSite home page, seeded workflow hub page, Keycloak discovery, and a protected downstream API route before starting assertions.
- Use explicit stop/start helpers for restart scenarios; do not try to fake a restart with page reloads.

### Give restart-heavy suites an isolated runtime root

- Route the Aspire-hosted app's mutable runtime state (at minimum the SQLite DB and cookie key ring) to a repo-local artifacts folder instead of the app's normal dev database.
- Reset that isolated root only on the suite's first boot so every run starts from a clean slate, but mid-suite restarts keep the same state and can verify restart stability.
- In this repo, the localhost auth suite uses `artifacts/aspire/testsite-runtime/`.

### Require exclusive control when the suite owns restart semantics

- If a real-app suite needs deterministic restart behaviour, do **not** attach to an already-running AppHost on the default ports.
- Fail fast and ask for those ports to be free, so the suite can own the AppHost lifecycle and reset path from the first boot.
- This avoids inheriting a developer's stale database or cookie key ring and makes fresh-clone regressions reproducible.

### Keep failing restart behaviours as contracts

- If the pre-restart login/API/navigation contracts pass but restart-specific contracts fail, keep those tests as the desired product behaviour.
- Document the real failure mode from the live browser (`401 Unauthorized`, Keycloak logout error page, etc.) rather than weakening assertions to match it.

### Assert page-specific affordances after navigation

- When two signed-in pages share welcome copy or headings, do not use that shared text as the post-navigation readiness signal.
- Wait for a dashboard-only affordance such as `View Workflows` or `Call Mock Business App API` so routing regressions fail immediately and explainably instead of hanging on a later click.

## Examples

- Live suite: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- AppHost helper: `src/UmbracoPrism.Client/tests/support/live-app-host.ts`
- Dedicated config: `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`

## Anti-Patterns

- **Putting restart-heavy real-app tests under the Storybook config** — couples unrelated suites and breaks local ergonomics.
- **Depending on Playwright `webServer` for restart scenarios** — it can start a stack once, but it is not the right tool for controlled mid-test restarts.
- **Weakening a real restart regression to “page loads something”** — loses the behavioural contract the user actually asked for.
