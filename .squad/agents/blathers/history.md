## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---

# Blathers — History (Summarized)

**Agent:** Backend specialist shipping Codespaces URL derivation fixes, backchannel rewrites for JWKS/token-refresh, and security analysis for auth isolation.

**Focus Areas:** Aspire dashboard Codespaces access, authentication diagnostics, runtime stale-code diagnosis, backchannel OIDC validation, dynamic endpoint discovery, transport diagnostics.

---

## Recent Work Summary

### Transport Diagnostics & Downstream Demo Fixes (2026-05-03 → 2026-05-04)
- ✅ Implemented response-visible transport diagnostics for downstream API calls
- ✅ Fixed workflow API backchannel URL resolution in Codespaces
- ✅ Diagnosed JWKS backchannel escape as root cause of auth timeouts
- ✅ Added logging for null auth headers in workflow clients
- ✅ Aligned workflow handlers to `Results.Problem()` for consistency
- ✅ Fixed `PrismContextTests` race condition via `EnvVarSensitiveTestCollection`

### Key Learnings
- Named HttpClients must be registered via AddHttpClient() even when timeout is managed via CancellationToken
- Any test class reading `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT` must use `EnvVarSensitiveTestCollection` to avoid parallelism hazards
- Response-visible diagnostics beat verbose logs for operator troubleshooting
- Safe transport diagnostics must mask internal ports but show public URLs (browser-visible anyway)

## Learnings: Authored Workflow V1 Foundation (2026-05-16)

**Authored types location:** `src/UmbracoPrism.Core/Workflow/Authoring/`. Namespace `UmbracoPrism.Core.Workflow.Authoring`. All types are records (immutable), JSON-serializable via STJ with `[JsonConverter(typeof(JsonStringEnumConverter))]` on `StageKind` and `FieldType`.

**Determinism enforcement:** `WorkflowProjector.CanonicalOptions` — `JsonNamingPolicy.CamelCase`, `WriteIndented = false`, `DefaultIgnoreCondition = Never`, `UnsafeRelaxedJsonEscaping`. Stages sorted by `StageKey` (ordinal), transitions by `(FromStage, ToStage, Action)`, fields by `Key`. SHA-256 of canonical UTF-8 bytes (no BOM) gives the `ProjectionResult.Checksum`. Locked by `WorkflowProjectorDeterminismTests`.

**Shell inference sharing:** `WorkflowProjector` emits components (FieldsetComponent, SummaryListComponent, PanelComponent, TaskListComponent, WaitingComponent) whose presence causes `PrismComponentExtensions.InferStepType()` (in `UmbracoPrism.Shared`) to return the correct step type. No duplication of inference logic — the projector drives inference via component choice, the runtime infers as normal.

**Planning fixture:** `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json` — 4 stages (declaration → application-form → check-answers → submitted). Tangy's planning-workflow tests consume this fixture as source of truth.

**Store:** `FilesystemAuthoredWorkflowStore(basePath)` — reads `*.workflow.json` from any directory. Not wired to live host in V1. Pass fixture path in tests.

**Commit:** `24374f2` — `feat(core): introduce authored workflow model and deterministic V1 projection slice`

### Implementation Patterns
- Use `BUSINESSAPP_BACKCHANNEL_URL` fallback for internal Codespaces calls, then `PrismBusinessApp:WorkflowApiBaseUrl` for production
- Instrument critical paths with safe metadata (transport type, backchannel presence, timeout cause) for diagnostics
- Guard dev diagnostics with `IsDevelopment` or `Prism:EnableDownstreamDemo` flags

---

## Full Session Archive

See `history-archive.md` for complete session-by-session work logs prior to 2026-05-03 summarization.

---

## Latest Coordination (2026-05-04)

**Status:** Release-ready. All tests passing. Awaiting final squad state consolidation and merge.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-04 | Workflow Admin UI Cleanup

**Status:** In Progress

Implemented workflow admin UI cleanup for walkthrough and manual documentation use.
Coordinating with Brewster (dashboard navigation) and Tangy (screenshot integration).

## Learnings

### 2026-05-17T16:56:41.297+01:00 | Runtime Boundary Audit — Separation Assessment

**Context:** Jonny asked whether the architecture has clean separation after the first extraction slice.

