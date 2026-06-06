---
name: "web-component-removal-quality-gate"
description: "Safely remove an obsolete Web Component and all UI/test/docs references"
domain: "frontend-maintenance"
confidence: "high"
source: "observed (2026-05-30T09:11:01.656+01:00 conversation pane removal)"
---

## Context

Use this when deleting a Lit/Web Component from the client. Component removal is more than deleting the source file: stories, global custom-element typings, selectors, docs, and skipped tests can keep stale contracts alive.

## Procedure

1. Search the whole client tree for the tag name, class name, file stem, data hooks, and story id fragments.
2. Delete the component and its dedicated stories outright.
3. Remove imports, Storybook registrations, selector assertions, custom-element typings, CSS hooks, and docs references.
4. Delete tests that only exist for the removed component; rewrite incidental references so broader behavioural coverage remains.
5. Re-run the reference search until no matches remain.
6. Validate with `npm run build`, `npm run build-storybook`, and targeted Playwright specs for touched surviving tests.

## Review Heuristics

- Do not keep negative assertions for the deleted component unless the product explicitly needs a regression guard. They preserve the old contract in tests.
- Story ids derived from deleted story titles often appear in skipped tests; remove those tests rather than leaving permanently skipped dead paths.
- Update walkthrough prose to describe the new user journey, not just to remove the old component name.
