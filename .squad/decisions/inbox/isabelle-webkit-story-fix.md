# Decision: Workflow editor host stories should select stages through the real graph UI

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Isabelle  
**Status:** Proposed  

For editor-host Storybook coverage, do not pre-seed stage selection by dispatching synthetic `stage-selected` events during story render. Drive selection through the graph’s real button interaction inside the story play function and wait on the resulting UI state, especially for the preview label shown in WebKit.

## Why

- The `Stage Selected` story in PR #75 was relying on a render-time custom event plus a fixed sleep, which made the assertion race the editor’s async preview refresh on WebKit.
- Clicking the actual graph stage exercises the same accessible path the component supports in production and proves the selected label survives the full host/editor update cycle.
- Waiting for the label to appear is a more honest contract than assuming 300 ms is always enough on every browser and CI runner.

## Consequences

- Future workflow-editor host stories should prefer user-visible controls over synthetic internal events when a stable accessible interaction already exists.
- When a story depends on async projected preview state, use an explicit wait on the rendered label or status instead of a fixed timeout.
- Keep this pattern focused on Storybook/test harness code; no product runtime change is needed when the underlying UI already behaves correctly.
