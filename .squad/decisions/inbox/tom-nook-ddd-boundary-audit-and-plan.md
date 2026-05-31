# DDD boundary audit + revised slice plan

**By:** Tom Nook (Lead)
**For:** Jonny Muir
**Date:** 2026-05-31
**Branch:** `squad/82-named-lanes-editor-slice` @ `6d84e39` (clean tree)
**Supersedes:** `.squad/decisions/inbox/tom-nook-post-reset-audit-and-plan.md` (kept as input)
**Inputs:** `copilot-directive-20260531T091300Z.md` (three corrections) +
           `copilot-directive-20260531T094000Z-ddd-boundary.md` (DDD reframe, deletes the HTTP endpoints)

---

## 1. The two-domain mental model (in my words)

Prism is a **service-design toolkit**: it describes what a workflow *is* (model + schema + canonical JSON), lets a designer build one (editor), and lets them check what it would do (authored-stage validator + an authored-walk simulator). It is not the runtime, not the persistence, not the auth, not the form UI. A **business domain** — MockBusinessApp is the reference — picks up an authored workflow Prism produced, stores it where it likes, projects it to live instances, decides who is allowed to author or advance, renders the end-user UI, and runs the actions. The boundary is a handful of named contracts — one to expose authored workflows to the editor (`WorkflowSource`), one to advertise the action shapes the host supports (`WorkflowActionCatalog`), and a small kit on the editor side for host-supplied identity hints (`WorkflowAuthorContext`). Everything else stays in its own domain.

---

## 2. Classification of every workflow-touching file/area

Legend: **🟦 Prism** (service-design) · **🟫 Business** (reference impl) · **🔌 Boundary** (interface / DTO) · **🚚 MIS-LOCATED** (needs to move) · **🗑 DELETE** (no longer earns its place)

### 2.1 `src/UmbracoPrism.WorkflowEditor/Authoring/` (currently 44 files)

Authored model — all 🟦 unless flagged:

