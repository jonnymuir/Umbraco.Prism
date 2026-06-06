---
name: "concurrent-workflow-demo-slicing"
description: "How to slice behavioural test and showcase work when workflows move to concurrent lanes and joins"
domain: "testing"
confidence: "medium"
source: "observed (2026-05-25T09:32:35.455+01:00 Tangy concurrent-lane issue slicing)"
---

## Context

Use this when workflow work is moving from a simple straight-line journey to concurrent lanes with join gateways, and the team needs to keep behavioural tests and demos understandable while the product changes underneath.

## Patterns

### 1. Keep one straight-line control flow green

- Do not replace every linear proof at once.
- Leave one simple workflow in place as a behavioural control while the new concurrent proofs land.

### 2. Split the work into three proof tracks

- **Editor contract:** lane visibility, join understanding, simulation, validation.
- **Showcase story:** each demo workflow tells one obvious user-visible parallel-work story.
- **Live walkthrough:** public/member/admin surfaces show honest progress before and after the join.

### 3. Keep catalogue contracts stable

- If the product already promises a fixed showcase set, keep that catalogue stable during the redesign.
- Evolve the stories inside the showcases before changing the number of showcases or their entry points.

### 4. Write acceptance in product language

- Prefer “the workflow waits until both checks are finished” over engine-detail wording.
- Explain how the gate stays green: add concurrent proof first, then remove linear-only assumptions once the new proof is passing.

### 5. Sequence the backlog around trust boundaries

- Start by removing duplicate surface/projection logic before changing behaviour.
- Lock the lane/gateway language before changing editor UX or runtime rules.
- Set the editor’s visual language for split/join paths before landing the deeper engine slices.
- Replace waiting stages with join gateways before enabling full multi-lane concurrency, so the user-facing waiting story is stable first.
- Leave showcase workflow evolution until the end of the sequence, but require every slice to keep the behavioural suite green.

## Anti-Patterns

- Replacing all existing walkthroughs before the concurrent replacements are green
- Writing join-gateway issues that only describe internal engine moves
- Changing the showcase catalogue and the behavioural gate in the same slice
- Starting engine concurrency work before the clean Umbraco-facing projection contract is protected