**Findings:**
- `UmbracoPrism.WorkflowEditor` — authoring plane is cleanly extracted ✅
- `UmbracoPrism.Core` (Umbraco) — correctly minimal: only antiforgery, nonce, PRG, HTTP client interface; knows nothing about definitions, instances, or transitions ✅
- `UmbracoPrism.MockBusinessApp` — mixed concerns: `BuildEnvelope()`/`BuildComponents()`/`BuildFields()` (~300 lines of generic runtime rendering pipeline) belongs in a library, not the example consumer
- `IBusinessAppWorkflowClient` in Core is the correct interface boundary — Umbraco only sees `WorkflowResponseEnvelope`

**Key gaps identified:**
1. No `UmbracoPrism.WorkflowRuntime` library — generic state machine and render pipeline are embedded in MockBusinessApp
2. No dedicated authoring shell host — editor UI is served via a `PhysicalFileProvider` hack pointing to a relative path in `Program.cs:52-62`
3. MockBusinessApp references `UmbracoPrism.Core` (Umbraco package) for `AddPrismAuthentication()`; stale `using UmbracoPrism.Core.Models.Workflow` imports in both `BusinessAppWorkflowEngine.cs:4` and `Program.cs:8`
4. `workflow-authored/` and `workflow-seeds/` both live in MockBusinessApp content root — these should move to the shell host when it is created

**Next slices:**
1. Extract `UmbracoPrism.WorkflowRuntime` (IWorkflowRuntimeEngine + BuildComponents pipeline + FilesystemDefinitionLoader)
2. Create `UmbracoPrism.WorkflowEditorHost` shell app (shows off zero-coupling embedding)
3. Move auth helpers out of Core → Shared to break MockBusinessApp → Core coupling

**Decision written:** `.squad/decisions/inbox/blathers-runtime-boundary.md`

### 2026-05-16T13:20:33.659+01:00 | Workflow Editor V1 — Authored Model, Projection, Validation

- **Authored Model shape:** `AuthoredWorkflow` with `AuthoredStage[]`, `AuthoredTransition[]`, `AuthoredRole[]`, `AuthoredField[]`. Stages carry `StageKind` (Capture/Review/Decision/TaskList/Waiting/Confirmation/Backstage/Complete), audience-specific `AuthoredView[]`, `AuthoredExit[]`, and authored-only concerns (EditorComment, CanvasPosition, ProvenanceTags). The model is never loaded by the Prism runtime directly.
- **Projection determinism contract:** `IWorkflowProjector.Project(AuthoredWorkflow)` is a pure function. Determinism is guaranteed by: (1) normalising arrays by sorted key before emit, (2) serialising with fixed camelCase options and no indentation, (3) computing SHA-256 of that serialisation as the checksum. Same input → byte-identical output. This enables diff/replay/test.
- **Shell inference preservation:** The projector must emit component trees that satisfy the existing `PrismComponentExtensions.InferStepType()` and `WorkflowRenderShellResolver.ResolveShell()` contracts. `WaitingComponent` → `status-timeline`; `PanelComponent` (no inputs) → `confirmation`; `SummaryListComponent` → `check-answers`; `TaskListComponent` → `task-list`; default → `question`. The projector must NEVER emit `stepType` or `waitingConfig` (locked by `WorkflowDefinitionInferenceTests.DemoWorkflowSeeds_DoNotAuthorLegacyStepMetadata`).
- **Validation layering:** Three layers — authoring-time (fast, no-IO, per-save), projection-time (before emit, blocks on error), runtime-time (existing Prism engine, untouched). Authoring-time and projection-time are exposed via `IWorkflowProjector.ValidateAuthored()` and `ValidateProjection()`.
- **Storage:** `workflow-authored/*.workflow.json` is the source of truth; `workflow-seeds/*.json` is the checked-in generated output. A CI verify step detects manual edits to the seed by re-projecting and comparing checksums.
- **Patch envelope:** `WorkflowPatchEnvelope` carries JSON-Pointer ops on the Authored Model, `baseVersion` for optimistic concurrency, `rationale`, and `PatchProvenance` (author, timestamp, original NL request).
- **Key design doc:** `docs/design/workflow-editor-v1/02-runtime-projection.md`

### 2026-05-15T06:35:47.013+01:00 | PASA death-process design

