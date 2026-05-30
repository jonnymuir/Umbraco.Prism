---
last_updated: 2026-05-25T16:48:28.029+01:00
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
- only stages and gateways appear as authored workflow nodes
- stages remain the place where user-facing work and actions live
- transition gateways become the only way to route from one node to another
- gateways read visually as diagonal/diamond routing nodes in the editor
- waiting belongs on join gateways rather than on a stage type
- joins release in a predictable way regardless of completion order
- runtime and end-user metadata stays clean
- history stays readable when people and systems act in parallel

## Plain-language model

The workflow is made of **stages** and **diamond transition gateways**.

The model is simple:

- **Stage** — a unit of work. This is where forms, review screens, confirmations, and other actions live.
- **Transition gateway** — a structural routing point with a name and description. A gateway can be a **split gateway** or a **join gateway**.
  - **Split gateway** — starts work in more than one lane.
  - **Join gateway** — waits for the required lanes, then releases the next step.

Authors should understand routing as happening **through gateways**, not through bare stage-to-stage arrows. Links still exist in the graph, but the nodes authors reason about are stages and gateways.

Each live workflow instance can now have more than one active **cursor** at the same time. A cursor is just the engine's way of saying "this lane is currently here".

Authors should mostly think in product language:

- lanes
- stages
- transition gateways
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

### 3. Routing links

The authored graph should allow these structural links:

- stage → gateway
- gateway → stage
- gateway → gateway

Direct stage → stage links are not part of the target model. Even a simple linear hand-off should read as stage → gateway → stage, so the editor, schema, and runtime all teach one routing language.

### 4. Split gateways

A split gateway is the point where one path becomes several lane-owned paths.

Behaviour rules:

- the split itself is deterministic
- each outgoing branch is explicit in the definition
- each created cursor is tied to its owning lane
- if two branches stay in the same lane, they still need distinct cursor identity so they can be tracked safely

The split gateway is about starting parallel work, not about assigning global state.

### 5. Join gateways

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

The gateway itself carries the user-facing metadata for that waiting point:

- gateway name
- gateway description
- waiting copy or instructions shown at runtime
- status detail about which lanes or cursors are still outstanding

That waiting story belongs to the gateway, not to a fake stage placed nearby.

### 6. Deterministic convergence

Join behaviour must be stable under race-order variation.

If lane A arrives before lane B, or lane B arrives before lane A, the final outcome must be the same:

- the same join is satisfied
- the same waiting metadata is shown for the owning lane
- the same next cursor or cursors are released
- no duplicate release happens

The engine therefore needs deterministic join bookkeeping. Internally that means storing arrival tokens in a stable, idempotent way keyed to the join and the arriving lane/cursor.

### 7. Waiting information belongs to the join's lane

Waiting copy, instructions, and status for a join belong to the lane that owns that join.

That means:

- one lane can show "waiting for finance review" while another lane keeps moving
- waiting details are not stored as a global workflow state that flattens all lanes together
- the author defines waiting intent at the join, not as a separate fake stage inserted only for engine reasons

This keeps the product story honest. People see the waiting information that belongs to their lane, not internal engine noise from other lanes.

At runtime, if a join is still waiting for more arrivals, the user should see the same kind of waiting explanation that older waiting states used to provide, but now sourced from the join gateway itself.

### 8. Clean runtime contract

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

### 9. Clear history semantics

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
- stages remain where user-facing actions and forms are configured
- published contracts stay clean and projection-driven
- single-lane workflows remain valid
- straight-line workflows should stay easy to author, but still route through gateways

### What changes

- gateways become first-class authored nodes rather than hidden routing assumptions
- gateways become the only authored routing nodes between stages
- the engine moves from one active state to multiple lane-owned cursors when needed
- waiting stages are removed from the target model and replaced by join gateways
- runtime convergence is explicit and deterministic
- history becomes lane-aware

## Delivery sequence

This design now maps to a condensed execution sequence:

1. **#81** — clean up duplicate surface logic so assignment is the source of truth
2. **#82** — let stages and gateways belong to named lanes
3. **#83** — merged gateway/runtime track for readable gateways, lane-owned joins, and safe parallel execution
4. **#86** — separate actor history from state-change history for parallel work
5. **#87** — evolve showcase workflows and behavioural proof slice by slice

Scope decision: **#84** and **#85** are now absorbed into **#83**. They should not be treated as independent execution items.