| File | Class | Notes |
|---|---|---|
| `AuthoredWorkflow.cs` | 🟦 | Authored aggregate root |
| `AuthoredStage.cs` | 🟦 | (legacy fields stripped in Slice A) |
| `AuthoredGateway.cs` | 🟦 | Gains `Source` + `Routes` in Slice C |
| `AuthoredTransition.cs` | 🗑 | Deleted in Slice C |
| `AuthoredHandoff.cs`, `AuthoredLane.cs`, `AuthoredAction.cs`, `AuthoredCondition.cs`, `AuthoredField.cs`, `AuthoredParameter{Definition,Schema}.cs`, `WaitingMetadata.cs`, `StageKind.cs`, `GatewayKind.cs`, `FieldType.cs`, `ActionTiming.cs`, `ActionCatalog{Scopes,Statuses}.cs`, `ParameterValueKind.cs`, `ParameterWidgets.cs`, `AuthoredWorkflowStoreEntry.cs` | 🟦 | All authored-model parts |
| `AuthoredWorkflowSchemaValidator.cs` | 🟦 | Service-design rule book (PROJ-codes) |
| `Schemas/authored-workflow.schema.json` | 🟦 | The contract Prism publishes |
| `WorkflowProjector.cs` (521 LOC), `ProjectionResult.cs`, `ProjectionDiagnostic.cs` | 🟦 | Compiles authored → `WorkflowDefinitionFile`. Pure function. Service-design tooling — needed for "what would my workflow look like at runtime?" |
| `WorkflowPublishService.cs`, `PublishResult.cs`, `PublishPreviewResult.cs` | 🚚 → 🟫 | **Mis-located.** Publish *writes the projected file to a published-workflow store* — that's a business decision (where do my runtime defs live? when am I allowed to publish?). The act of *projecting* belongs to Prism (`WorkflowProjector`); the act of *publishing* is the host saving the result. Move to MockBusinessApp; Prism just exposes `WorkflowProjector` and the host calls it then writes wherever it stores published defs. |
| `WorkflowSimulationService.cs`, `WorkflowSimulationResult.cs` | 🟦 | The *authored* simulator — walks an `AuthoredWorkflow` against a list of triggers to show what would happen. Editor-side "what does this design do?" tool. Stays. |
| `WorkflowPatchService.cs` (241 LOC), `IWorkflowPatchService.cs`, `ProposalEnvelope.cs`, `PatchResult.cs` | 🟦 | The save protocol — applies a list of ops to an `AuthoredWorkflow` and returns a new immutable one. Service-design (it's how the editor produces a new authored value); the host then hands that value to its `WorkflowSource.save`. Stays in Prism — `ProposalEnvelope` may shrink (Slice 8a already collapsed most fields). |
| `BuiltInActionCatalogProvider.cs` (389 LOC), `IActionCatalogProvider.cs`, `IActionCatalogSource.cs`, `ActionCatalogEntry.cs`, `DefaultParameterWidgetMapper.cs`, `IParameterWidgetMapper.cs` | 🟦 (base) | The **base** action catalog — generic action shapes the editor can render (`SetField`, `SendNotification`, etc.). Host-extensible via `IActionCatalogSource`. Stays in Prism; host augments. |
| `IAuthoredWorkflowStore.cs` | 🔌 → 🗑 | Today: server-side interface fronted by `/api/workflow-authoring`. After Slice B: replaced by the TS-side `WorkflowSource`; the C# interface and its three impls collapse. There is no C# consumer of `IAuthoredWorkflowStore` in-tree once the endpoints go. |
| `InMemoryAuthoredWorkflowStore.cs` | 🚚 → 🗑 | The seam moves to the editor (TS). Today used only by MockBusinessApp's DI registration; that registration is replaced by the editor page constructing a TS `InMemoryWorkflowSource`. |
| `FilesystemAuthoredWorkflowStore.cs` | 🚚 → 🗑 | Reads `*.workflow.json` from disk. After the endpoint deletion, no consumer. If a future business app wants disk-backed authored workflows, it writes its own `WorkflowSource` on top of any storage it likes. |
| `IPublishedWorkflowStore.cs`, `FilesystemPublishedWorkflowStore.cs` | 🚚 → 🟫 | The "where do projected runtime defs live" abstraction — business-domain by definition. Moves to MockBusinessApp alongside `WorkflowPublishService`. (`InMemoryRuntimePublishedWorkflowStore.cs` is already there.) |
| `IWorkflowAuthoringProvenanceStore.cs`, `InMemoryWorkflowAuthoringProvenanceStore.cs`, `FilesystemWorkflowAuthoringProvenanceStore.cs` | 🗑 | Provenance recorded `(who saved which workflow when)` — that's host-side audit, not Prism's job. The interface lives only because the endpoint group writes to it. Endpoints go ⇒ this goes. A host that wants an audit trail wires it inside its `WorkflowSource.save`. |
| `IWorkflowProjector.cs`, `IWorkflowPublishService.cs`, `IWorkflowSimulationService.cs` | 🟦/🟫 | Projector + simulator stay 🟦; publish-service interface moves with the impl. |
| `ApplyWorkflowRequest.cs` | 🟦 | Patch wire DTO, used by patch service |

### 2.2 `src/UmbracoPrism.WorkflowEditor/Authoring/Http/` and `Extensions/`

| File | Notes |
|---|---|
| `Http/WorkflowAuthoringEndpoints.cs` | 🗑 — back-compat alias to `MapPrismWorkflowEditor`. Deleted with the endpoints. |
| `Http/WorkflowAuthoringServiceExtensions.cs` | 🗑 — back-compat alias. |
| `Extensions/WorkflowEditorEndpointExtensions.cs` (379 LOC) | 🗑 — the nine `/api/workflow-authoring/*` routes. |
| `Extensions/WorkflowAuthoringPolicies.cs` | 🗑 — `WorkflowAuthor` policy is only asserted by the endpoint group. With endpoints gone, this constant is dead. |
| `Extensions/WorkflowEditorServiceExtensions.cs` (`AddPrismWorkflowEditor`) | 🟦 — kept, **trimmed.** After the deletions it just registers projector + patch service + simulator + action catalog. No filesystem paths, no store impls, no published-workflow base path. Probably renames its parameter list to nothing — `services.AddPrismWorkflowEditor()`. |

### 2.3 `src/UmbracoPrism.WorkflowEditor/wwwroot/`

`dist/` is the Vite build output. 🟦. (Build pipeline already correct: editor element ships with the editor package.)

### 2.4 `src/UmbracoPrism.Client/src/workflow-editor/` (TypeScript editor)

| File | Class | Notes |
|---|---|---|
| `prism-workflow-editor.ts`, `prism-workflow-editor-shell.ts` | 🟦 | The Lit elements. Stop calling `fetch`; consume `workflowSource` property. |
| `prism-workflow-graph.ts` (≈4 500 LOC), `prism-step-inspector.ts`, `prism-workflow-outline.ts`, `prism-workflow-simulation.ts`, `prism-stage-preview.ts`, `prism-help-panel.ts`, `prism-inline-help.ts`, `prism-confidence-tabs.ts`, `prism-workflow-action-editor.ts`, `prism-definition-editor*.ts` | 🟦 | All editor surfaces. |
| `types.ts` | 🟦 | Authored TS model. |
| `workflow-validation.ts`, `workflow-canonical-json.ts`, `workflow-runtime-projection.ts`, `workflow-definition-lint.ts`, `workflow-shortcuts.ts`, `workflow-action-editing.ts`, `workflow-stage-assignment.ts`, `gateway-route-conditions.ts`, `workflow-gateway-representation.ts` | 🟦 | Editor-side helpers. (`workflow-gateway-representation.ts` mostly *deletes* in Slice C — gateway anchors become explicit.) |
| `workflow-authoring-client.ts` (5 HTTP functions) | 🗑 + replaced | Becomes `workflow-source.ts` (interface) + `InMemoryWorkflowSource` (reference impl). No `HttpWorkflowSource` — endpoints are gone. The `projectWorkflowLocally` helper survives (in-process projection used by the in-memory source's `project()` and by stories). |
| `fixtures/planning.workflow.json`, `fixtures/index.ts` | 🟦 | Reference fixtures the editor's stories/tests load. |
| `prism-workflow-editor.stories.ts`, `prism-workflow-editor-shell.stories.ts`, `prism-workflow-graph.stories.ts`, `prism-step-inspector.stories.ts` | 🟦 | Service-design illustrations. Switch from fetch-interception to in-memory source. |

### 2.5 `src/UmbracoPrism.Client/tests/workflow-editor/` (28 specs)

All 🟦 — behavioural illustration of the editor. They switch from fetch-mocking to in-memory `WorkflowSource`. `workflow-transition-editor.spec.ts` retires in Slice C (no standalone transitions to edit).

### 2.6 `src/UmbracoPrism.MockBusinessApp/`

| File | Class | Notes |
|---|---|---|
| `Program.cs` (998 LOC) | 🟫 | Composition root + admin pages + runtime endpoints + workflow-editor host page. Trims significantly across Slices B/C. |
| `Services/BusinessAppWorkflowEngine.cs` (426 LOC) | 🟫 | Live-instance runtime, reviewer-action routing. |
| `Services/ReferenceWorkflowDefinitionStore.cs`, `ReferenceWorkflowRepository.cs` (466 LOC) | 🟫 | The four reference workflows are encoded as C# constructors here. **See Open Q1** — they may or may not still live here after this arc. |
| `Services/InMemoryRuntimePublishedWorkflowStore.cs` | 🟫 | Runtime published-def cache. |
| `Services/WorkflowTuiService.cs` (339 LOC) | 🟫 | Terminal UI to drive instances. |
| `Services/WorkflowActions/BuiltInWorkflowActionHandlers.cs` (261 LOC), `WorkflowActionRegistry.cs`, `WorkflowActionContracts.cs`, `WorkflowActionServiceCollectionExtensions.cs` | 🟫 | Runtime action *handlers* — the things that actually do something when an action fires. Correctly placed; this is where the action-catalog/action-handler split lives. |
| `workflow-authored/planning.workflow.json` | 🟫 | Authored doc copied to bin (currently unused since `ReferenceWorkflowRepository` is in code). Either align with Open Q1 outcome or delete. |
| `workflow-seeds/*.json` (5 files) | 🟫 | Projected runtime defs. Read by `FilesystemPublishedWorkflowStore`'s default registration but actually unused at runtime (`ReferenceWorkflowDefinitionStore` re-projects in-process). Audit + likely delete. |

### 2.7 `src/UmbracoPrism.WorkflowRuntime/`

Stand-alone project, referenced only by MockBusinessApp.

| File | Notes |
|---|---|
| `Services/WorkflowRuntimeEngine.cs`, `Abstractions/IWorkflowRuntimeEngine.cs`, `Abstractions/IWorkflowDefinitionStore.cs`, `Stores/FilesystemWorkflowDefinitionStore.cs`, `Models/WorkflowInstanceState.cs`, `Models/WorkflowCursor.cs`, `Extensions/WorkflowRuntimeServiceExtensions.cs` | 🟫 (currently mis-packaged as Prism). **See Open Q2.** By Jonny's definition this is business-domain runtime. The argument for keeping it Prism is that downstream business apps will reach for the same runtime — i.e. Prism ships an opinionated reference runtime so integrators don't reinvent it. Defer the move to Open Q2; my preference noted below. |

### 2.8 `src/UmbracoPrism.Core.Tests/Workflow/Authoring/` (27 test files)

All 🟦 in spirit — they exercise authored model, validator, projector, simulator, patch service. The endpoint/security/store tests retire with the endpoints (Slice B); the publish-service tests move with the publish service (Slice B). Round-trip and fixture tests are rewritten for the gateway-owned shape (Slice C).

### 2.9 `docs/`

| Path | Audience today | Should be |
|---|---|---|
| `docs/design/workflow-editor-v1/01-authoring-ux.md` | service-designer | 🟦 keep |
| `docs/design/workflow-editor-v1/02-runtime-projection.md` | mixed | 🟦 keep — rewrite around `WorkflowProjector` as Prism API, with "host owns the published-def store" callout |
| `docs/design/workflow-editor-v1/03-umbraco-integration.md` | integrator | 🟫-flavoured — move under `docs/guides/` and reframe as "embed the editor in your app" |
| `docs/design/workflow-editor-v1/04-agentic-surfaces.md` | historical | 🗑 delete (Slice 2 already removed the surfaces) |
| `docs/walkthroughs/authoring-a-workflow.md`, `…/planning-workflow-editor.md`, `…/planning-workflow-complete.md`, `…/community-enquiry.md`, `…/information-request.md`, `…/payment-demo.md`, `…/planning-notification.md` | service-designer | 🟦 — rewritten for gateway-owned routes in Slice C |
| `docs/walkthroughs/workflow-administration.md` | business-app operator | 🟫 — rewritten when admin page shrinks (Slice C) |
| `docs/walkthroughs/home-entry.md`, `building-a-mobile-app.md`, `creating-a-tenant.md`, `push-notifications.md`, `design-system.md` | mostly host concerns | 🟫 — left alone, not workflow-domain |
| `docs/guides/extending-prism.md`, `workflow-customisation.md`, `workflow-gds-components.md`, `workflow-setup.md`, `workflow-forms-validation.md` | integrator | 🟫-flavoured. Mostly stay; cross-link to new editor-integration guide. |
| `docs/guides/workflow-editor-composition.md` | confused — half integrator, half UX | rewrite to "Embedding the Workflow Editor" (the boundary recipe — see Slice B) |
| `docs/guides/reference-workflow-contract.md` | service-designer | 🟦 keep, light updates |

### 2.10 Counts

| Bucket | Files |
|---|---|
| Correctly placed | ≈ 95 % of the surface (all editor TS, all authored-model C#, all stories/tests except endpoints, all canonical schema/validator/projector, all MockBusinessApp runtime + handlers + admin code) |
| **Moving** (mis-located) | `WorkflowPublishService.cs` + `PublishResult.cs` + `PublishPreviewResult.cs` + `IWorkflowPublishService.cs` (Prism → MockBusinessApp); `IPublishedWorkflowStore.cs` + `FilesystemPublishedWorkflowStore.cs` (Prism → MockBusinessApp); `docs/design/.../03-umbraco-integration.md` → `docs/guides/` |
| **Deleting** | `AuthoredTransition.cs`; `IAuthoredWorkflowStore.cs` + `InMemoryAuthoredWorkflowStore.cs` + `FilesystemAuthoredWorkflowStore.cs`; `IWorkflowAuthoringProvenanceStore.cs` + 2 impls; `Http/WorkflowAuthoringEndpoints.cs` + `Http/WorkflowAuthoringServiceExtensions.cs`; `Extensions/WorkflowEditorEndpointExtensions.cs`; `Extensions/WorkflowAuthoringPolicies.cs`; `workflow-authoring-client.ts`; `04-agentic-surfaces.md`; `workflow-seeds/*.json` (audit-and-delete) |
| **Replacing** (in spirit) | `workflow-authoring-client.ts` → `workflow-source.ts` + `InMemoryWorkflowSource` + `workflow-action-catalog.ts` (host extension hook) |

Headline: **the vast majority of the tree is already on the right side of the boundary**; the issues are concentrated in (a) the HTTP/store stack inside `UmbracoPrism.WorkflowEditor` (10-ish files, all going), (b) the publish-service move (3 files), (c) the editor's hard-coded HTTP client (one file, replaced).

---

## 3. Boundary contracts

Two domains, two languages. The boundary is asymmetric — the editor lives in TS, the runtime lives in C#. Each contract names its language explicitly.

### 3.1 `WorkflowSource` (TS — primary contract)

```ts
// src/UmbracoPrism.Client/src/workflow-editor/workflow-source.ts
export interface WorkflowSource {
  list(): Promise<WorkflowSummary[]>;
  load(key: string): Promise<AuthoredWorkflow>;
  save(key: string, workflow: AuthoredWorkflow): Promise<void>;
}
```

- **Purpose:** the only way the editor finds out which authored workflows exist, reads one, or writes one back. No `fetch`, no `apiBase`, no auth headers in editor code.
- **Implemented by:** the host. Reference impl `InMemoryWorkflowSource` ships in the package.
- **Consumed by:** `<prism-workflow-editor-shell>` (list/load), `<prism-workflow-editor>` (load/save). Property: `@property({ attribute: false }) workflowSource!: WorkflowSource;`. No automatic HTTP fallback; if unset, the editor renders an empty state.
- **Identity:** *the host* decides whether `save` is allowed for the current user, before resolving the promise. The editor never speaks about identity. This replaces Slice 3c's claims-from-endpoints flow entirely. (See `WorkflowAuthorContext` below for an optional editor-side hint.)
- **Reference impl location:** `src/UmbracoPrism.Client/src/workflow-editor/in-memory-workflow-source.ts` (exported from the package). MockBusinessApp's editor page constructs one seeded from its four reference workflows.

### 3.2 `WorkflowActionCatalog` (TS — host action extension)

```ts
// src/UmbracoPrism.Client/src/workflow-editor/workflow-action-catalog.ts
export interface WorkflowActionCatalog {
  entries(): Promise<ActionCatalogEntry[]>;
}
```

- **Purpose:** the editor needs to know which `action.type` values are renderable (with which parameter shapes). Prism ships a **base** catalog covering generic action types; the host **extends** it with business-specific actions (e.g. `SendPlanningEmail`, `CreateCRMRecord`).
- **Implemented by:** Prism's `BuiltInWorkflowActionCatalog` (TS facade returning the same entries as the C# `BuiltInActionCatalogProvider`), wrapped/composed by the host if it has extensions.
- **Consumed by:** `<prism-workflow-editor>` action-editor dropdowns. Property `@property({ attribute: false }) actionCatalog?: WorkflowActionCatalog;`. Falls back to `BuiltInWorkflowActionCatalog` if unset (because the base catalog is enough for the four reference workflows).
- **Reference impl location:** `BuiltInWorkflowActionCatalog` in `src/.../workflow-action-catalog.ts`. Composition example in the integrator guide.

### 3.3 `WorkflowAuthorContext` (TS — optional UX hint)

```ts
export interface WorkflowAuthorContext {
  canSave?: boolean;
  displayName?: string;
}
```

- **Purpose:** lets the host tell the editor "the current user is X and probably can't save" *for UX reasons only* (greyed-out Save button, "viewing as ${displayName}" badge). **Never** authoritative — the host's `WorkflowSource.save` is the only enforcement.
- **Optional.** If absent, Save is always enabled and the editor stays anonymous.
- **Replaces:** all the claim-reading the deleted endpoint group used to do.

### 3.4 `IWorkflowProjector` (C# — service-design tool the host calls)

```csharp
// src/UmbracoPrism.WorkflowEditor/Authoring/IWorkflowProjector.cs (unchanged)
public interface IWorkflowProjector
{
    ProjectionResult Project(AuthoredWorkflow workflow);
}
```

- **Purpose:** pure function from authored doc to runtime `WorkflowDefinitionFile`. Used by the host when it decides to publish.
- **Implemented by:** Prism (`WorkflowProjector`).
- **Consumed by:** the host's publish flow (now in MockBusinessApp), the host's startup-time projection of reference workflows.

### 3.5 What is **not** a boundary contract

- `IAuthoredWorkflowStore` / `IPublishedWorkflowStore` / `IWorkflowAuthoringProvenanceStore` — deleted; superseded by `WorkflowSource` (TS) and the host's own storage.
- `WorkflowRoleResolver` — considered and rejected. Role gates evaluate at *runtime* against a live instance, not while authoring. The editor needs to know role names exist (free-text on routes) but doesn't need to resolve them. If anything's needed, it's a `WorkflowRoleCatalog` for autocomplete — defer until a story asks.
- HTTP. There is no HTTP boundary contract. The editor is a Lit element; it talks to whatever object the host hands it.

---

## 4. Revised slice plan

Three slices now — the previous plan's Slice B grows substantially (it now includes the HTTP-stack deletion and the publish-service move), Slice C is unchanged in shape (gateway collapse) but inherits an easier file move from B, and a new **Slice D** lands the integrator-recipe docs cleanly. Slice A is unchanged.

### Slice A — Legacy purge *(UNCHANGED from previous plan)*

**Goal, owner, files, tests, risks:** as in `tom-nook-post-reset-audit-and-plan.md` §Slice A. No change. Lands first.

### Slice B — DDD boundary + `WorkflowSource` + endpoint deletion + publish-service move *(REPLACES previous Slice B; substantially bigger)*

**Goal:** the editor depends on `WorkflowSource` only. `/api/workflow-authoring/*` and the `IAuthoredWorkflowStore` family are deleted from the tree. `WorkflowPublishService` and `IPublishedWorkflowStore` move into MockBusinessApp. After this slice, `grep -rn "/api/workflow-authoring" src` and `grep -rn "IAuthoredWorkflowStore\|FilesystemAuthoredWorkflowStore\|WorkflowAuthoringProvenance" src` both return empty.

**Owner:** Isabelle (editor + boundary TS), Blathers (server-side deletions + publish-service move), Brewster (MockBusinessApp re-wire), Mabel (boundary recipe doc — drafted, finalised in Slice D).

**Files in scope:**

*New (TS):*
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-source.ts` — interface
- `…/in-memory-workflow-source.ts` — reference impl
- `…/workflow-action-catalog.ts` — `WorkflowActionCatalog` interface + `BuiltInWorkflowActionCatalog` mirror of the C# base catalog
- `…/workflow-author-context.ts` — the optional UX hint
- `…/fixtures/reference-workflows.ts` — the four authored workflows as plain objects (parsed from existing JSON or written direct), reused by stories/tests/MockBusinessApp

*Modified (TS):*
- `prism-workflow-editor.ts`, `prism-workflow-editor-shell.ts` — replace `fetch*`/`apiBase` plumbing with `workflowSource`/`actionCatalog`/`authorContext` properties; empty state when source unset.
- All 4 stories files — switch to `new InMemoryWorkflowSource([...])`. Stories simplify (no fetch interception).
- All 28 Playwright specs — switch from fetch-mock to source-injection.

*Deleted (TS):*
- `workflow-authoring-client.ts` (`projectWorkflowLocally` moves to `workflow-runtime-projection.ts` if not already there).

*Deleted (C#) — endpoints + stores:*
- `Authoring/Http/WorkflowAuthoringEndpoints.cs`, `Authoring/Http/WorkflowAuthoringServiceExtensions.cs`
- `Extensions/WorkflowEditorEndpointExtensions.cs`
- `Extensions/WorkflowAuthoringPolicies.cs`
- `Authoring/IAuthoredWorkflowStore.cs`, `InMemoryAuthoredWorkflowStore.cs`, `FilesystemAuthoredWorkflowStore.cs`, `AuthoredWorkflowStoreEntry.cs`
- `Authoring/IWorkflowAuthoringProvenanceStore.cs`, `InMemoryWorkflowAuthoringProvenanceStore.cs`, `FilesystemWorkflowAuthoringProvenanceStore.cs`
- All endpoint/security tests under `src/UmbracoPrism.Core.Tests/Workflow/Authoring/` — `WorkflowAuthoringEndpointsTests.cs`, `WorkflowAuthoringEndpointSecurityTests.cs`, `WorkflowAuthoringApplyRelaxationTests.cs`, `InMemoryAuthoredWorkflowStoreTests.cs`.

*Moved (C#) — publish stack to business domain:*
- `Authoring/WorkflowPublishService.cs`, `IWorkflowPublishService.cs`, `PublishResult.cs`, `PublishPreviewResult.cs` → `src/UmbracoPrism.MockBusinessApp/Services/Publishing/`
- `Authoring/IPublishedWorkflowStore.cs`, `FilesystemPublishedWorkflowStore.cs` → same destination
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowPublishServiceTests.cs` → moves under a new `MockBusinessApp.Tests/...` folder or stays in Core.Tests but moves to a Publishing/ subfolder — Blathers picks, fine either way

*Modified (C#):*
- `Extensions/WorkflowEditorServiceExtensions.cs` — `AddPrismWorkflowEditor()` (no args) registers only: `IWorkflowProjector`, `IWorkflowPatchService`, `IWorkflowSimulationService`, action catalog, parameter widget mapper. No store registrations, no published-workflow path.
- `src/UmbracoPrism.MockBusinessApp/Program.cs` — strip the deleted DI lines, the auth policy registration that existed only for the endpoint group, the CORS policy (no more cross-origin editor calls), the `MapPrismWorkflowEditor()` line, the `/admin/workflow/definition/{key}/json` GET/PUT pair (already on the chopping block in old Slice C — pull forward to here because the endpoints they replace are going), and wire the editor host page to bootstrap an in-memory source.
- MockBusinessApp's editor host page (`/workflow-editor.html`) — bootstrap script that constructs `InMemoryWorkflowSource` from the 4 reference workflows and assigns it to the element. Persistence: write-back into the same in-memory list (page-lifetime). **No HTTP, no disk** — exactly the integrator-facing story we want to show. If demo-day "persist across reloads" is needed, MockBusinessApp owns that decision and can serialise to `localStorage` in its bootstrap — a host concern, not Prism's.

*Docs (touched, finalised in Slice D):*
- `docs/guides/embedding-the-workflow-editor.md` — new, draft.
- `docs/guides/workflow-editor-composition.md` — rewritten or redirected.

**Dependencies:** Slice A merged. (Slice A keeps the cut surface small — no legacy normaliser to port.)

**Behavioural tests to add/rewrite:**
- New: `<prism-workflow-editor>` renders an empty state when `workflowSource` is unset.
- New: `<prism-workflow-editor-shell>` lists exactly what an injected source returns; selecting one loads through `load(key)`; saving calls `save(key, workflow)`.
- New: a tiny bespoke `WorkflowSource` in a test file proves the contract is small enough to implement in ~20 lines.
- New (MockBusinessApp): editor host page boots without network calls (no `/api/workflow-authoring/*` requests in the Playwright trace).
- Existing: every Playwright spec stays green after the fetch-mock → source-injection swap.
- Existing (C#): `WorkflowProjectorDeterminismTests`, `WorkflowGatewayProjectionTests`, `WorkflowSimulationServiceTests`, `AuthoredWorkflowSchemaValidationTests`, all `MultiLane*` / `PlanningWorkflow*` / `FourWorkflowReferenceContractTests` — untouched, still green.

**Risk + mitigation:**
- *Risk:* this is bigger than the previous Slice B. **Mitigation:** the deletions are the bulk of the LOC and are mechanically safe (a deleted file with no consumer is the safest change there is). The actual code change is small: one new interface, one in-memory impl, ~6 properties on two Lit elements, ~50 test files re-pointed at the new constructor. Each part lands green independently in the WIP branch.
- *Risk:* publish-service move breaks `StartupWorkflowPublishingTests` and `MockBusinessAppPlanningWorkflowSeedTests` because they reach into `UmbracoPrism.WorkflowEditor` namespaces that no longer host the publish types. **Mitigation:** namespaces follow the files — `UmbracoPrism.MockBusinessApp.Services.Publishing` — and these tests update in the same PR.
- *Risk:* the editor host page in MockBusinessApp currently relies on the API for any "load workflow" action; switching to a script bootstrap means a small new piece of host JS. **Mitigation:** the bootstrap is ~30 lines and matches the in-tree story Storybook already uses.
- *Risk:* Slice 3c's role-gating regressions. **Mitigation:** Slice 3c's whole concern (authoring auth at the HTTP boundary) **disappears** — there is no HTTP boundary. The host decides whether to even render the editor; if it does, the host's `WorkflowSource.save` is the enforcement point.

---

### Slice C — Gateways own routes *(UNCHANGED in shape from previous plan; inherits an easier admin-page edit from Slice B)*

**Goal, owner, files, tests, risks:** as in `tom-nook-post-reset-audit-and-plan.md` §Slice C, with the following deltas:

- **Removed from scope:** the `/admin/workflow/definition/{key}/json` endpoint deletion + admin-page JSON modal removal — these moved forward into Slice B (they're part of the HTTP authoring story we're collapsing). Slice C just removes the mermaid in-page diagram, the action buttons, and the workflow-administration walkthrough rewrite.
- **Added to scope:** rename `WorkflowPatchService`'s `update-transition` op to `update-route`, plus `add-route`/`delete-route` — same as before, just noted explicitly that the service has moved namespace if Open Q2 picks the move-runtime route (it hasn't, see below).

### Slice D — Boundary recipe + integrator docs *(NEW — closes the doc arc cleanly)*

**Goal:** every doc is addressed to one audience. Two recipe trails are explicit: "designing a service" (Prism) and "embedding Prism in your business app" (integrator). The integrator's WorkflowSource recipe is unmistakeable.

**Owner:** Mabel (lead), Celeste (design doc reframe), Tom Nook (review).

**Files in scope:**
- `docs/guides/embedding-the-workflow-editor.md` — finalised: what `WorkflowSource` is, the in-memory reference, write-your-own example (≈20 lines), action-catalog extension hook, the `WorkflowAuthorContext` UX hint, where the four reference workflows live, why there is no HTTP API. ~2 pages.
- `docs/guides/workflow-editor-composition.md` — either rewritten as a deeper-dive companion or redirected. (Pick during the slice.)
- `docs/design/workflow-editor-v1/03-umbraco-integration.md` → move to `docs/guides/` and reframe as integrator-only.
- `docs/design/workflow-editor-v1/02-runtime-projection.md` — rewrite the "publish" passages around `IWorkflowProjector` as Prism API and "host owns the published-def store" pattern.
- `docs/design/workflow-editor-v1/04-agentic-surfaces.md` — delete (Slice 2 already retired the surfaces; the doc has been carrying dead narrative since).
- `docs/walkthroughs/workflow-administration.md` — rewrite to match the simplified admin page.
- `docs/guides/README.md`, root `README.md`, `docs/walkthroughs/README.md` — pointers to the new guide.

**Dependencies:** Slices B and C merged (the recipe describes the real shape).

**Behavioural tests:** none — docs only. Markdown link check stays green.

**Risk + mitigation:** low. The risk is doc rot if Slice D lags Slice B by too long — schedule Slice D within ~one week of Slice B.

---

## 5. Open questions for Jonny

I made calls on five of the original six (legacy normaliser → hard error, abstraction name `WorkflowSource`, admin JSON modal → delete, single-route gateway shape → accept, `AuthoredHandoff` → leave alone). The genuinely ambiguous ones the audit added:

1. **Where do the four reference workflows live?** Options:
   (a) **In `UmbracoPrism.Client` package** (`src/workflow-editor/fixtures/reference-workflows.ts`) — Prism *ships* a portfolio of reference designs so any host can show them. Strongest argument: integrators trying the editor for the first time get a curated experience by default; the "Squad reference" identity stays with Squad.
   (b) **In MockBusinessApp only** — the reference business app *chose* these four scenarios. Strongest argument: Prism shouldn't have an opinion about which workflows are interesting; reference workflows are domain choices, and "planning application" is a domain decision.
   (c) **Split:** a *generic* one or two ship with Prism (e.g. "Approval", "Two-step request") to power empty-state demos; the four current ones move fully into MockBusinessApp.
   **My recommendation:** (c). Prism ships a *tiny* generic pair as the editor's empty-state preview; MockBusinessApp owns the four named domain scenarios. This keeps the editor self-demonstrable without dragging planning/payment-demo vocabulary into the toolkit. Confirm.

2. **Where does `UmbracoPrism.WorkflowRuntime` belong?** Three options:
   (a) **Stays Prism-shipped** — Prism provides an opinionated reference runtime so business apps don't reinvent it. Argument: most Prism integrators *will* want a basic in-memory runtime to get going, and Prism's projector contract is much easier to test with a runtime in the box.
   (b) **Moves into MockBusinessApp** — strictly by Jonny's framing, runtime is business-domain. Argument: by definition.
   (c) **Stays its own assembly, renamed and labelled as a reference business-domain runtime** (e.g. `UmbracoPrism.ReferenceRuntime`), explicitly optional, integrators are free to ignore it. Argument: keeps it factored out (reusable across business apps) without claiming it's part of the service-design surface.
   **My recommendation:** (c). It's the honest position — it isn't service-design, but it isn't bespoke to MockBusinessApp either. Defer the rename to a later arc; doing it in this arc inflates Slice B again. Flag the decision and execute the rename in a follow-up. Confirm direction.

3. **What persistence semantics should `InMemoryWorkflowSource` give the editor host page in MockBusinessApp?** Today there's a JSON modal that mutates `workflow-authored/planning.workflow.json` on disk. After Slice B's delete, the simplest answer is "page-lifetime in memory; reload starts over". Acceptable for the reference business app? Or do you want MockBusinessApp to write through to `localStorage` (still no server round-trip) so demos persist? **My recommendation:** page-lifetime is enough; document it; if a demo needs more, add `localStorage` later. Confirm.

---

## 6. Out of scope for this arc

Same as previous plan, plus:
- The `UmbracoPrism.WorkflowRuntime` rename / repackaging (handled in a follow-up if Open Q2 picks (c)).
- Action *handler* registration patterns in MockBusinessApp (`WorkflowActionRegistry` is already on the right side of the boundary; not touching it).
- Multi-tenant scoping of any host-side workflow source (host concern, not Prism's).
- Any change to `WorkflowProjector` or `WorkflowSimulationService` behaviour — those are service-design and they stay where they are.
- Any change to the runtime contract (`WorkflowDefinitionFile`, `WorkflowTransitionFile`, `IWorkflowRuntimeEngine`).
- All the non-workflow "legacy" code dotted across OIDC/Codespace — same as before.

---

## 7. Recommended execution order

**A → B → C → D**, single PRs, green throughout. After A the tree has no legacy dialect; after B the editor is integrator-friendly and the HTTP authoring stack is gone; after C the model matches the mental model; after D the integrator story is documented as cleanly as the model now reads.
