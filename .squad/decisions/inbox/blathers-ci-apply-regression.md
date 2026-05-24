---
date: 2026-05-24T08:40:25.066+01:00
agent: blathers
type: fix
scope: backend
status: resolved
---

# CI Regression: WorkflowAuthoringEndpointsTests.PostApply_WithExistingWorkflow_PublishesRuntimeDefinition

## Problem

CI Tests workflow failed on commit 25a72d5 (and previous commits starting with d5e76ca) with HTTP 500 error in the workflow apply/publish endpoint. The test `PostApply_WithExistingWorkflow_PublishesRuntimeDefinition` expected HTTP 200 but got HTTP 500.

The test passed locally on macOS but failed consistently in CI on Linux (Ubuntu).

## Root Cause

Platform-specific filesystem timing issue in `FilesystemPublishedWorkflowStore.SaveAsync()`. The method was not explicitly flushing the file stream after `JsonSerializer.SerializeAsync()`, relying only on the implicit flush during `await using` disposal.

On Linux CI runners, filesystem caching can delay the visibility of newly written files. The `PublishAsync` workflow:
1. Saves workflow JSON to disk
2. Immediately reloads it for round-trip verification
3. The reload failed because `File.Exists()` returned false due to cached directory metadata

This is a known issue with virtualized/networked filesystems in CI environments where directory entry updates lag behind write operations.

## Solution

Added explicit `await stream.FlushAsync(ct);` in `FilesystemPublishedWorkflowStore.SaveAsync()` before returning. This ensures:
- All buffered data is written to disk
- OS-level filesystem metadata is updated
- Subsequent `File.Exists()` checks see the file immediately

## Files Changed

- `src/UmbracoPrism.WorkflowEditor/Authoring/FilesystemPublishedWorkflowStore.cs`: Added explicit flush before return

## Verification

- Local test suite: ✅ 815/815 passed (Release mode)
- Specific test: ✅ `PostApply_WithExistingWorkflow_PublishesRuntimeDefinition` passed

## Branch Protection

**Action Required:** The main branch is currently not protected. This allowed the failing commits to be pushed directly to main without CI validation.

Recommendation: Enable branch protection on `main` requiring:
- Status check: `core-tests` (from CI Tests workflow)
- Prevent direct pushes
- Require PR reviews

## Related Patterns

This follows the test-discipline skill pattern about platform-specific issues, though that skill focuses on CancellationToken mocking rather than filesystem timing.

A new skill could be extracted: "Filesystem durability in cross-platform test environments — always flush streams explicitly before verification operations."
