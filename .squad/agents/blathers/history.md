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

## 2026-05-17: Workflow Editor Backend Extraction & Runtime Model

### 2026-05-17T16:56:41.297+01:00 | Runtime Boundary Audit — Separation Assessment

**Findings:**
- `UmbracoPrism.WorkflowEditor` — authoring plane cleanly extracted ✅
- `UmbracoPrism.Core` (Umbraco) — correctly minimal: only antiforgery, nonce, PRG, HTTP client interface ✅
- `UmbracoPrism.MockBusinessApp` — mixed concerns: generic runtime rendering pipeline (~300 lines) should be in a library, not example consumer

**Key gaps:**
1. No `UmbracoPrism.WorkflowRuntime` library — generic state machine embedded in MockBusinessApp
2. No dedicated authoring shell host — editor served via PhysicalFileProvider hack

### 2026-05-17T17:09:07.957+01:00 | WorkflowRuntime extraction slice

- `UmbracoPrism.WorkflowRuntime` is safe first extraction target: move reusable engine, instance state, definition-store abstraction, and DI extension.
- Keep `BusinessAppWorkflowEngine` as thin adapter over `WorkflowRuntimeEngine` to preserve MockBusinessApp's reviewer/dev hooks while showing clean consumer story.
- Constructor-level fallback to `FilesystemWorkflowDefinitionStore(...)` lets old test construction keep working while DI-based hosts supply `IWorkflowDefinitionStore` explicitly.

### 2026-05-17T20:02:23.686+01:00 | MCP/Copilot workflow gap audit

**What exists:**
- Real backend authoring primitives in `UmbracoPrism.WorkflowEditor/`
- HTTP surface: `GET /workflows`, `POST /workflows/{key}/validate`, `/project`, `/preview`, `/apply`
- **No** shipped NL drafting endpoint, **no** MCP server/tool host

**Current NL reality:**
- Editor routes NL text through `workflow-authoring-mock-drafter.ts` (canned local matcher for "id&v")
- `prism-conversation-pane.ts` simulates agent acknowledgement locally

**Runtime boundary:**
- Apply persists only authored JSON + provenance; live runtime still loads from `workflow-seeds/`
- Apply does not currently republish runtime seeds

**Reference/demo indicators:**
- Design docs describe MCP/Copilot path as aspirational
- Agent-loop test coverage marked as skipped/fixme pending MCP implementation

### 2026-05-17T22:05:30.472+01:00 | Workflow Editor Backend Extraction — First Slice

**Status:** ✅ Complete  
**Commit:** 9ab9ba4

Completed first extraction slice: moved all backend authoring code from Core into new `UmbracoPrism.WorkflowEditor` library.

**Implementation:**
- Scaffolded `UmbracoPrism.WorkflowEditor` Razor Class Library (net10.0)
- Moved 23 C# files: domain models, projector, patch/preview services, HTTP endpoints
- Updated namespaces: `UmbracoPrism.Core.Workflow.Authoring` → `UmbracoPrism.WorkflowEditor.Authoring`
- Added consumer API: `AddPrismWorkflowEditor()` / `MapPrismWorkflowEditor()`
- Migrated MockBusinessApp Program.cs to use new API
- Updated 7 test files with new namespace imports

**Validation:**
- ✅ Full solution build succeeds
- ✅ All 51 workflow authoring tests pass (47 passed, 4 skipped)
- ✅ Backward-compat maintained

**Key Learnings:**
- **SDK choice:** Used `Microsoft.NET.Sdk.Web` (not `Razor`) to avoid Blazor deps
- **Static asset isolation:** Template wwwroot caused path conflicts; `EnableDefaultContentItems=false` prevents discovery
- **Git rename detection:** Moving files then updating namespaces triggers clean rename detection

**Next Slice Boundary:**
1. Frontend extraction: Move Vite-built UI assets into WorkflowEditor library
2. Authoring fixture relocation: Consider library-embedded defaults
3. Deprecation path: Add `[Obsolete]` in V2, remove in V3

### 2026-05-17T22:05:30.472+01:00 | Design rewrite batch — Runtime action model solidified

- Rewrote `docs/design/workflow-editor-v1/02-runtime-projection.md` as simplified workflow engine/action model document: workflow definition, action catalog, workflow engine, action handlers.
- Produced two decisions merged to `.squad/decisions.md`:
  1. **blathers-workflow-action-model.md** — Handler registry pattern for runtime actions (declarative refs + business-app handlers; no callbacks)
  2. **blathers-workflow-engine-doc.md** — Action system split (design-time catalog for editor discovery + runtime handler dispatch)
- Rationale: Portable/reviewable workflows; no C# leakage into JSON; matches Prism split (renders vs decides)
- Forms-backed actions and email actions fit same typed-action contract; registry supports DI/testing/extensibility

## Learnings

### Authored Workflow V1 Foundation (2026-05-16)

**Authored types:** `src/UmbracoPrism.Core/Workflow/Authoring/`, namespace `UmbracoPrism.Core.Workflow.Authoring`. All types are immutable records, JSON-serializable via STJ.

