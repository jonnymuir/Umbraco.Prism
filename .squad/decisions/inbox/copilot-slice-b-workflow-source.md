# Decision: Slice B — WorkflowSource boundary lands; authoring stack leaves WorkflowEditor

**Author:** Copilot CLI  
**Date:** 2026-05-31  
**Branch:** `squad/82-named-lanes-editor-slice`  
**Scope:** Editor ↔ host DDD boundary, publish-stack move, endpoint rewrite, test-infra refit  
**Status:** Implemented — green build, 814 passing C# tests (was 860; 46 deleted with the obsolete stores), TS typecheck/Vite/Storybook all clean, frontend behavioural test count unchanged vs Slice A baseline.

---

## What changed

### 1. TypeScript: a typed boundary, no HTTP client

`UmbracoPrism.WorkflowEditor` no longer ships an HTTP client and no longer has any opinion about authentication or transport. The editor now consumes three host-supplied contracts:

| Contract | File | Role |
|---|---|---|
| `WorkflowSource` | `workflow-source.ts` | `list / load / save` — the host's persistence boundary. |
| `WorkflowActionCatalog` | `workflow-action-catalog.ts` | The host's extensible action catalog. Falls back to `BuiltInWorkflowActionCatalog` (wraps `STUB_ACTION_CATALOG`). |
| `WorkflowAuthorContext` | `workflow-author-context.ts` | A UX-only hint (`canSave?`). Never authoritative. |

Plus:
- `in-memory-workflow-source.ts` — fixture-friendly `WorkflowSource` implementation used by Storybook stories and any host that wants a zero-network mode.
- `workflow-wire-format.ts` — extracted `normaliseWorkflow` / `serialiseWorkflow` so integrators can convert between wire JSON and `AuthoredWorkflow` without re-implementing the contract.
- `integrations/mockapp-workflow-source.ts` — a **reference implementation** of `WorkflowSource` for the MockBusinessApp. Lives under `integrations/` to make clear it is *example* host code, not editor code. Downstream hosts copy/adapt this.

The editor element now exposes `workflowSource`, `actionCatalog`, `authorContext` as JS-only properties (no attribute mirroring). The previous `authoring-api-base` and `approver-name` attributes are **deleted** — host-side auth posture lives in the host, not in editor markup.

Save button gating: `_canSave = workflow && !blockingIssues && state !== 'saving' && _canSaveByContext`. The tooltip surfaces the author-context reason when present. Server-side authorisation remains the source of truth.

Empty-state semantics: editor element stays silently empty when no source is wired (so Storybook stories driving via `initialWorkflow` are undisturbed). Shell renders a developer-affordance message in the same state.

### 2. C# WorkflowEditor: the authoring stack is gone

Deleted from `UmbracoPrism.WorkflowEditor`:

- `Authoring/Http/WorkflowAuthoringEndpoints.cs`
- `Authoring/Http/WorkflowAuthoringServiceExtensions.cs`
- `Extensions/WorkflowEditorEndpointExtensions.cs`
- `Extensions/WorkflowAuthoringPolicies.cs`
- `Authoring/IAuthoredWorkflowStore.cs`
- `Authoring/InMemoryAuthoredWorkflowStore.cs`
- `Authoring/FilesystemAuthoredWorkflowStore.cs`
- `Authoring/AuthoredWorkflowStoreEntry.cs`
- `Authoring/IWorkflowAuthoringProvenanceStore.cs`
- `Authoring/InMemoryWorkflowAuthoringProvenanceStore.cs`
- `Authoring/FilesystemWorkflowAuthoringProvenanceStore.cs`

`WorkflowEditorServiceExtensions.AddPrismWorkflowEditor()` is now a no-arg call that only registers the projector / patch service / simulation engine / action catalog / parameter widget mapper. Hosts wire their own persistence.

### 3. Publish stack moves into MockBusinessApp

The "publish" concern (snapshotting an authored workflow into a runtime store) is a *host concern*, not an editor concern. Moved (via `git mv`) into `UmbracoPrism.MockBusinessApp/Services/Publishing/` and renamespaced to `UmbracoPrism.MockBusinessApp.Services.Publishing`:

- `WorkflowPublishService.cs`
- `IWorkflowPublishService.cs`
- `PublishResult.cs`
- `PublishPreviewResult.cs`
- `IPublishedWorkflowStore.cs`
- `FilesystemPublishedWorkflowStore.cs`

`WorkflowPublishServiceTests.cs` likewise moved to `Workflow/Publishing/` and renamespaced.

### 4. MockBusinessApp endpoints + storage

