# Decision: Keep shared workflow authoring fixtures stable across the full core-tests lane

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Blathers  
**Status:** Implemented  

## Decision

1. Treat `community-enquiry.workflow.json` as a shared authored fixture that must survive the whole `core-tests` lane.
2. Restore any canonical authored fixture from the source fixtures directory after `WorkflowAuthoringEndpointsTests` mutates it.
3. Resolve authoring fixtures from the copied output directory first, with a fallback walk up to the source test fixtures directory.

## Why

- `WorkflowPreviewServiceTests` and `WorkflowPatchServiceTests` started depending on `community-enquiry`, but `WorkflowAuthoringEndpointsTests` intentionally wrote an invalid `community-enquiry.workflow.json` and then deleted it in cleanup.
- That left the shared output fixtures directory missing `community-enquiry.workflow.json`, so later test ordering on GitHub Actions could throw `community-enquiry fixture not found`.
- Restoring the canonical fixture and adding a source-tree fallback removes the order dependency without broadening the backend test scope.

## Validation

- `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests --nologo`
