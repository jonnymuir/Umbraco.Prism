---
name: "workflow-gateway-model-source-of-truth"
description: "Keep authored workflows limited to stages and diamond gateways, with gateways as the only routing mechanism and join gateways owning waiting"
domain: "workflow-design"
confidence: "high"
source: "observed (2026-05-25T16:48:28.029+01:00 gateway-only redo contract)"
---

## Context

Use this when a workflow redesign risks blurring together user-facing work steps and structural routing logic. The model gets confusing fast if waiting, branch, and merge semantics are spread across fake stages, transition labels, and runtime-only metadata.

## Patterns

- Keep **stages** as the place where user-facing work, forms, reviews, confirmations, and actions live.
- Model **transition gateways** as distinct diamond nodes with a name and description.
- Make gateways the only routing mechanism between nodes; do not teach or persist direct stage-to-stage authoring in the target model.
- Use node-level transition fields such as `source`, `target`, and `trigger` in the canonical contract so stage and gateway routes share one shape.
- Let links connect stage → gateway, gateway → stage, and gateway → gateway so the authored graph can express real routing structure.
- For join gateways, define the waiting copy and runtime waiting status at the gateway itself.
- Make the editor visuals reinforce the model: gateways should read as diagonal/diamond routing nodes, not as rounded stage variants.
- Treat transitions as supporting plumbing, not the primary author-facing editing concept.

## Anti-Patterns

- Treating gateways as hidden engine metadata that authors cannot reason about.
- Preserving direct stage-to-stage routing in the target model just because it feels simpler for linear flows.
- Carrying editor-only `fromGateway` / `toGateway` shims instead of first-class authored gateway routes.
- Putting user-facing waiting copy on a fake stage while the real wait rule lives elsewhere.
- Rendering gateways as rounded cards so they read like another stage type.
- Forcing authors to create placeholder stages just to express gateway-to-gateway routing.
