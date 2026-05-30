---
author: blathers
date: 2026-05-30T18:00:00+01:00
status: proposed
area: workflow-editor
confidence: high
scope: implementation
branch: squad/82-named-lanes-editor-slice
slice: 8a
---

# Slice 8a — Write surface consolidated + ProposalEnvelope relaxed

Closes the two related backend findings from Tom Nook's editor-reset review
(`tom-nook-editor-reset-review.md`, WORTH-NOTING items on the three write
endpoints and the load-bearing agentic envelope).

## Decision

### Endpoint surface — three doors → two

| Route | Status | Purpose |
| --- | --- | --- |
| `POST /api/workflow-authoring/workflows/{key}/publish` | **Kept (canonical direct save)** | Persist a complete `AuthoredWorkflow` and re-publish the runtime definition. Use this for whole-document saves from the editor or any non-agentic integrator. |
| `POST /api/workflow-authoring/workflows/{key}/apply` | **Kept (envelope-mediated save)** | Apply a `ProposalEnvelope`'s `PatchOps` to the stored workflow, persist, re-publish, and write a provenance record. Use this when you need diff-shaped operations and an audit trail. |
| `POST /api/workflow-authoring/workflows/{key}/save` | **Retired** | Used to be a behavioural alias for `/publish` — same handler, same code path. Removed in Slice 8a; callers must migrate to `/publish`. |

The duplicate `/publish` route-header comment block that previously labelled
both `/save` and `/publish` was also fixed.

### `ProposalEnvelope` shape

Required fields (unchanged):

- `Id : Guid` — provenance audit
- `CreatedAt : DateTimeOffset` — provenance audit
- `TargetWorkflowId : string`
- `Ops : IReadOnlyList<PatchOp>` — must be **non-empty** at `/apply` (new 400 case)

Now optional:

- `Agent : PatchAgent?` — when omitted, `/apply` synthesises
  `new PatchAgent { Kind = "human-assisted", Identity = <authenticated principal> }`.
- `Rationale : string?` — accepts `null` or empty.

`PatchAgent.Kind` is no longer a closed vocabulary. The historical labels
(`github-copilot`, `custom-agent`, `human-assisted`) still work but any
non-blank string is accepted. The endpoint:

- rejects whitespace-only `Kind` (when an agent is supplied) with 400,
- continues to cross-stamp `Kind == "human-assisted"` against the calling
  principal (this is the security guarantee from Slice 3c, preserved).

### `/apply` validation order

1. Safe workflow key (`^[a-zA-Z0-9_-]+$`) → 400.
2. Parseable request body → 400.
3. `envelope.ops` non-empty → 400 *(new in 8a)*.
4. Authenticated approver resolvable → 401.
5. Agent kind non-blank / cross-stamp match → 400.
6. Workflow exists → 404.

## Breaking changes for integrators

- **`POST /api/workflow-authoring/workflows/{key}/save` is gone.** Integrators
  must POST to `/publish` (same request body, same response shape). The
  TypeScript SDK (`workflow-authoring-client.ts`) was already on `/publish`,
  so no SDK rename is needed.
- **`/apply` with empty `ops` now returns 400.** Previously this was a silent
  no-op apply. Whole-document saves must move to `/publish`.

## Additive (not breaking)

- `ProposalEnvelope.Agent` and `Rationale` becoming nullable is wire-compatible
  with every existing caller — payloads that still send them keep working.
- `PatchAgent.Kind` accepting free-form strings is wire-compatible with the
  three historical labels.

## Deferred

- `WorkflowPatchService` covert insert (Copper MEDIUM) — separate slice.
- `WorkflowRuntimeEngine` join-arrival forgery (Copper MEDIUM) — separate slice.
- Multi-tenant scoping — V1 is single-tenant by directive.
- Docs refresh (`docs/walkthroughs/*`, `docs/guides/*`,
  `docs/design/workflow-editor-v1/*`) — Mabel owns this in Slice 8b.

## Validation

- `dotnet build UmbracoPrism.sln -c Release` — clean (0 warnings, 0 errors).
- `dotnet test … --filter FullyQualifiedName~UmbracoPrism.Core.Tests.Workflow.Authoring`
  — 147/147 passed (143 prior + 4 new in `WorkflowAuthoringApplyRelaxationTests`).
- Full Core suite: 860 passed / 6 pre-existing manifest failures unchanged
  (`WorkflowEditorManifestTests.*` — missing `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/`
  assets, unrelated to this slice).
- `npm run build` in `src/UmbracoPrism.Client` — green (workflow-editor bundle
  rebuilt).
- Playwright editor specs not re-run — no frontend changes landed (the SDK
  client was already targeting `/publish`).

## Files touched

```
src/UmbracoPrism.WorkflowEditor/Authoring/ProposalEnvelope.cs                            (M)
src/UmbracoPrism.WorkflowEditor/Extensions/WorkflowEditorEndpointExtensions.cs           (M)
src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowAuthoringEndpointsTests.cs        (M)
src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowAuthoringEndpointSecurityTests.cs (M)
src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowAuthoringApplyRelaxationTests.cs  (A)
```
