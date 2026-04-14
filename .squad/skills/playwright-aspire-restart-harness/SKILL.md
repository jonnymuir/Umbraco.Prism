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

### Separate startup readiness from behaviour assertions

- Treat machine-readable startup probes as a distinct gate from user-behaviour assertions.
- In this repo, startup readiness is proved by the home ready marker, `/api/prism/downstream-demo/seed-contract-ready`, the workflow-hub auth challenge, Keycloak discovery, and MockBusinessApp reachability — not by whatever content happens to be visible first in the browser.
- After startup is ready, still assert the authored `href` before each click and wait for page-specific affordances after navigation. Shared signed-in copy is too weak to prove that a child route really settled.
- If a test needs to visit `/dashboard`, `/my-workflows`, or `/get-in-touch` directly, keep that as its own route-contract check rather than using it as the setup step for unrelated behaviour.

### Give restart-heavy suites an isolated runtime root

- Route the Aspire-hosted app's mutable runtime state (at minimum the SQLite DB and cookie key ring) to a repo-local artifacts folder instead of the app's normal dev database.
- Reset that isolated root only on the suite's first boot so every run starts from a clean slate, but mid-suite restarts keep the same state and can verify restart stability.
- In this repo, the localhost auth suite uses `artifacts/aspire/testsite-runtime/`.

### Require exclusive control when the suite owns restart semantics

- If a real-app suite needs deterministic restart behaviour, do **not** attach to an already-running AppHost on the default ports.
- Fail fast and ask for those ports to be free, so the suite can own the AppHost lifecycle and reset path from the first boot.
- This avoids inheriting a developer's stale database or cookie key ring and makes fresh-clone regressions reproducible.

### Check listener ownership, not just HTTP reachability

- For port-exclusive startup preflight, inspect listening PIDs rather than only probing `http(s)://localhost:{port}/`.
- In this repo, Aspire's resource-service port `22194` can be bound while not returning a meaningful HTTP probe, so HTTP-only checks can miss a stale stack and produce a confusing bind failure later.
- Include the occupied PIDs in the failure output so a blocked rerun is immediately attributable to a real leftover process.

### Allow a short port-drain grace period on rerun

- A managed `stop()` can take ~30 seconds to fully drain Aspire listeners even after the test process is done with the stack.
- Before failing a rerun as "ports already in use", poll for a bounded grace window so the suite can recover from its own just-finished shutdown path.
- Keep the grace period bounded; if listeners remain after that, still fail fast because exclusive ownership is part of the contract.

### Keep failing restart behaviours as contracts

- If the pre-restart login/API/navigation contracts pass but restart-specific contracts fail, keep those tests as the desired product behaviour.
- Document the real failure mode from the live browser (`401 Unauthorized`, Keycloak logout error page, etc.) rather than weakening assertions to match it.

### Assert page-specific affordances after navigation

- When two signed-in pages share welcome copy or headings, do not use that shared text as the post-navigation readiness signal.
- Wait for a dashboard-only affordance such as `View Workflows` or `Call Mock Business App API` so routing regressions fail immediately and explainably instead of hanging on a later click.

### Capture redirect chains when auth navigation fails

- For sign-in callbacks and dashboard clicks, temporarily record matching Playwright `response` events and include recent `status + url + Location` entries in the thrown error.
- This repo's current failure mode can present as `chrome-error://chromewebdata/` after `signin-oidc -> /dashboard -> /dashboard...`; the browser page is blank, but the response chain proves the real stall is a server-side self-redirect loop.
- Include a short body preview too, so empty error pages are distinguishable from real rendered HTML.

### Make readiness timeouts name the missing gate

- If the live AppHost readiness gate times out, report each readiness check with its latest HTTP status and missing header/body expectations.
- This separates "dashboard up but seed contract not converged" from "nothing is listening" without forcing the reader to infer state from raw AppHost logs alone.

### Distinguish IdP-upstream startup failure from browser-contract failure

- If the only missing readiness probe is Keycloak/OIDC discovery while TestSite, seed contract, workflow challenge, and downstream API probes are all green, treat the lane as blocked on IdP startup rather than on a Playwright auth assertion.
- In this repo, a decisive AppHost clue is `Aspire.Hosting.Dcp.dcpctrl.ServiceReconciler.Proxy` logging `Error handling TCP connection` for service `keycloak` with `connect: connection refused` to an ephemeral localhost port. That means the HTTPS proxy path is alive enough to try the upstream hop, but the Keycloak container itself is not yet accepting connections.
- The next fix belongs in the Aspire resource/readiness contract (for example, gating on real Keycloak HTTP health/discovery before dependent resources proceed), not in the browser assertions.

## Examples

- Redirect-loop diagnostic failure:
  - `302 /signin-oidc -> /dashboard`
  - repeated `302 /dashboard -> /dashboard`
  - final Playwright error: `net::ERR_TOO_MANY_REDIRECTS`

- Live suite: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- AppHost helper: `src/UmbracoPrism.Client/tests/support/live-app-host.ts`
- Dedicated config: `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`

## Anti-Patterns

- **Putting restart-heavy real-app tests under the Storybook config** — couples unrelated suites and breaks local ergonomics.
- **Depending on Playwright `webServer` for restart scenarios** — it can start a stack once, but it is not the right tool for controlled mid-test restarts.
- **Weakening a real restart regression to “page loads something”** — loses the behavioural contract the user actually asked for.
