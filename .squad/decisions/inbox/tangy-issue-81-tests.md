# Tangy decision — issue #81 behavioural tests

- Date: 2026-05-25T09:54:48.365+01:00
- Issue: #81
- Scope: workflow-editor behavioural contracts

## Decision

For the workflow surface cleanup, keep behavioural coverage anchored to author-visible contracts instead of internal surface enums. Preview tests should assert the selected stage, read-only runtime copy, and assignment language; lane/list tests should assert visible lane labels and role-first navigation rather than exact `front-stage` / `back-stage` implementation details.

## Why

Issue #81 removes duplicate surface rules before later lane work. Internal surface naming and decomposition can legitimately move during that cleanup, but authors still care about the same outcomes: which lane they are in, what the preview shows, and whether the editor remains navigable.

## Consequence

Future UI refactors can simplify or merge surface plumbing without forcing noisy test rewrites, while regressions that change author-visible guidance should still fail fast.
