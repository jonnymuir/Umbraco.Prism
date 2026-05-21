---
name: "workflow-action-handler-registry-quality-gate"
description: "Minimum honest validation and acceptance audit for the Umbraco runtime action-handler registry slice"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #70 quality gate)"
---

## Context

Use this when validating work that claims authored workflow actions now execute at runtime in the reference business app. This slice is backend-first: the key risks are fake catalog wiring, DI registration drift, and action metadata being preserved in published workflow JSON without ever being consumed by the runtime.

## Minimum Gate

1. `dotnet test src/UmbracoPrism.Core.Tests/UmbracoPrism.Core.Tests.csproj --filter "FullyQualifiedName~UmbracoPrism.Core.Tests.Workflow.Authoring.ActionCatalogTests|FullyQualifiedName~UmbracoPrism.Core.Tests.Workflow.Authoring.WorkflowAuthoringEndpointsTests" --nologo`
2. `dotnet test src/UmbracoPrism.Core.Tests/UmbracoPrism.Core.Tests.csproj --filter "FullyQualifiedName~UmbracoPrism.Core.Tests.WorkflowEngine.BusinessAppWorkflowEngine|FullyQualifiedName~WorkflowActionRegistry|FullyQualifiedName~ActionExecution" --nologo`
3. Run one reference-host smoke against MockBusinessApp that proves the runtime-served catalog endpoint responds and at least one workflow path triggers a registered handler.

## Why this combination works

- **Authoring/catalog endpoint tests** prove the reference host exposes the action catalog expected by the editor and keeps catalog shape stable.
- **Workflow-engine plus dedicated registry/execution tests** are where the real acceptance lives: handler resolution, DI wiring, runtime context, and stage/transition action execution.
- **One reference-host smoke** prevents a false green where unit tests pass but the app startup path never registers or exposes the registry correctly.

## Acceptance Audit Heuristics

- Do not credit the catalog endpoint if it is still backed by an editor-only provider instead of the runtime registry requested by the issue.
- Do not credit “handler registry implemented” until `Resolve(actionType)` can return concrete handlers for at least five shipped action types.
- Do not credit “ExecuteAsync works with context” unless a test proves a handler receives workflow/action/context data from a real runtime path, not just direct constructor invocation in isolation.
- Preserving `WorkflowActionDefinition` metadata in published JSON is prerequisite plumbing, not acceptance for #70 by itself.
- Keep the review on the reference app/runtime slice; do not require Storybook or workflow-editor Playwright evidence unless the implementation changes the editor surface.

## Anti-Patterns

- Calling the slice green because `/api/workflow-authoring/action-catalog` already returns built-in entries
- Shipping handler classes without a shared registry abstraction or DI registration
- Adding registry resolution tests but never proving the engine or business app actually invokes handlers
- Treating editor-side action-schema validation as proof of runtime action execution