That merged order is intentional:

- authors should only learn one gateway story
- join waiting copy should move to the same gateway model authors can already see
- runtime concurrency should land against that same visible model rather than against a temporary seam

## Merged gateway/runtime track after #82

The next slice is no longer editor-only. It is one merged gateway/runtime track that should leave Prism with one coherent story from authoring through runtime.

By the end of this slice, authors and runtime consumers should both see the same plain-language model:

- split gateways visibly branch work into named lanes
- join gateways visibly own the waiting point for their lane
- waiting text lives on the join gateway itself
- one lane can move without overwriting another lane
- joins release the same way regardless of arrival order

### Internal sequence inside #83

1. **Isabelle first** — lock the editor visual language and authoring rules:
   - render split and join gateways as unmistakable diamond/diagonal nodes rather than rounded stage-like cards
   - make lane ownership obvious on canvas and in inspector
   - make gateways the only visible routing object between stages in graph, list, and inspector flows
   - support stage → gateway, gateway → stage, and gateway → gateway links
   - prevent direct stage → stage authoring and any other invalid links that would make the flow ambiguous or unsafe
2. **Blathers second** — move the waiting story and projection contract onto join gateways:
   - replace waiting-stage modelling and waiting-stage types with lane-owned join gateway metadata
   - keep published/runtime projection clean and assignment-driven
   - preserve the user-facing waiting story without exposing engine bookkeeping
   - align authored/runtime contracts so gateways, not bare transitions, carry the routing story
3. **Blathers + Tangy third** — make the runtime honour the same gateway model:
   - run more than one active lane safely at the same time
   - record arrivals per lane/cursor at joins
   - release deterministically with race-order coverage
   - prove that join waiting is surfaced from the join gateway and nowhere else

### Implementation contract

- **Isabelle owns**
  - canvas rendering for split/join diamonds with clear diagonal shape language
  - inspector and list editing that teaches gateways as the routing object instead of transitions
  - graph affordances, lane readability, and invalid-link prevention including no direct stage → stage authoring
- **Blathers owns**
  - authored model and projection changes needed to remove waiting stages from the target model and replace them with join gateways
  - runtime execution semantics for independent lane cursors
  - deterministic join bookkeeping and release behaviour
  - routing contract changes needed so gateways are the only transition mechanism
- **Tangy owns**
  - behavioural proof for editor readability, gateway-only routing, publish/projection continuity, and race-order stability
  - regression coverage proving one lane cannot overwrite or force-advance another
  - final green-gate confirmation for the merged slice

### First movement slice for the slot canvas

The first movement slice should stay simple and accessible:

- lanes render as **vertical columns**
- service flow reads **top to bottom** inside each lane column
- if the same lane needs more than one node at the same level, those sibling nodes expand **to the right**
- the graph canvas keeps its automatic slot layout
- the list workspace is the authoritative place to reorder authored stages
- authors move stages with explicit **Move up** and **Move down** actions
- keyboard reorder uses **Alt + Arrow Up** and **Alt + Arrow Down**
- focus stays on the moved row and a polite live announcement confirms the new position
- freeform numeric order fields are not part of the product model
- drag-to-slot can come later, but only after it matches the same behaviour and accessibility contract

### What must stay green

Pin the workflow contract while the merged gateway/runtime slice lands:

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

The merged slice is only done when gateway authoring, gateway-backed waiting, gateway-only routing, and parallel-lane runtime behaviour all pass those gates together.

## What older docs now mean

Treat these as partial background for the multi-lane redesign, not as the behavioural source of truth:

- `docs/design/workflow-editor-v1/README.md`
- `docs/design/workflow-editor-v1/01-authoring-ux.md`
- `docs/design/workflow-editor-v1/02-runtime-projection.md`
- `docs/design/workflow-forms-engine.md`
- `docs/design/workflow-forms-engine-backend.md`

They still describe useful current-state behaviour, but they include linear-flow and waiting-stage assumptions that the new engine design is replacing.

## Decision summary

Prism should evolve into a lane-based workflow engine where stages carry user-facing work and actions, only stages and diamond gateways appear as authored nodes, gateways are the only routing mechanism between nodes, split gateways create independent cursors, join gateways own waiting semantics and replace waiting stages, convergence is deterministic, the runtime contract stays clean, and history clearly separates actors from state changes.
