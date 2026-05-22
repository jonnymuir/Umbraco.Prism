---
name: "workflow-publish-preview-apply-boundary"
description: "Keep workflow preview side-effect free and make apply/publish use one deterministic, round-trip-verified runtime publish path"
domain: "workflow-backend"
confidence: "medium"
source: "observed"
---

## Context

Use this when a workflow editor has an authored source model, a generated runtime artifact, and a proposal-first review loop. The tricky part is separating confidence-building preview behaviour from the actual publish mutation without creating two different projection pipelines.

## Patterns

- Keep **one deterministic projector** for every path:
  - direct publish of authored workflow
  - preview of proposal patches
  - apply of approved proposals
- Make **preview a dry run**:
  - patch authored workflow in memory
  - project runtime definition
  - compare with the currently published seed/checksum
  - do not write authored or runtime files
- Make **apply the approved mutation boundary**:
  - persist authored workflow JSON
  - immediately republish runtime JSON with the same projector
  - return publish checksum/path/verification details to the caller
- Preserve authored-only concepts in the published runtime artifact via **optional metadata blocks**, not by changing the runtime's core navigation contract.
- Treat **round-trip verification** as part of publish:
  - write canonical runtime JSON
  - reload it from the publish store
  - compare canonical bytes/checksum before reporting success

## Examples

- `src/UmbracoPrism.WorkflowEditor/Authoring/WorkflowPublishService.cs`
- `src/UmbracoPrism.WorkflowEditor/Authoring/FilesystemPublishedWorkflowStore.cs`
- `src/UmbracoPrism.WorkflowEditor/Extensions/WorkflowEditorEndpointExtensions.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowPublishServiceTests.cs`

## Anti-Patterns

- Letting preview mutate runtime seeds "just to make it easier to inspect"
- Having apply use a different projector or serializer than direct publish
- Writing publish timestamps into the runtime JSON and then expecting deterministic checksums
- Dropping authored action/condition intent during publish and expecting later runtime slices to reconstruct it