- New endpoints: `GET /mockapp/workflows`, `GET /mockapp/workflows/{key}`, `PUT /mockapp/workflows/{key}`.
- **No authentication, no CORS.** Same-origin reference host posture, deliberately. See caveat below.
- Key validation: regex `^[a-zA-Z0-9_\-]+$`.
- Bad JSON returns `400` with a `ProblemDetails` payload.
- New singleton `ReferenceAuthoredWorkflowStore` (in-memory, seeded from `ReferenceWorkflowRepository.GetReferenceWorkflows()`). Save mutates memory only — the host owns its own persistence story, and the reference host explicitly does not persist to disk.
- `Program.cs` lost: the CORS policy, the `WorkflowAuthor` auth policy, the deleted store registrations, `MapPrismWorkflowEditor()`, the `/api/workflow-authoring` middleware guard, the legacy `/admin/workflow/definition/{key}/json` GET+PUT, the JSON modal HTML/CSS/JS + ace.js CDN, and the now-unused `ResolveWorkflowDefinitionKeyAsync` helper.

### 5. Test-infra refit

- New static helper `AuthoredWorkflowFixtureLoader` (test helper, lives in `Workflow/Authoring/`). Replaces the deleted `FilesystemAuthoredWorkflowStore` for tests that only need to read fixture JSON. Six test files migrated.
- New anonymous `MockBusinessAppWebFactory` (lives inside `FourWorkflowReferenceContractTests.cs`). Replaces the deleted `WorkflowAuthoringWebFactory` + `TestUserHeaderAuthHandler`. That test file rewritten to call `/mockapp/workflows/*`.
- Three tests deleted in `AuthoredWorkflowSerializationTests.cs` (`FilesystemStore_ListKeys_ReturnsFixtureKey`, `FilesystemStore_ListAsync_PreservesWorkflowKeySeparatelyFromDefinitionKey`, `FilesystemStore_ReturnsNull_ForMissingKey`) — all tested impl of the deleted `FilesystemAuthoredWorkflowStore`. `FilesystemStore_LoadsFixtureDocument` kept and converted to the new fixture loader.
- Four whole test files deleted: `WorkflowAuthoringEndpointsTests.cs`, `WorkflowAuthoringEndpointSecurityTests.cs`, `WorkflowAuthoringApplyRelaxationTests.cs`, `InMemoryAuthoredWorkflowStoreTests.cs` — all tested deleted production code.

---

## Caveats / downstream impact

1. **No auth on `/mockapp/workflows/*`.** This is intentional — MockBusinessApp is a same-origin reference host. Any production host that mounts the editor against its own endpoints **must** add its own authentication and authorization story. The editor will faithfully send whatever `fetch` defaults the host configures (cookies, bearer, mutual TLS, whatever).
2. **CORS is removed.** If anyone runs `vite dev` against the MockBusinessApp at a cross-origin port, add `proxy: { '/mockapp': 'http://localhost:5163' }` to `vite.config.ts`. Slice scope says Vite-dev cross-origin is not required.
3. **Slice C/D hand-off points for Mabel.** The Definition tab, the simulation engine, and the validation pipeline still consume `AuthoredWorkflow` directly — they don't need restructuring for this slice. Future slices that split the bundle (e.g., per-tab lazy-loading, per-host theming) can layer on top of the same boundary without touching it.
4. **Pre-existing Playwright failures unchanged.** `tests/workflow-editor/layout-professionalization.spec.ts` and `tests/workflow-editor/workflow-browser-surface.spec.ts` continue to fail because they target `http://localhost:5167/workflow-editor.html` (no such server) and `http://localhost:7245` (MockBusinessApp HTTPS, not running during CI playwright runs). A handful of other tests fail at Slice A baseline for unrelated reasons (e.g. `workflow-editor-simulation.spec.ts:8` — Canvas tab is default, button in Simulation slot is not visible). **No new failures introduced by Slice B** — verified by stash + spot-run at baseline.

---

## Validation

| Gate | Result |
|---|---|
| `dotnet build UmbracoPrism.sln` | green, 0 warnings, 0 errors |
| `dotnet test UmbracoPrism.sln` | 814 passed, 0 failed, 11 skipped (was 860; 46 tests deleted with the obsolete stores) |
| `tsc --noEmit` | clean |
| `vite build` (workflow-editor entry) | clean (332.94 kB) |
| `storybook build` | clean |
| `playwright test tests/workflow-editor/` | 85 pass / 11 skip / 49 pre-existing fail / 2 flaky — identical posture to Slice A baseline |
