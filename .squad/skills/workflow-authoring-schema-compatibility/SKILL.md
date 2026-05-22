---
name: "workflow-authoring-schema-compatibility"
description: "Lock a cleaner authored workflow JSON shape while keeping legacy patch/proposal payloads working"
domain: "workflow-backend"
confidence: "medium"
source: "observed (2026-05-18T13:17:12.103+01:00 issue #55 workflow schema foundation)"
---

## Context

Use this when an authored workflow/document schema needs clearer persisted property names, but existing proposal envelopes, patch payloads, or fixtures still emit the previous names.

## Patterns

- Put the canonical persisted schema in the editor/authoring backend, beside the typed records and validator.
- Publish a JSON schema file next to the C# model so tests can lock both the saved shape and the runtime-facing validation contract.
- Keep projection/runtime contracts unchanged while the editor contract evolves.
- Accept legacy property aliases during deserialisation instead of rewriting every upstream producer at once.
- If a TypeScript client still renders the legacy/editor-friendly names, normalize the API response on fetch instead of assuming the browser sees the same aliases the C# deserializer accepts.
- Prefer alias shims on authoring DTOs for property renames like:
  - `stageKey` → `key`
  - `displayName` → `title`
  - `kind` → `type`
  - `fromStage`/`toStage`/`action` → `source`/`target`/`trigger`
- Back the alias-friendly properties with validation so missing canonical data still becomes diagnostics rather than silent bad state.
- Keep parameter validation reusable via top-level `parameterSchemas` referenced by action instances.

## Examples

- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredStage.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredTransition.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredWorkflowSchemaValidator.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/Schemas/authored-workflow.schema.json`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts`

## Anti-Patterns

- Breaking patch/preview tooling by renaming persisted properties without a compatibility shim
- Pushing authoring-only schema concerns into `WorkflowDefinitionFile`
- Storing executable behaviour instead of declarative action metadata in authored JSON
