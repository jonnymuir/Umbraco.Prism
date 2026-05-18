---
name: "workflow-authoring-live-seed-contract"
description: "Keep live workflow-authoring seeds valid in MockBusinessApp; fixture-only coverage will miss real authoring API 500s"
domain: "testing"
confidence: "high"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #57 publish pipeline quality gate)"
---

## Context

Use this when workflow-editor or publish-pipeline work changes authored workflow storage, planning fixtures, or MockBusinessApp integration. Backend projector/publish tests can stay green while the live authoring API is already broken if the real `workflow-authored/*.json` seed files drift or become empty.

## Patterns

- Treat the test fixture and the live MockBusinessApp authored seed as two separate contracts:
  1. `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/*.workflow.json` proves backend publish/projection logic
  2. `src/UmbracoPrism.MockBusinessApp/workflow-authored/*.workflow.json` proves the real authoring API and editor shell can load the workflow
- For authoring/publish slices, run both:
  - focused backend publish tests
  - the planning workflow editor smoke (or a direct `/api/workflow-authoring/...` probe if smoke is infrastructure-blocked)
- If `/api/workflow-authoring/workflows` or `/api/workflow-authoring/workflows/{key}` returns `500` with `FilesystemAuthoredWorkflowStore.LoadAsync(...)`, suspect an empty or invalid authored seed before investigating projection code.
- Keep the live authored seed aligned with the workflow key used by the editor shell redirect (`/workflow-editor.html?workflow=planning` in MockBusinessApp).

## Examples

- `src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json`
- `src/UmbracoPrism.MockBusinessApp/Program.cs`
- `src/UmbracoPrism.Client/tests/walkthroughs/01-planning-workflow-editor.walkthrough.spec.ts`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowPublishServiceTests.cs`

## Anti-Patterns

- Trusting fixture-backed unit tests alone after authoring-store changes
- Letting `workflow-authored/planning.workflow.json` go empty or schema-invalid
- Assuming preview/apply are healthy because `WorkflowPublishServiceTests` pass