- A third-party initiated workflow should authenticate the notifier as the actor and link the deceased member as a server-side subject, not as the signed-in user.
- Save/resume for sensitive one-off cases works better with verified case access (magic link or OTP) plus a separate case aggregate than with mandatory permanent registration.
- Prism remains the workflow shell; case tracking, member matching, evidence manifests, and reviewer notes belong in business-app domain persistence.

### 2026-05-16T10:59:37.438+01:00 | Workflow editor authored-model projection

- Keep workflow authoring stage-centric: authors describe stage intent, routes, actors, handoffs, waiting/deadline metadata, and audience views, while Prism-compatible shells are inferred/projected at runtime instead of being duplicated in authored JSON.
- Preserve current Prism compatibility by keeping `definitionKey`, `initialState`, `instancePolicy`, component-based form semantics, transition/action keys, `StateVersion`, `WorkflowProblem`, and waiting/task/check-answers/confirmation behaviour stable across projection.
- Treat the workflow instance as journey position only; case state, linked subjects, assignments, reviewer notes, deadlines, and third-party participation must live in business-app case persistence rather than inside generic workflow field values.
- Key file paths: `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`, `src/UmbracoPrism.Shared/Models/Workflow/WorkflowResponseEnvelope.cs`, `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs`, `src/UmbracoPrism.Core/Models/Workflow/WorkflowRenderShellResolver.cs`, `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppWorkflowEngine.cs`, `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json`.

## 2026-05-15: PASA Death Process Backend Decision

Produced backend decision on notifier workflow mechanics. Specified lightweight verified contact (magic link/SMS OTP), case-scoped identity, Prism-hosted workflow for notifier, case persistence in business app. Defined need for NotifierIdentity/NotifierSession model alongside DeathCase. Merged to shared registry.

## Learnings: V1 Agent Loop Services + HTTP API (2026-05-17)

**WorkflowPatchService:** Applies `ProposalEnvelope` ops immutably using C# record `with` expressions and `ToList()` copies. Five op kinds: `insert-stage`, `remove-stage`, `update-stage`, `insert-handoff`, `update-transition`. Errors are diagnostic-only — service never throws. Version increments only after all ops succeed. Output validated via `IWorkflowProjector.Project()`. Path resolution: `/stages/{key}` (non-integer key) or `/stages/{index}` (integer index) or value.StageKey fallback.

**WorkflowPreviewService:** Pure function computing semantic diff between original and patched `AuthoredWorkflow`. Returns `PreviewResult` with `Diff []DiffEntry` (polymorphic) and `JourneyTrace string[]`. Trace is deterministic: starts from `InitialStageKey`, follows transitions sorted by `Action` (ordinal), halts at terminal stages or cycle detection via `visited` HashSet.

**SemanticDiff:** `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` with six `[JsonDerivedType]` subtypes — `StageAdded`, `StageRemoved`, `StageUpdated`, `HandoffAdded`, `HandoffRemoved`, `TransitionUpdated`. Discriminator field `"type"` is Tangy-compatible for client-side parsing.

**HTTP surface:** Six endpoints under `/api/workflow-authoring`:
- `GET /workflows` → list all stored authored workflows
- `GET /workflows/{key}` → load single by key
- `POST /workflows/{key}/validate` → validate authoring-time rules, return `ProjectionResult` (with `hasErrors`)
- `POST /workflows/{key}/project` → full projection, return `ProjectionResult` with checksum + file
- `POST /workflows/{key}/preview` → apply envelope, compute diff + journey trace
- `POST /workflows/{key}/apply` → apply + save + write provenance record

All responses use `WorkflowProjector.CanonicalOptions`. CORS dev policy via `RequireCors("WorkflowAuthoringDevCors")` gated on `IsDevelopment()`.

**WAF integration tests:** Two `Program` classes conflict (MockBusinessApp + TestSite both referenced). Resolved with `Aliases="global,MockBusinessApp"` on the `ProjectReference` and `extern alias MockBusinessApp;` + type alias in the test file. `ConfigureWebHost` sets `UseEnvironment("Development")`, injects minimal tenant config via `AddInMemoryCollection`, and overrides `IAuthoredWorkflowStore` to point at fixture directory.

