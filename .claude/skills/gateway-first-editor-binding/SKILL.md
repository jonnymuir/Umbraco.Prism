---
name: "gateway-first-editor-binding"
description: "Retrofitting a gateway-first workflow editor onto a transport model that still stores stage-to-stage transitions"
domain: "frontend"
confidence: "high"
source: "observed (2026-05-25T16:48:28.029+01:00)"
---

## Context

Use this when the product model says authors work with stages and gateways, but the saved transport model still stores stage-to-stage transitions plus optional gateway references.

## Pattern

1. Treat `fromGateway` and `toGateway` as the authoritative client-side route bindings whenever they exist.
2. Derive gateway placement from those explicit bindings before falling back to topology heuristics.
3. In gateway-first workflows, hide edge chips and stage route handles so the canvas reads as **stage → gateway → stage** instead of stage-to-stage wiring.
4. Keep join-only waiting copy on the gateway inspector, not on a separate waiting stage.
5. When layouting branch flows, push downstream stages below the split gateway and downstream-of-join stages below the join gateway so the vertical reading order teaches the model visually.
6. Use a **slot grid** instead of free placement:
   - stage/content rows for stage cards
   - connector rows for gateways and routed rails
   - lane sub-columns that expand only when same-lane fan-out needs sibling gateways
7. Keep cross-lane fan-out readable by drawing a short shared trunk from the split gateway before branching across lanes on shared connector rails.
8. Route gateway-to-gateway and join-heavy flows on shared rails with elbows/buses; do not draw a unique long curve for every edge if a cleaner grouped path exists.
9. Keep validation detail off the canvas when a dedicated Validation surface already exists; at most show a quiet summary with a pointer to that surface.

## Why it helps

This lets the client teach the corrected routing model immediately, without lying about backend support that does not exist yet. It also makes future backend migration safer because the explicit gateway bindings already act as the source of truth for editor placement and copy.
