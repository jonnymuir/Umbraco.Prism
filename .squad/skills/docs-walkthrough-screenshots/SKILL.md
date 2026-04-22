---
name: "docs-walkthrough-screenshots"
description: "Create documentation screenshots against the real Aspire stack and store them under docs/images/walkthroughs with relative Markdown links"
domain: "documentation"
confidence: "high"
source: "user-guided"
---

## Context

Use this when documentation work needs fresh screenshots of the real Umbraco.Prism demo experience rather than static mockups.

## Patterns

### Boot the real stack through Aspire

- Start the environment from the repo root with:
  - `dotnet run --project src/UmbracoPrism.AppHost`
- Aspire publishes the local URLs for the running resources. For walkthrough capture, the important browser targets are usually:
  - TestSite: `https://localhost:44345`
  - Keycloak proxy: `https://localhost:8443`
  - MockBusinessApp: `https://localhost:7245`

### Use the Playwright MCP browser for screenshots

- Use the `@playwright/mcp` tool to drive the live localhost URLs exposed by Aspire.
- Prefer real browser navigation over static image generation so screenshots match the running product.
- Capture after page-specific readiness, not immediately after navigation.

### Reuse the existing live auth/session lane

- The real end-to-end suite lives under `src/UmbracoPrism.Client/tests/`.
- The main live auth/session contract is:
  - `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- The dedicated Playwright config is:
  - `src/UmbracoPrism.Client/playwright.localhost-auth.config.ts`
- The AppHost lifecycle/readiness helper is:
  - `src/UmbracoPrism.Client/tests/support/live-app-host.ts`
- The standard command to run that lane is:
  - `cd src/UmbracoPrism.Client && npm run test:playwright:localhost-auth`

### Follow the seeded login flow

- The local demo credentials and walkthrough bootstrap are documented in `ASPIRE_DEV.md`.
- Default seeded demo user:
  - `demo@prism.local` / `password`
- Use the existing Playwright tests to understand:
  - how the stack is started
  - how readiness is proved
  - how login is completed
  - which authored routes matter (`/dashboard`, `/my-workflows`, `/get-in-touch`)

### Screenshot storage standard

- Save walkthrough images to:
  - `docs/images/walkthroughs/`
- In Markdown, link screenshots with repo-relative paths from the document being edited.
- Prefer descriptive filenames tied to the walkthrough step, for example:
  - `docs/images/walkthroughs/planning-step-1-project-details.png`
  - `docs/images/walkthroughs/payment-demo-enter-details.png`

### Keep screenshots documentation-first

- Capture the full UI state needed by the narrative: heading, primary CTA, key fields, and any validation/help text being described.
- Avoid noisy browser chrome unless it helps explain the flow.
- Refresh screenshots whenever walkthrough copy changes materially.

## Examples

- Start stack:
  - `dotnet run --project src/UmbracoPrism.AppHost`
- Run live auth lane:
  - `cd src/UmbracoPrism.Client && npm run test:playwright:localhost-auth`
- Typical screenshot path:
  - `docs/images/walkthroughs/get-in-touch-check-answers.png`

## Anti-Patterns

- **Saving screenshots outside `docs/images/walkthroughs/`** — makes walkthrough assets hard to find and reuse.
- **Using absolute paths in Markdown** — breaks portability across branches, forks, and package consumers.
- **Capturing pages before Aspire/TestSite readiness settles** — produces flaky or misleading documentation.
