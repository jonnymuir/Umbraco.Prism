---
name: "workflow-editor-fixture-selector-alignment"
description: "Keep live workflow-editor smoke selectors aligned with authored workflow fixture keys"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #56 quality gate)"
---

## Context

Use this when workflow-editor authoring fixtures or seeded workflow stage keys change. Live walkthrough smoke tests often assert stage nodes by semantic `data-prism-stage` selectors, so fixture drift can break smoke coverage even though the app boots and backend tests still pass.

## Patterns

- Treat the authored fixture and the walkthrough spec as one behavioural contract.
- If a fixture changes stage keys or initial stage order, update the walkthrough comments, selectors, and inspector assertions in the same commit.
- Prefer semantic stage-key selectors such as `data-prism-stage="{stageKey}"`, but keep them sourced from the real seeded workflow rather than stale stub names.
- Use both signals together:
  1. authored workflow fixture/unit tests for schema correctness
  2. `npm run test:playwright:planning-smoke` for live-shell rendering correctness
- When the localhost-auth stack reaches readiness and the first stage selector still fails, suspect contract drift before infrastructure.

## Examples

- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json`
- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`

## Anti-Patterns

- Renaming seeded stages without touching the walkthrough spec
- Trusting backend green status alone after fixture changes
- Leaving spec comments describing obsolete stub workflows after the fixture has evolved
