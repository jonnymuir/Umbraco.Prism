---
last_updated: 2026-05-25T12:01:09.927+01:00
status: canonical
---

# Multi-lane workflow engine design

This is the source of truth for how Prism evolves from the current single-path workflow engine into a lane-based engine that can run more than one live path at once.

Use this document for the behavioural model. Older docs that talk about front stage/back stage lanes or waiting stages are still useful background, but they are not the place to define the new concurrent behaviour.

## Why this design exists

The current engine is good at one active path moving from state to state. That is no longer enough for the workflow story we want.

We now need a model where:

- lanes are owned by roles
- more than one lane can be active at the same time
- one lane cannot overwrite another lane's progress
- waiting stages evolve into join gateways
- joins release in a predictable way regardless of completion order
- runtime and end-user metadata stays clean
- history stays readable when people and systems act in parallel

## Plain-language model

The workflow is still made of stages and transitions, but two new ideas become first-class:

- **Split gateway** — starts work in more than one lane
- **Join gateway** — waits for the required lanes, then releases the next step

Each live workflow instance can now have more than one active **cursor** at the same time. A cursor is just the engine's way of saying "this lane is currently here".

Authors should mostly think in product language:

- lanes
- stages
- split gateways
- join gateways
- assignments
- waiting messages

The engine can use cursor bookkeeping internally, but that should not leak into normal authored or end-user language.

## Core behaviour

### 1. Lane ownership

Every stage and gateway belongs to a named lane. Lane meaning comes from assignment data, not from a separate surface flag.

- `actor` and `roleGates` remain the authored source of truth for who owns the work
- lane presentation is derived from that assignment
- lane ownership must survive projection into runtime without adding editor-only noise

The important rule is simple: a lane owns its own work. Another lane can depend on it, wait for it, or join with it, but it must not silently take over its state.

### 2. Independent cursors

When the engine reaches a normal stage, one cursor is active in one lane.

When the engine reaches a split gateway:

- it creates a new active cursor for each outgoing lane path
- each new cursor keeps its own position and progress
- later work in one lane must not replace the current position of another lane

That means a fast lane and a slow lane can finish in different orders without corrupting the workflow.

### 3. Split gateways

A split gateway is the point where one path becomes several lane-owned paths.

Behaviour rules:

- the split itself is deterministic
- each outgoing branch is explicit in the definition
- each created cursor is tied to its owning lane
- if two branches stay in the same lane, they still need distinct cursor identity so they can be tracked safely

The split gateway is about starting parallel work, not about assigning global state.

### 4. Join gateways

A join gateway replaces the old waiting-stage idea.

The join gateway belongs to one lane and has two jobs:

1. hold the waiting story for that owning lane
2. wait until the required incoming lanes have arrived

Behaviour rules:

- a join does not complete just because one lane arrives
- arrival is recorded per required lane/cursor
- the join releases only when its rule is satisfied
- release order is deterministic no matter which lane arrived first

This gives us a stable answer to "what is this lane waiting for?" without putting the whole workflow into one global waiting state.

### 5. Deterministic convergence

Join behaviour must be stable under race-order variation.

If lane A arrives before lane B, or lane B arrives before lane A, the final outcome must be the same:

- the same join is satisfied
- the same waiting metadata is shown for the owning lane
- the same next cursor or cursors are released
- no duplicate release happens

The engine therefore needs deterministic join bookkeeping. Internally that means storing arrival tokens in a stable, idempotent way keyed to the join and the arriving lane/cursor.

### 6. Waiting information belongs to the join's lane

Waiting copy, instructions, and status for a join belong to the lane that owns that join.

That means:

- one lane can show "waiting for finance review" while another lane keeps moving
- waiting details are not stored as a global workflow state that flattens all lanes together
- the author defines waiting intent at the join, not as a separate fake stage inserted only for engine reasons

This keeps the product story honest. People see the waiting information that belongs to their lane, not internal engine noise from other lanes.

### 7. Clean runtime contract

The published runtime contract should stay assignment-driven and user-facing.

Keep:

- stage and gateway assignment
- lane-relevant waiting copy
- available actions
- visible progress/status information

Do not expose as normal runtime contract:

- internal cursor ids
- token accumulation details
- join bookkeeping records
- engine-only merge metadata

The runtime may need that data internally, but authors and consumers should not have to model around it.

