# Decision: Workflow graph visual baselines follow GitHub Actions Linux

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Isabelle  
**Status:** Proposed  

For the workflow graph visual regression spec, treat the Ubuntu-rendered screenshots from the CI-equivalent Linux repro as the canonical baselines for the committed Playwright snapshots.

## Why

- PR #75 showed that the first harness stabilization removed the broad platform drift, but the committed images were still recorded on macOS and failed unchanged on GitHub Actions Linux.
- Local Linux reproduction matched the CI failure while preserving the same workflow graph structure, controls, and accessibility affordances, so this was a rendering-baseline mismatch rather than a product regression.
- The enforced PR lane runs on `ubuntu-latest`, so the checked-in snapshots need to reflect that environment until the project ships a fully bundled cross-platform font/rendering strategy.

## Consequences

- Refresh workflow graph screenshot baselines from the Linux CI-equivalent path when the visual spec changes.
- Do not treat macOS-only screenshot approval as sufficient evidence for workflow-editor visual stability on this lane.
- Keep the harness stabilization in place so future diffs are more likely to indicate real UI change instead of host-specific font rendering noise.