**FluentAssertions v6 + expression trees:** `ContainSingle(e => e is T derived && derived.Prop == X)` fails at compile time (`CS8122`). Use `.OfType<T>().Should().ContainSingle(t => t.Prop == X)` instead. `BeOneOf` with `because:` as a named positional arg also fails — use the `BeOneOf(IEnumerable, string because)` overload.

**Commit:** `dfa26ec` — `feat(core): patch + preview services and authoring HTTP API for V1 agent loop`


## 2026-05-17 | Workflow Editor Backend Extraction — First Slice

**Status:** ✅ Complete  
**Commit:** 9ab9ba4  
**Branch:** feat/workflow-editor-library-extraction (worktree)

Completed first extraction slice of workflow editor architecture: moved all backend authoring code from `UmbracoPrism.Core/Workflow/Authoring/` into new dedicated `UmbracoPrism.WorkflowEditor` library with clean consumer API surface.

### Implementation

- Scaffolded `UmbracoPrism.WorkflowEditor` Razor Class Library project (net10.0, Web SDK)
- Moved 23 C# files: domain models, projector, patch/preview services, HTTP endpoints
- Updated all namespaces: `UmbracoPrism.Core.Workflow.Authoring` → `UmbracoPrism.WorkflowEditor.Authoring`
- Added new consumer API: `AddPrismWorkflowEditor()` / `MapPrismWorkflowEditor()` in Extensions namespace
- Migrated MockBusinessApp Program.cs to use new API surface
- Updated 7 test files with new namespace imports
- Removed static asset conflicts (`EnableDefaultContentItems=false`, deleted template wwwroot)

### Validation

- ✅ Full solution build succeeds (4 pre-existing warnings only)
- ✅ All 51 workflow authoring tests pass (47 passed, 4 skipped by design)
- ✅ Backward-compat maintained via Http subdirectory (old API still compiles)

### API Migration

**Old:**
```csharp
using UmbracoPrism.Core.Workflow.Authoring.Http;
builder.Services.AddWorkflowAuthoring(path);
app.MapWorkflowAuthoringEndpoints();
```

**New:**
```csharp
using UmbracoPrism.WorkflowEditor.Extensions;
builder.Services.AddPrismWorkflowEditor(path);
app.MapPrismWorkflowEditor();
```

### Key Learnings

- **SDK choice matters**: Used `Microsoft.NET.Sdk.Web` (not `Razor`) to avoid Blazor deps we don't need. Backend-only library doesn't need component runtime.
- **Static asset isolation**: Template-generated wwwroot caused path conflicts with Core's static assets. Explicit `EnableDefaultContentItems=false` prevents automatic discovery.
- **Git rename detection**: Moving 23 files as-is (then updating namespaces) triggers Git rename detection, preserving history cleanly.
- **Test namespace updates**: Bulk sed for using statements worked well for 7 test files; manual verification caught one interface that sed missed.

### Next Slice Boundary

This slice focused on backend domain extraction only. Remaining work for full workflow editor isolation:

1. **Frontend extraction**: Move Vite-built UI assets from Core wwwroot/dist into WorkflowEditor library
2. **Authoring fixture relocation**: workflow-authored/*.json currently lives in MockBusinessApp; consider library-embedded defaults
3. **Deprecation path**: Add `[Obsolete]` to old `AddWorkflowAuthoring()` in V2, remove in V3

### Decision

Wrote `.squad/decisions/inbox/blathers-workflow-editor-extraction-slice.md` documenting the extraction strategy, API migration, validation results, and next-slice handoff.

### 2026-05-17T17:09:07.957+01:00 | WorkflowRuntime extraction slice

- `UmbracoPrism.WorkflowRuntime` is a safe first extraction target: move the reusable engine, instance state, definition-store abstraction, and DI extension first; leave endpoint mapping and host-specific auth concerns for a later slice.
- Keeping `BusinessAppWorkflowEngine` as a thin adapter over `WorkflowRuntimeEngine` preserves MockBusinessApp's reviewer/dev hooks while making the reference app show a clean consumer story (`AddPrismWorkflowRuntime(...)`).
- A constructor-level fallback to `FilesystemWorkflowDefinitionStore(Path.Combine(env.ContentRootPath, "workflow-seeds"))` lets old direct test construction keep working while DI-based hosts can supply `IWorkflowDefinitionStore` explicitly.
