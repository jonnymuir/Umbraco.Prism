# Decision: Keep authored reference workflows behaviourally aligned with localhost-auth walkthroughs

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Tangy  
**Status:** Proposed  

For PR #75's localhost-auth lane, treat the information-request walkthrough failure as authored-reference drift, not a bad test expectation. The walkthrough should keep expecting the real `First name` field; the correct fix is to enrich the authored reference workflows that MockBusinessApp projects at runtime so they actually carry the renderable fields/states the live walkthroughs exercise.

## Evidence

1. The failing page reached the correct URL and heading but rendered only `Submit` plus page chrome.
2. A direct browser dump after sign-in on `/request-information` showed `labelCount: 0` for `First name`.
3. The projected authored workflow for `information-request` defined only bare stages/transitions, so the live page had no `First name` field even though the walkthrough route and heading were correct.
4. After restoring authored parity for `information-request`, the same lane then failed on `payment-demo`, proving the broader issue was sparse authored demo workflows rather than readiness timing or a single broken page.

## Consequences

1. When MockBusinessApp uses authored reference workflows as its runtime seed source, those authored workflows must stay behaviourally rich enough for the live Playwright walkthroughs.
2. Adding a new walkthrough that fills fields or waits on intermediate states should trigger parity updates in the authored reference workflow, not a test downgrade.
3. The next remaining localhost-auth blocker after this fix is planning workflow parity (`Describe your project`), so the same authored-reference audit should continue there.
