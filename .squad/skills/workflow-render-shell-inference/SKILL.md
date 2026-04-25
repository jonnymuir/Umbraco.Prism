---
name: "workflow-render-shell-inference"
description: "Derive Prism workflow shells from component shape while keeping legacy runtime contracts working"
domain: "workflow-ui"
confidence: "high"
source: "observed"
---

## Context

Use this skill when workflow authoring is moving to component-only JSON but the backend/UI still need runtime shell metadata such as `StepType`, waiting polling settings, or terminal-state detection.

## Patterns

- Keep authored `stepType` optional and expose runtime-only effective metadata instead:
  - `StepDefinition.EffectiveStepType`
  - `StepDefinition.EffectiveWaitingConfig`
- Infer shell selection from the authored component tree in a stable order:
  - any `waiting` component (or legacy waiting sidecar) → `waiting`
  - any `task-list` component → `task-list`
  - any `summary-list` component → `check-answers`
  - any `fieldset` component → `question`
  - any `panel` component → `confirmation`
  - otherwise → `status-timeline`
- Keep waiting behavior backward-compatible during migration by projecting a `waiting` component back into `WaitingConfig`/`PollAfterMs` for existing shells.
- Treat explanatory copy as standalone components (`inset-text`, `details`, `warning-text`, `body`, `heading`) rather than pseudo-fields inside fieldsets.
- Preserve input semantics by keeping real fields keyed with `fieldKey` + `fieldType`; validation and persistence should remain field-based, not component-shape-based.

## Examples

- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`
- `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/payment-demo-v1.json`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/community-enquiry-v1.json`
- `src/UmbracoPrism.Core.Tests/WorkflowEngine/WorkflowDefinitionInferenceTests.cs`

## Anti-Patterns

- Requiring authors to duplicate shell intent in both `stepType` and component shape.
- Leaving explanatory copy inside `fieldset.fields[]`, where it can be mistaken for a user-editable input.
- Making waiting-state polling depend on authored sidecars when a first-class `waiting` component already carries the same data.
