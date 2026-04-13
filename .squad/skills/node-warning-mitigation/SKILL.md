---
name: "node-warning-mitigation"
description: "How to handle upstream-only Node deprecation noise without hiding real warnings"
domain: "tooling"
confidence: "high"
source: "earned"
---

## Context

Use this when a Node warning appears during local tooling startup (Playwright, Storybook, CLI wrappers) and the trace points into `node_modules` rather than project code.

## Patterns

- Reproduce the warning with the upstream tool started directly, outside any repo wrapper like Playwright `webServer`, before changing project config.
- Use `node --trace-deprecation <tool-entrypoint>` or an equivalent targeted trace to identify the exact package and call site.
- Decide whether the repo actually owns the root cause. If the trace lands in vendored tooling, prefer documenting the warning and keeping it visible unless a safe package upgrade or repo-controlled reconfiguration removes it.
- Only land a warning-related change when it is a real root-cause fix (for example, upgrading to a compatible package version or removing a repo-owned unsafe invocation).
- Validate the affected startup/test path after the investigation so the team knows whether the warning is harmless noise or coupled to a failing workflow.

## Examples

- Investigation path for Storybook DEP0190:
  - `src/UmbracoPrism.Client/package.json`
  - `src/UmbracoPrism.Client/playwright.config.ts`
  - `src/UmbracoPrism.Client/node_modules/@storybook/core/dist/common/index.cjs`
- Validation commands:
  - `cd src/UmbracoPrism.Client && npm run storybook -- --quiet --smoke-test`
  - `cd src/UmbracoPrism.Client && node node_modules/.bin/playwright test --reporter=line`

## Anti-Patterns

- Silencing the warning with `--disable-warning` or `--no-warnings` when the underlying package issue still exists.
- Editing vendored `node_modules` files to stop a warning.
- Blaming Playwright/webServer config before reproducing the warning with the upstream tool alone.
- Treating "warning still visible but workflow passes" as license to hide it instead of documenting the real blocker.
