---
name: "workflow-gateway-slice-sequencing"
description: "Represent future split/join gateways in the editor before changing runtime execution semantics"
domain: "workflow-editor"
confidence: "high"
source: "observed (2026-05-25T14:17:36.055+01:00 issue #83/#84 planning)"
---

## Context

Use this when the workflow model already contains lane and gateway concepts, but the runtime still executes a simpler stage-to-stage path. The team needs a safe behavioural slice that makes gateways visible without partially shipping concurrent execution semantics.

## Patterns

- Make the first post-model slice **editor representation only**: show split/join gateways clearly in lanes before changing runtime rules.
- Keep the executable path stage-driven until join semantics, waiting ownership, and independent cursor behaviour are explicitly implemented.
- Treat gateways as authored intent plus editor affordance first; treat runtime execution as a later slice with its own tests.
- Pin both authoring contract tests and editor behaviour tests while changing the gateway visual language.
- Name the defer line plainly: waiting-stage replacement belongs to the join slice, and deterministic convergence belongs to the engine slice.

## Anti-Patterns

- Partially routing runtime execution through gateway nodes before join rules are locked.
- Mixing gateway visuals, waiting-stage replacement, and concurrent cursor execution into one UI slice.
- Declaring the gateway slice “safe” without pinning the existing planning workflow preview/simulation/publish tests.
