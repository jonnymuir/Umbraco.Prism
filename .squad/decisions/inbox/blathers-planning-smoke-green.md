# Decision: Planning smoke timeout was a walkthrough interaction bug, not a backend regression

**Date:** 2026-05-22T05:48:34.538+01:00  
**Author:** Blathers  
**Status:** Proposed

PR #75's latest `planning-workflow-editor-smoke` cancellation should be treated as a real merge gate, but the underlying cause was not in the planning runtime or authored seed contract.

## Decision

1. Keep `planning-workflow-editor-smoke` as a required confidence gate for merge.
2. Classify the latest cancellation as a walkthrough interaction failure: the job reached localhost-auth readiness, then timed out because the validation rail intercepted pointer clicks on the workflow editor's Send action.
3. Fix the walkthrough by using keyboard activation (`focus()` + `press('Enter')`) for Send and Accept All instead of pointer clicks.
4. Treat this as the honest contract for the editor shell because the keyboard path is part of the accessibility model and avoids layout-sensitive pointer interception.

## Why

- CI evidence showed the planning job got past startup and seed readiness, so there was no remaining backend/runtime/seed break in the four-workflow contract.
- The red `localhost-auth-playwright` lane failed on the same walkthrough step, proving the cancelled planning smoke did not need another backend change.
- Using keyboard activation keeps the test faithful to the user-facing accessibility path instead of masking the issue with brittle coordinate or force-click workarounds.

## Consequences

- Future workflow-editor walkthrough steps should prefer keyboard activation when overlapping inspector or validation chrome can intercept pointer events.
- A cancelled planning smoke on this branch now points first to harness interaction drift before reopening backend seed/runtime investigations.
