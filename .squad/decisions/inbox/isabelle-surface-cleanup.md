# Isabelle decision — issue #81 workflow surface cleanup

- Date: 2026-05-25T09:54:48.365+01:00
- Issue: #81
- Scope: workflow-editor assignment and projection contract

## Decision

Treat actor and role gates as the only authoring source of truth for workflow assignment. The client should derive lane presentation from that assignment data, stop persisting `editorSurface`, and strip any legacy surface hint before project/publish requests. Validation issue links should return authors to the Canvas tab before focusing the affected inspector target.

## Why

Issue #81 is about removing duplicate surface rules before lane redesign. Keeping a second stored surface flag lets the editor drift away from the authored assignment contract, while hidden validation jumps make the contract harder for authors to trust during review.

## Consequence

Later lane work can reorganise visual groupings without changing the authored/runtime payload shape, and authors still get a reliable jump-to-item flow from validation findings.