**Determinism:** `WorkflowProjector.CanonicalOptions` — CamelCase naming, no indent, never ignore conditions, relaxed escaping. Stages sorted by `StageKey`, transitions by `(FromStage, ToStage, Action)`, fields by `Key`. SHA-256 of canonical UTF-8 bytes gives `ProjectionResult.Checksum`. Locked by `WorkflowProjectorDeterminismTests`.

**Shell inference:** `WorkflowProjector` emits components (FieldsetComponent, SummaryListComponent, etc.) whose presence causes `PrismComponentExtensions.InferStepType()` to return correct step type. No duplication — projector drives inference via component choice.

**Planning fixture:** `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/planning.workflow.json` — 4 stages. Source of truth for Tangy's tests.

### Workflow schema foundation for issue #55 (2026-05-18T13:17:12.103+01:00)

- Authored workflow contract now lives in `src/UmbracoPrism.WorkflowEditor/Authoring/` with a companion schema document at `src/UmbracoPrism.WorkflowEditor/Authoring/Schemas/authored-workflow.schema.json`.
- Locked persisted JSON names: stage `key`/`title`/`type`, transition `source`/`target`/`trigger`, action `type`/`timing`/`params`, plus top-level `parameterSchemas` for reusable action parameter validation.
- `AuthoredWorkflowSchemaValidator` now enforces stage/action/transition shape and parameter-schema compatibility before projection, while `WorkflowProjector` keeps projecting into unchanged `WorkflowDefinitionFile` runtime contracts.
- Backward-compat shim: `AuthoredStage` and `AuthoredTransition` accept legacy proposal payload aliases (`stageKey`, `displayName`, `kind`, `fromStage`, `toStage`, `action`, `condition`) so patch/preview flows stay green while saved files move to the new schema.
- Key tests: `src/UmbracoPrism.Core.Tests/Workflow/Authoring/AuthoredWorkflowSerializationTests.cs`, `AuthoredWorkflowSchemaValidationTests.cs`, `PlanningWorkflowFixtureTests.cs`.

### Action runtime model refinement

- Keep authored JSON declarative: transition verbs in graph, executable business actions as typed references (`type` + `params`)
- Never put callbacks or code-like lambdas in JSON
- Use DI-backed handler registry: business app publishes descriptors/schemas for discovery, resolves same keys to typed C# handlers at runtime
- Forms-backed and future actions (email, identity-verification) use same extension shape

---

See history-archive.md for pre-2026-05-16 history.


### Copilot-facing workflow integration surface (2026-05-17T22:21:16.980+01:00)

- Prefer a thin proposal-first MCP surface: draft/propose, validate, preview/diff, and apply/publish remain separate operations with human approval before mutation.
- Copilot should learn orchestration via skills; domain truth should come from runtime-advertised tool metadata (workflow schema version, stage kinds, action catalog, actor/service-zone rules, ambiguity candidates, validation classes).
- First backend tool slice should expose authored-workflow summary/read, draft-proposal, validate, preview, diff, and apply; publish can remain coupled to apply in V1 if runtime seed regeneration is synchronous.
- Keep NL prompting guidance and conversation choreography in Copilot skills, not in the MCP tools themselves; keep graph semantics, insertion-point resolution, projection, and validation in workflow-aware backend services.
- Relevant paths: `docs/design/workflow-editor-v1/02-runtime-projection.md`, `docs/design/workflow-editor-v1/04-agentic-surfaces.md`, `src/UmbracoPrism.WorkflowEditor/Authoring/Http/WorkflowAuthoringEndpoints.cs`, `.squad/skills/workflow-action-handler-registry/SKILL.md`, `.squad/skills/workflow-editor-simple-system-frame/SKILL.md`.

### 2026-05-17T21:24:00Z | Copilot-facing workflow integration surface design

**Batch:** AI integration design  
**Decision published:** "Copilot-facing workflow integration surface"

Defined concrete Copilot-facing tool surface and thinnest viable first implementation for conversational workflow/service design. Specified thin MCP surface anchored on workflow-aware semantics (`draft-proposal`, `validate`, `preview`, `diff`, `apply`) with human approval required before apply.

**Key outcomes:**
- Established responsibility split: Copilot handles natural-language orchestration; backend tools handle workflow semantics
- Designed proposal-first integration preserving editor-first product model
- Described first implementation shape using existing authoring HTTP/backend seams with thin adapter

**Peers:** tom-nook (architecture evaluation), scribe (orchestration)


## Session: 2026-05-18 — Issue #55 Schema Foundation Implementation

**Date:** 2026-05-18T12:35:32Z  
**Issue:** #55 (workflow-schema-foundation)  
**Outcome:** ✅ Complete

Completed authored workflow contract definition in UmbracoPrism.WorkflowEditor/Authoring/. Delivered:
- Typed stage/transition/action/condition/parameter-schema C# models
- authored-workflow.schema.json artifact
- Validator implementation with comprehensive test coverage
- Legacy alias compatibility for patch payloads (stageKey → key, displayName → title, kind → type, etc.)
- Planning fixture/test alignment

**Validation:**
- dotnet build UmbracoPrism.sln ✅ succeeded
- dotnet test -c Release ✅ passed (761/761)
- Pre-existing warnings unchanged

**Branch:** squad/55-workflow-schema-foundation

