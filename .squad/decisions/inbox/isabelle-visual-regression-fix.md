---
title: Stabilize workflow graph visual regression baselines across macOS and Linux
date: 2026-05-21T21:54:07.868+01:00
status: accepted
author: Isabelle
---

## Context

PR #75 introduced dedicated workflow graph screenshot baselines, but the `storybook-tests` GitHub Actions lane failed on Ubuntu even though the spec passed locally on macOS. The CI log showed the same deterministic mismatch on both retries: 8,174 differing pixels for the graph canvas screenshot and 19,017 for the list-mode screenshot.

## Decision

Keep the product UI unchanged and stabilize the visual test harness instead:

1. Launch the visual regression spec with Chromium `--font-render-hinting=none`.
2. Set `--uui-font-family: Arial, Helvetica, sans-serif` on the `prism-workflow-graph` host before taking screenshots.
3. Re-record the committed baselines from that stabilized harness output.

## Why

The regression was in screenshot determinism, not in the workflow graph UX itself. Using a metrics-stable fallback stack plus disabled font hinting keeps the baseline focused on intentional layout and styling changes rather than OS-specific text rasterization noise.

## Impact

- Future workflow graph screenshot updates should be recorded through the stabilized harness.
- Product rendering remains unchanged for real users.
- CI and local visual checks now share a more predictable capture setup.