### 8. Clear history semantics

Parallel workflows make history confusing unless we separate two different things:

- **who acted** — person/system, action, time, lane
- **what state changed** — cursor moved, join token recorded, join released, workflow status changed

Those should be related, but not collapsed into one ambiguous line.

History rules:

- record the actor separately from the state change
- include the lane for both when relevant
- do not imply false ordering across independent lanes
- do not let system activity hide a human action trail

Support and debugging should be able to answer:

- who did something
- which lane they acted in
- what changed because of it
- whether the workflow was waiting, splitting, or joining at the time

## Mapping from current model to the new one

### What stays

- authored assignment still comes from `actor` and `roleGates`
- published contracts stay clean and projection-driven
- single-lane workflows remain valid
- straight-line workflows should keep working without redesign

### What changes

- the engine moves from one active state to multiple lane-owned cursors when needed
- waiting stages are replaced by join gateways
- runtime convergence is explicit and deterministic
- history becomes lane-aware

## Delivery sequence

This design maps directly to the existing issue sequence:

1. **#81** — clean up duplicate surface logic so assignment is the source of truth
2. **#82** — let stages and gateways belong to named lanes
3. **#83** — make split/join behaviour readable in the editor
4. **#84** — replace waiting stages with lane-owned join gateways
5. **#85** — run parallel lanes safely with independent cursors and deterministic joins
6. **#86** — separate actor history from state-change history for parallel work
7. **#87** — evolve showcase workflows and behavioural proof slice by slice

This order matters because the behavioural contract should be locked before the runtime gets more complex.

## Safest next behavioural slice after #82

The safest next cut is **editor representation only** for gateways.

That means the next implementation slice should do four things:

1. render split and join gateways as first-class lane items in the editor
2. make the owning lane obvious on the canvas and in the inspector
3. make fan-out and merge direction easy to read across lanes
4. leave current runtime execution, preview, simulation, and publish behaviour stage-driven for now

### What to implement next

- Show a **split gateway** as a distinct gateway node in the lane that owns the branching point.
- Show a **join gateway** as a distinct gateway node in the lane that owns the convergence point.
- Keep gateway copy short and structural: title, kind, owning lane, and related transitions.
- Let authors select a gateway and inspect its lane, title, and split/join kind without treating it as a normal stage.
- Make the graph draw branch and merge lines in a way that clearly shows “one path becomes many” and “many paths converge here”.
- Keep single-lane workflows readable even when no gateways are present.

### What to defer

- Do **not** change published runtime execution semantics in this slice.
- Do **not** replace current waiting-stage runtime behaviour yet; that belongs to **#84**.
- Do **not** introduce independent live cursors, join token bookkeeping, or deterministic release rules yet; that belongs to **#85**.
- Do **not** require workflows to route through executable gateway nodes before the current end-to-end workflow story is preserved.

### Practical rule for this slice

Until #84 and #85 land, gateways are an authored/editor concept that explains intent and lane ownership. The existing stage-to-stage workflow path remains the executable path that preview, simulation, publish, and the current runtime continue to follow.

That is the safest way to make gateways visible now without breaking the planning workflow or forcing the engine to partially emulate concurrency before the join rules are locked.

### Tests to keep green while doing it

Pin the current workflow contract while the editor visual language changes:

- `dotnet test UmbracoPrism.sln`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/AuthoredWorkflowSchemaValidationTests.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/AuthoredWorkflowSerializationTests.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowPublishServiceTests.cs`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-visual.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-stage-preview.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-history.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-simulation.spec.ts`
- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`

## What older docs now mean

Treat these as partial background for the multi-lane redesign, not as the behavioural source of truth:

- `docs/design/workflow-editor-v1/README.md`
- `docs/design/workflow-editor-v1/01-authoring-ux.md`
- `docs/design/workflow-editor-v1/02-runtime-projection.md`
- `docs/design/workflow-forms-engine.md`
- `docs/design/workflow-forms-engine-backend.md`

They still describe useful current-state behaviour, but they include linear-flow and waiting-stage assumptions that the new engine design is replacing.

## Decision summary

Prism should evolve into a lane-based workflow engine where split gateways create independent cursors, join gateways replace waiting stages, convergence is deterministic, waiting information belongs to the owning lane, the runtime contract stays clean, and history clearly separates actors from state changes.
