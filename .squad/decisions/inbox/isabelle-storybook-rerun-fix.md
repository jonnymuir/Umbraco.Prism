# Decision: Workflow graph visual specs must ship their own test font

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Isabelle  
**Status:** Proposed  

For the workflow graph visual Playwright spec, do not rely on host system UI fonts or fallback stacks alone. The spec should embed its own test font, force that font through the component shadow root, and refresh the committed baselines from the Linux validation path that matches the enforced CI lane.

## Why

- PR #75 still showed Linux-only screenshot drift after the earlier font-stack stabilization because text was still being rasterized differently across renderers.
- The graph canvas and list workspace both settled once the same Linux environment generated the baselines, which confirmed the UI was stable and the remaining mismatch lived in screenshot rendering rather than product behaviour.
- Shipping the font inside the spec makes the visual lane less dependent on whatever system fonts happen to exist on a developer machine or runner image.

## Consequences

- Keep the embedded visual-test font and shadow-root font lock in `workflow-graph-visual.spec.ts`.
- Treat Linux-captured screenshots as the canonical baselines for this lane until the project has a broader shared screenshot-font strategy.
- When this spec changes, verify the visual lane from a Linux path before approving baseline updates.
