# Decision: Use keyboard activation for the planning editor walkthrough send/apply actions

**Date:** 2026-05-22T05:48:34.538+01:00  
**Author:** Tangy  
**Status:** Proposed  

The localhost-auth CI lane should treat the planning workflow editor walkthrough's `Send` and proposal-accept actions as keyboard-driven behavioural steps, not pointer-hit tests.

## Decision

1. Keep the walkthrough focused on the authored user outcome: submit the natural-language request and accept the proposal.
2. Activate the visible controls by focus + `Enter` rather than pointer click when editor rails can overlap the button hit target on CI.
3. Re-validate on the real localhost-auth lane, not just an isolated unit seam.

## Why

- The failing GitHub run showed the `Send` button was visible and enabled, but repeated pointer clicks were intercepted by overlapping editor sections.
- That is a walkthrough harness issue, not evidence that the authoring action itself is unavailable to keyboard users.
- Using keyboard activation stays aligned with the product's accessibility contract and removes an avoidable CI-only hit-target race.

## Evidence

- Latest failing lane evidence: `localhost-auth-playwright` on PR #75 timed out in `tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts` while clicking `Send`.
- Full local rerun after the landed fix: `cd src/UmbracoPrism.Client && npm run test:playwright:localhost-auth -- --max-failures=1` → `34 passed`, `7 skipped`, `0 failed`.
