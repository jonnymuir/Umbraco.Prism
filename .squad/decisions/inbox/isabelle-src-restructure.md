# Decision: Split src/UmbracoPrism.Client/src/ into backoffice/ and mobile/

**Date:** 2026-04-02
**Author:** Isabelle (Frontend Dev)
**Status:** Implemented

## Context

All Lit components previously lived flat in `src/UmbracoPrism.Client/src/`. This mixed backoffice components (with `@umbraco-cms/backoffice` dependencies) and the mobile nav component (zero Umbraco deps). The mobile nav is loaded on every member-facing page view and must stay lean and dependency-free.

## Decision

Split into two subdirectories:
- **`src/backoffice/`** — all Umbraco back-office components + shared utilities (biometric-bridge, index.ts entry point, index.css)
- **`src/mobile/`** — `prism-mobile-nav.ts` and its story

Add an ESLint 9 flat config (`eslint.config.mjs`) with `no-restricted-imports` scoped to `src/mobile/**` that hard-errors on any `@umbraco-cms/backoffice` import.

## Rationale

- Architectural clarity: the `mobile/` directory can never accidentally gain Umbraco dependencies
- `biometric-bridge.ts` moves to `backoffice/` because it is only consumed by backoffice biometric components (`prism-biometric-register`, `prism-biometric-settings`) — it has no relevance on member-facing pages
- `assets/` stays at `src/` root (shared, currently only contains `lit.svg` which is not imported by any component)
- Output filenames (`prism-dashboard.js`, `prism-mobile-nav.js`) are unchanged — Razor partials load them by these exact names

## Impact

- Vite entry points updated
- Storybook glob (`../src/**/*.stories.*`) covers subdirectories automatically — no config change
- All relative imports between co-located files are unchanged (files that referenced each other moved to the same target directory)
