---
name: "workflow-action-catalog-foundation"
description: "Model workflow-editor action discovery as catalog metadata backed by the authored parameter-schema contract"
domain: "workflow-backend"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #56 action catalog foundation)"
---

## Context

Use this when the workflow editor needs to discover available runtime actions, render parameter editors, and validate authored action parameters without leaking runtime handler implementation into workflow JSON.

## Patterns

- Keep the action catalog in the authoring boundary (`UmbracoPrism.WorkflowEditor/Authoring`) because it is a design-time/editor concern.
- Reuse `AuthoredParameterSchema` and `AuthoredParameterDefinition` for action parameter contracts so catalog discovery and authored-workflow validation share one shape.
- Keep workflow JSON declarative: stable action `type` + serialisable `params`; never inline code or callbacks.
- Expose a provider seam (`IActionCatalogProvider`) that can return built-ins now and host-specific extensions later.
- Export widget hints from parameter schema metadata through a mapper (`text`, `number`, `select`, `toggle`, `date`, `textarea`) instead of hard-coding form widgets in the UI.
- Let validation fall back to catalog-defined schemas for built-in actions when a workflow does not duplicate top-level reusable parameter schemas.
- Expose the discovered catalog over a thin API (for example `/api/workflow-authoring/action-catalog`) so the frontend can stay data-driven.

## Examples

- `src/UmbracoPrism.WorkflowEditor/Authoring/ActionCatalogEntry.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/BuiltInActionCatalogProvider.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/DefaultParameterWidgetMapper.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredWorkflowSchemaValidator.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/ActionCatalogTests.cs`

## Anti-Patterns

- Hard-coding the action picker in the UI with no backend discovery seam
- Creating a second, slightly-different schema type just for catalog parameters
- Making runtime projection depend on catalog-only metadata instead of stable action type keys
- Requiring every workflow document to repeat built-in parameter schemas just to pass validation
