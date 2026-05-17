---
name: "workflow-editor-three-plane-architecture"
description: "Design Prism-adjacent workflow tooling with separate authoring, projection, and agent planes"
domain: "workflow-architecture"
confidence: "high"
source: "observed"
---

## Context

Use this skill when proposing or reviewing workflow-editor work around Prism so the editor can evolve without becoming the runtime authority.

## Patterns

- Keep the **authoring model** richer than Prism runtime JSON. Let editors work with graph concepts, lane/role intent, annotations, and reusable stage patterns.
- Treat Prism-compatible workflow JSON as a **projection target**. The projection layer should deterministically emit `WorkflowDefinitionFile`, component trees, transitions, and any runtime-only inferred metadata.
- Keep **render-shell inference** compatible with existing Prism rules. Do not ask authors to duplicate shell intent when the component tree can drive it.
- Keep **Umbraco content ownership** intact. Public/member workflow pages remain authored site shells; business applications stay authoritative for state progression and validation.
- Expose AI/agent workflows through **structured surfaces** such as generate, explain, diff, validate, and test operations. Avoid direct live-instance mutation as the primary integration path.
- Choose a reference demo that spans both front-stage and back-stage service work. Planning-style journeys are better than trivial single-form demos because they exercise actor changes, review loops, and multi-surface publishing.

## Examples

- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`
- `src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`
- `src/UmbracoPrism.Core/Models/Workflow/WorkflowRenderShellResolver.cs`
- `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs`
- `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json`

## Anti-Patterns

- Making the raw runtime JSON the only authoring experience.
- Letting AI integrations bypass projection/validation and mutate live runtime state directly.
- Collapsing page-shell ownership, runtime workflow state, and authoring concerns into one tool boundary.
- Picking an overly simple demo that hides actor handoffs or service complexity.
