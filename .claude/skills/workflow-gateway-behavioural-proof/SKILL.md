---
name: "workflow-gateway-behavioural-proof"
description: "Write workflow-editor tests that prove a gateway-only authoring model in plain product language"
domain: "testing"
confidence: "high"
source: "observed (2026-05-25T16:48:28.029+01:00 gateway-only proof rewrite)"
---

## Context

Use this when the workflow editor has shifted away from a transition-first mental model and the quality gate needs to prove what authors should actually see: stages for work, gateways for routing, and join gateways for waiting.

## Pattern

1. **Prove the visual read first.** Use measured DOM geometry to show the canvas reads as `stage -> gateway -> next node`, rather than as direct stage-to-stage jumps.
2. **Keep the language product-facing.** Validation, inspector, and test names should talk about stages, gateways, waiting messages, and reachable routes — not raw transition objects.
3. **Test the gateway silhouette honestly.** Gateways should read as diamond routing points, not as another rounded stage card with different colours.
4. **Move waiting proof to the join.** Join-gateway inspector fields and backend metadata should own the waiting message; do not keep a behavioural contract for a dedicated waiting stage.
5. **Remove stale hybrid proofs.** Rewrite or delete tests that open “Create transition”, assert transition chips as author-facing controls, or describe waiting as a stage type.

## Anti-patterns

- Keeping old transition-editor tests green and calling the model corrected
- Accepting generic validation copy like “add a route” once the product model says “connect it through a gateway”
- Treating rounded gateway cards as good enough when the visual contract is explicitly diamond routing points
