---
name: "projector-engine-integration-proof"
description: "Prove projector ↔ runtime engine wiring with one behavioural integration test, not two layers of mirror tests"
domain: "testing"
confidence: "low"
source: "observed (2026-05-31 Slice 1 projector gateway emission fix, blathers)"
---

## Context

Use this when there are two layers (a compiler/projector and a runtime
engine) that each have their own unit tests but the integration boundary
between them silently rots — typical symptom: each layer's tests pass forever
while production behaviour is dead code because the data shape one side emits
no longer matches what the other side expects.

Example precedent: `WorkflowProjector` happily emitted `stage → stage`
transitions and `WorkflowRuntimeEngine` happily implemented Split/Join cursor
logic for `stage → gatewayKey → stage` shapes — and neither side's tests
noticed for months that no authored workflow ever exercised the runtime path.

## Pattern

1. **Author a tiny but representative workflow in code** (no JSON fixture)
   that uses every feature you want to prove integrated — for the gateway
   case: at least one Split with ≥2 branches, one Join with
   `RequiredIncomingLanes`, and a waiting envelope.
2. **Project through the real compiler.** No hand-built runtime definition —
   that's an implementation mirror and lets the real bug hide.
3. **Wire the projected output into the real engine** via the smallest
   possible shim (in this codebase, a `TestableWorkflowRuntimeEngine`
   subclass plus an in-memory `IWorkflowDefinitionStore` holding one
   definition).
4. **Assert outcomes in product language.** The user submits, two cursors
   exist; the first lane approves, the engine defers with waiting copy;
   the second lane approves, the engine completes. No assertion on
   `transition.ToState == "split-foo"` — that's the implementation mirror
   trap.
5. **Keep the test red-first.** If the integration test passes against
   broken code, it's not proving anything. Show it fails on the bug, then
   show it passes on the fix.

## Anti-patterns

- **Two unit-test layers in agreement, no integration test.** Projector tests
  assert what the projector emits; engine tests assert what the engine does
  with hand-built definitions. Neither test would ever notice the contract
  drifting between them.
- **Hand-built `WorkflowDefinitionFile` in an engine test.** Fine for
  isolating engine behaviour, but does NOT prove the projector emits that
  shape. The integration test must start from the authored object.
- **Asserting projector output shape instead of engine outcome.** A test
  that says "the projector emits a transition with ToState == splitKey"
  proves the compiler does a thing. The test you actually want says "after
  the user takes the submit action, two cursors exist". The first reads
  like a spec; the second is the spec.
- **Mocking the engine.** If you mock `WorkflowRuntimeEngine`, you are no
  longer testing the integration — only your own assumptions about it.
