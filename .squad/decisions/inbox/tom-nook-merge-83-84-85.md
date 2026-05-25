---
author: tom-nook
date: 2026-05-25T15:34:44.680+01:00
status: proposed
area: workflow-multi-lane-backlog
---

# Decision: Merge issues #83, #84, and #85 into one gateway/runtime track

## Context

Jonny asked to stop treating issues #83, #84, and #85 as independently executable slices. The previous split made the editor gateway model, join waiting model, and concurrent runtime model look separable when they now need to move as one product track.

## Decision

Use **#83** as the single live umbrella for the merged slice.

- **#83** becomes the active gateway/runtime track
- **#84** and **#85** are absorbed into **#83** and should be closed as no-longer-independent work items
- the canonical design doc must describe the merged slice explicitly
- the GitHub backlog must show one implementation story, not three separate starts

## Implementation contract

1. **Isabelle** locks the visible gateway model first: gateway rendering, lane readability, inspector affordances, and invalid-link prevention.
2. **Blathers** lands the join-gateway projection/runtime contract next: waiting-stage replacement, clean projection, and runtime semantics.
3. **Tangy** spans the slice with behavioural proof, then closes on race-order and regression coverage once concurrent execution is real.

## Must stay green

- `dotnet test UmbracoPrism.sln`
- workflow authoring serialization/schema/publish tests
- workflow editor visual, keyboard, preview, history, simulation, and walkthrough coverage

## Rationale

This keeps one plain product story: authors see gateways, joins own waiting, and runtime executes the same model safely. It avoids shipping a visible gateway UX that still depends on an older waiting-stage/runtime story.
