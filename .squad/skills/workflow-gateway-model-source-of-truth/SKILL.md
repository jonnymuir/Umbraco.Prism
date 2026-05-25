---
name: "workflow-gateway-model-source-of-truth"
description: "Keep authored workflow stages as work nodes and model split/join/wait semantics on named diamond gateways"
domain: "workflow-design"
confidence: "high"
source: "observed (2026-05-25T15:23:06.241+01:00 gateway model clarification)"
---

## Context

Use this when a workflow redesign risks blurring together user-facing work steps and structural routing logic. The model gets confusing fast if waiting, branch, and merge semantics are spread across fake stages, transition labels, and runtime-only metadata.

## Patterns

- Keep **stages** as the place where user-facing work, forms, reviews, confirmations, and actions live.
- Model **transition gateways** as distinct diamond nodes with a name and description.
- Put branch, merge, and join-style waiting semantics on the gateway rather than inventing placeholder waiting stages.
- Let transitions connect stage → gateway, gateway → stage, and gateway → gateway so the authored graph can express real routing structure.
- For join gateways, define the waiting copy and runtime waiting status at the gateway itself.
- Preserve simple stage-to-stage links for linear flows so basic workflows stay easy to author.

## Anti-Patterns

- Treating gateways as hidden engine metadata that authors cannot reason about.
- Putting user-facing waiting copy on a fake stage while the real wait rule lives elsewhere.
- Forcing authors to create placeholder stages just to express gateway-to-gateway routing.
