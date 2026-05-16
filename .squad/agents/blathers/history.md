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
