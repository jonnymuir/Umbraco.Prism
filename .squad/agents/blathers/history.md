# Blathers — History

Backend Developer specializing in core infrastructure and pipeline design.

**Current Focus:**
- Vinyl/Core notification boundary refactor COMPLETED
- Core notification infrastructure remains reusable and stable
- TestSite vinyl behavior now opt-in (configuration-driven)

**Status:** All 851 backend tests passing; 0 build warnings

## Key Learnings

- Fixture ordering safety: Use `WorkflowAuthoringFixtureLocator.GetFixturesPath()` for shared fixtures, not direct Assembly.Location paths. xUnit test collection scheduling creates races with concurrent fixture resets.
- Projection error handling: Startup publishing must check `PublishResult.HasErrors` and log diagnostics selectively by severity.
- Workflow routing: Editor and runtime serve the same workflow when `PlanningWorkflowKey` matches the authored `DefinitionKey`.
- Process cleanup: Use specific PIDs (`kill $PID`), not name-based (`pkill`, `killall`) per security guidelines.
- Aspire cleanup: Wire `postDebugTask` in `.vscode/launch.json` to clean up child processes spawned by DCP on debugger stop.
- **Filesystem durability (2026-05-24):** Always call `stream.FlushAsync()` explicitly before closing file streams in write operations that are immediately followed by read verification. Linux CI environments with virtualized/networked filesystems cache directory metadata; relying only on `await using` disposal isn't sufficient to guarantee File.Exists() sees the new file. This manifested as intermittent HTTP 500 failures in `PostApply_WithExistingWorkflow_PublishesRuntimeDefinition` where `PublishAsync` couldn't reload the just-saved workflow JSON for round-trip verification.

## 2026-05-31 — Slice B: Authoring stack leaves WorkflowEditor; publish moves into MockBusinessApp

**Session:** named-lanes editor — Slice B (DDD boundary, backend cut)  
**Branch:** `squad/82-named-lanes-editor-slice`

**Scope:** Tear out `/api/workflow-authoring/*` from `UmbracoPrism.WorkflowEditor`, move the publish stack into MockBusinessApp where it belongs, and replace the authoring endpoints with anonymous `/mockapp/workflows/*` CRUD on a singleton in-memory store.

**Outcomes:**
- ✅ Deleted 11 production files from `UmbracoPrism.WorkflowEditor`: both `Authoring/Http/*` endpoint files, both extension files (`WorkflowEditorEndpointExtensions`, `WorkflowAuthoringPolicies`), and the entire `IAuthoredWorkflowStore` family + `IWorkflowAuthoringProvenanceStore` family (in-memory + filesystem flavours of each, plus `AuthoredWorkflowStoreEntry`).
- ✅ Moved 6 publish-stack files (`git mv`) from `UmbracoPrism.WorkflowEditor/Authoring/` to `UmbracoPrism.MockBusinessApp/Services/Publishing/` — `WorkflowPublishService`, `IWorkflowPublishService`, `PublishResult`, `PublishPreviewResult`, `IPublishedWorkflowStore`, `FilesystemPublishedWorkflowStore`. Renamespaced to `UmbracoPrism.MockBusinessApp.Services.Publishing` and re-added `using UmbracoPrism.WorkflowEditor.Authoring;` for `WorkflowProjector.CanonicalOptions`.
- ✅ Trimmed `WorkflowEditorServiceExtensions.AddPrismWorkflowEditor()` to a no-arg call (registers only projector / patch / simulation / action catalog / parameter widget mapper). Hosts wire their own persistence.
- ✅ Created `ReferenceAuthoredWorkflowStore` (singleton, in-memory, seeded from `ReferenceWorkflowRepository.GetReferenceWorkflows()`) and three anonymous endpoints under `/mockapp/workflows/*` (list, GET, PUT). Key regex `^[a-zA-Z0-9_\-]+$`, ProblemDetails on bad JSON. **No auth, no CORS** — same-origin reference host posture, deliberate.
- ✅ Major `Program.cs` surgery: dropped CORS, dropped the `WorkflowAuthor` auth policy, dropped store registrations, dropped `MapPrismWorkflowEditor()`, dropped the `/api/workflow-authoring` middleware guard, dropped the legacy `/admin/workflow/definition/{key}/json` GET+PUT, and dropped the JSON modal HTML/CSS/JS + ace.js CDN + `ResolveWorkflowDefinitionKeyAsync` helper + Edit JSON button.
- ✅ Validated: `dotnet build UmbracoPrism.sln` green / 0 warnings / 0 errors; `dotnet test` 814 passed / 0 failed / 11 skipped (was 860 — 46 tests deleted with the obsolete stores).

**Peers:** Isabelle (TS boundary + editor rewrite + integration example), Brewster (test-infra refit and FourWorkflow contract rewrite against `/mockapp/workflows/*`).

## 2026-05-23T13:04:58.778000+00:00 — Session: Vinyl/Core Boundary Integration

All squad members deployed together to complete the vinyl/core boundary work. Architecture split successful:
- Core remains reusable notification infrastructure
- TestSite vinyl behavior is now opt-in
- All 815 tests passing
- 0 warnings in build/test lane

Decision doc merged to decisions.md; full session log at `.squad/log/2026-05-23T13:04:58.778000+00:00-vinyl-core-boundary-integration.md`

## 2026-05-24 — CI Red Run Resolution

Fixed Linux CI apply/publish flush race condition in WorkflowAuthoringEndpoints.PostApply causing backend 500 errors. Local backend validation passed. Decision logged: `blathers-ci-apply-regression.md`.

## Learnings

- 2026-05-25T12:49:20.153+01:00 — For multi-lane workflow slices, land workflow-level lane and gateway metadata first, then project effective actor/role assignment back onto published state metadata so current runtime behaviour stays stable while later issues add split/join execution.
- Multi-cursor join pattern: `Cursors = []` means single-cursor legacy mode; `Cursors` populated means multi-cursor. Keep `CurrentState` in sync with `FirstActiveStageCursorKey()` on every save so legacy callers never see a cursor-only state key.
- FluentAssertions `ContainInOrder` overload: pass expected values as `IEnumerable<T>`, and reason string as the second argument. Passing the reason as a trailing string in the params-array treats it as an additional expected element.
- PROJ137/138/139: any pre-existing test using a Join gateway must now also provide `WaitingInfo` and `RequiredIncomingLanes`; the schema validator enforces these from the authoring layer upward.
- 2026-05-25T16:48:28.029+01:00 — Gateway-only authoring means canonical transitions use node-level source/target/trigger across stages and gateways; direct stage-to-stage links and stage-level waiting are invalid, and join gateways own waiting/defer metadata.

## 2026-05-25T16:48:28Z — Gateway-Only Redo: Runtime & Authored Model Alignment

**Task:** Rebuild gateway-only runtime model; realign backend/runtime contract  
**Status:** ✅ Complete

### Decision: Gateway-only authored routing and runtime alignment

- Canonical transitions now use `source`, `target`, `trigger` (uniform for stage and gateway nodes)
- Backend validation rejects direct stage-to-stage transitions (`PROJ141`) and stage-level waiting (`PROJ140`)
- Join gateway metadata carries full waiting contract + defer affordance
- Reference workflows and fixtures now route through explicit gateways (including pass-through gateways for linear flows)

### Frontend Coordination Gap (Deferred)

Current workflow editor client still carries hybrid assumptions:
- `types.ts` models transitions as `fromStage`/`toStage` with shims; still allows stage-level waiting
- `workflow-authoring-client.ts` normalizes gateway waiting incorrectly
- `workflow-gateway-representation.ts` infers gateway visuals heuristically (should use first-class authored gateways)

→ Documented for Isabelle/dedicated frontend alignment slice

### Orchestration Log

Written to `.squad/orchestration-log/2026-05-25T15-48-28-blathers.md`

### Backend Validation Status

All backend publishing and validation now aligned to gateway-only contract. Ready for frontend UX layer.

---

## [2026-05-25T12:00:03Z] Scribe: Spawn Manifest Processing

**Activity:**
- Orchestration log written
- Decisions inbox merged (9 files processed)
- Cross-agent updates logged
- Session log recorded

**Status:** ✓ Manifest processed, ready for next cycle


## 2026-05-25T14:34:44.680Z — Merged Gateway Runtime Slice Implementation

**Spawn:** blathers background agent  
**Task:** Build merged gateway runtime slice (#83/#84/#85)  
**Outcome:** ✅ Complete (PR #89 open)

### Deliverables

- `WorkflowCursor.cs` — Per-lane cursor records
- Extended `WorkflowInstanceState.cs` with `Cursors` and `JoinArrivals` bookkeeping
- Split/join gateway dispatch in `WorkflowRuntimeEngine.cs`
- Schema validation codes PROJ137, PROJ138, PROJ139 for join gateway completeness
- Join waiting envelope sourced from `WorkflowGatewayDefinition.WaitingContent` (not fake stages)
- Backward-compatible cursor model: legacy single-cursor workflows show no regression
- `RequiredIncomingLanes` emitted in sorted order for deterministic publish output

### Quality Gate

✅ All 851 tests passing  
✅ Backend authoring: 129 passed, 3 skipped (deferred semantics)  
✅ Workflow serialization/schema/publish: green  
✅ `dotnet test UmbracoPrism.sln`: green  
✅ Branch clean; PR #89 ready for review  

### Files Modified

- `AuthoredGateway.cs` — `Description`, `WaitingInfo`, `RequiredIncomingLanes`
- `WorkflowDefinitionFile.cs` — published gateway fields
- `WorkflowProjector.cs` — gateway-targeted transitions
- `AuthoredWorkflowSchemaValidator.cs` — PROJ137/138/139
- `WorkflowCursor.cs`, `WorkflowInstanceState.cs`, `WorkflowRuntimeEngine.cs` — NEW/extended
- Test files: 17 new tests (gateway projection + engine behavior)

### Cross-Layer Coordination

- Isabelle's editor-only fields NOT yet in C# model (deferred for later alignment)
- Backend publish pipeline decision deferred (strip or preserve on publish)

**Orchestration log:** `.squad/orchestration-log/2026-05-25T14-34-44-blathers.md`


## 2026-05-30T11:15:00+01:00 — Slice 1 (backend): proposal preview removal

**Task:** Delete preview path (`IWorkflowPreviewService`, `WorkflowPreviewService`, `PreviewResult`, `SemanticDiff`) and the `/workflows/{key}/preview` endpoint. Keep `PublishPreviewResult` / `PublishResult` / `ProposalEnvelope` — those are the save/apply protocol, not the diff preview.
**Status:** ✅ Complete — commit 1e8bbcf on `squad/82-named-lanes-editor-slice`. Build 0W/0E; 842 Core tests green.

### Learnings

- The naming overlap between `PreviewResult` (semantic-diff preview, deleted) and `PublishPreviewResult` (publish dry-run, kept) is a real footgun. The directive's explicit "DO NOT DELETE" list was essential — a naive grep for `Preview` would have wiped the publish dry-run too.
- The endpoint was the only consumer of `IWorkflowPreviewService` in production code; once the endpoint went, the DI registration in `WorkflowEditorServiceExtensions` (line 39, plus a docstring mention) was the only other backend touchpoint.
- The `/preview` endpoint composed both services: `previewService.Preview(...) with { PublishPreview = publishService.PreviewAsync(...) }`. Worth noting for any future "what does a save dry-run look like?" question — the answer post-slice is "call `IWorkflowPublishService.PreviewAsync` directly via /apply, no separate endpoint."
- Pattern to watch: working tree arrived with ~50 unrelated pre-staged changes (Isabelle's TS work, Tom-Nook's docs, mock app, runtime engine). Had to use `git add` only on my four target paths and `git restore --staged` on three `prism-proposal-diff*` / `workflow-authoring-mock-drafter.ts` deletions that someone had pre-staged. The commit ended up cleanly 8 files / +1 / -431, exactly the backend slice.
- Test count: 842 (was 851 in the gateway-runtime slice; the diff is preview tests + skipped deferred-semantics tests being removed elsewhere in the working tree). No regressions in my scope.

---

## 2026-05-30 — Scope-Reset Session: Slice 1 Backend Complete

**Session:** workflow-editor-scope-reset  
**Role:** Implementation (backend deletions)

**Outcomes:**
- ✅ Slice 1 backend deletions (5 files deleted, 3 files edited, commit 1e8bbcf)
- ✅ Preview service removal (PreviewService.cs, endpoints, DTOs)
- ✅ Dependency injection cleanup (ServiceRegistration.cs)
- ✅ Full test suite green (842 tests pass, 0 warnings, 0 errors)

**Key Notes:**
- Slice 1 backend deletions complete and safe
- All build/test checks passed
- Frontend deletion by isabelle followed (Slice 1 frontend deletions)
- 3 git stashes preserved on branch (untouched, pending Slice 3/5)

---

## 2026-05-30 — Slice 3c: workflow-authoring security hardening (squad/82)

**Scope:** Closed the three CRITICAL/HIGH must-fix items from Copper's
`copper-editor-reset-security-review.md`: (1) auth on `/api/workflow-authoring/*`,
(2) approver derived from `HttpContext.User` (not body), (3) workflow-key
sanitisation + filesystem path-containment.

**Shipped:**
- `WorkflowAuthoringPolicies.WorkflowAuthor` constant; group
  `.RequireAuthorization` on `MapPrismWorkflowEditor`; MockBusinessApp wires
  `RequireAuthenticatedUser`; non-Dev 404 guard extended.
- **BREAKING** `ApplyWorkflowRequest.Approver` removed; `/apply` resolves the
  approver via the same `preferred_username → email → name → Identity.Name`
  ladder as `PrismIdentityExtensions.GetEmail`. Human-assisted agents get a
  cross-stamp check on `envelope.Agent.Identity`.
- Endpoint-layer `^[a-zA-Z0-9_-]+$` regex on `{key}`; defence-in-depth
  `ResolveSafePath` containment guards added to all three Filesystem*Store
  classes.
- Dev CORS tightened from `AllowAnyOrigin` to a configurable
  localhost:5173/127.0.0.1:5173 allowlist.
- 16 new behavioural tests in `WorkflowAuthoringEndpointSecurityTests.cs` plus
  Tangy's two pin tests (bare-`waiting` PROJ140 branch and AuthoredTransition
  legacy-shim round-trip). One stale `PostApply_WithMissingApprover…` test
  deleted. **862/862 tests green, stable on repeat.**

**Test infrastructure learnings:**
- `WebApplicationFactory<T>.ConfigureWebHost` re-fires on every
  `CreateClient()` / `WithWebHostBuilder()` call. Anything stateful inside
  (file resets, dir cleanup) must be guarded once-per-process — added three
  `static bool _xInitialised` + `static object _xGate` pairs.
- `ResetAuthoredFixturesDirectory`'s previous "delete-all then copy-all"
  shape raced with sibling test classes' readers — `IOException: file in use`.
  Final shape: skip the copy entirely when the target already exists (csproj
  `<Content Include>` mirrors source on build), and only delete files not in
  the canonical source set.
- Header-driven `Test` auth scheme handler with `X-Test-User` makes 401
  assertions trivial — omit the header to get the policy challenge.
- `[CollectionDefinition("WorkflowAuthoringFactory")] + ICollectionFixture`
  forces the auth-touching classes to share one factory + run serially.
- Provenance filename has second-granular UTC stamp — two `/apply` calls in
  the same wall-clock second silently overwrite. Snapshot-diff tests are
  fragile; better to read `provenancePath` from the response body and assert
  on it directly.

**Iframe follow-up (flagged to Squad — not shipped):**
The TestSite dashboard mounts the editor via iframe to BusinessApp:
authenticating BusinessApp's API breaks the iframe's anonymous-fetch
contract. Documented options (Bearer forwarding vs. Brewster's web-component
re-host) in the decision file. Not a backend slice.

**Explicitly deferred:** multi-tenant scoping, `WorkflowPatchService` covert
insert, `WorkflowRuntimeEngine` join forgery (pre-existing), absolute-path
leaks in responses, `/save` vs `/publish` vs `/apply` consolidation.

**Key Notes:**
- BREAKING API change called out in `blathers-slice3c-security-hardening.md`
- All Copper verification-matrix tests pass
- Pre-existing fixture-race exposed during the slice was repaired as a
  by-product

## 2026-05-30 — Slice 8a: collapse write surface + relax ProposalEnvelope (squad/82)

**Scope:** Two of Tom Nook's worth-noting findings from the editor reset
review: retire the `/save` alias and stop forcing integrators through the
agentic envelope theatre for non-agentic saves.

**Shipped:**
- **Package A — `/save` retired.** Removed the `MapPost("/workflows/{key}/save")`
  endpoint from `WorkflowEditorEndpointExtensions.cs`. `/publish` is now the
  canonical direct save; `/apply` keeps envelope-mediated saves with provenance.
  Fixed the duplicated `/publish` route-header comment that had marked both
  endpoints with the same banner. Renamed the two `PostSave_…` tests in
  `WorkflowAuthoringEndpointsTests` to `PostPublish_…` and pointed them at
  the surviving route, then added a pin test that `/save` on a real workflow
  returns 404. Dropped the `/save` row from the unauth theory and renamed
  `PostSave_WithUnsafeKey…` to `PostPublish_WithUnsafeKey…`.
- **Package B — `ProposalEnvelope` relaxed.** `Agent` and `Rationale` are now
  nullable; `Id` and `CreatedAt` stay required for audit. `PatchAgent.Kind`'s
  XML doc became "free-form actor identifier" — the endpoint only validates
  whitespace and only cross-stamps when `Kind == "human-assisted"`. The
  `/apply` endpoint now (a) rejects empty `Ops` with 400 before any other
  validation runs, (b) synthesises a `PatchAgent { Kind = "human-assisted",
  Identity = approver }` when none is supplied, (c) rejects whitespace-only
  `Agent.Kind` when an agent *is* supplied. Provenance store needed no
  changes — the anonymous JSON payload serialises nulls fine.
- **Behavioural tests:** added `WorkflowAuthoringApplyRelaxationTests`
  (4 tests, all green): null-Agent+null-Rationale succeeds and provenance
  synthesises the actor; arbitrary actor string (`planning-bot`) accepted
  verbatim; empty ops returns 400; `/save` returns 404. Test totals:
  866 total / 860 passed / 6 pre-existing manifest failures unchanged.

**Surface I touched:**
- `src/UmbracoPrism.WorkflowEditor/Authoring/ProposalEnvelope.cs`
- `src/UmbracoPrism.WorkflowEditor/Extensions/WorkflowEditorEndpointExtensions.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowAuthoringEndpointsTests.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowAuthoringEndpointSecurityTests.cs`
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowAuthoringApplyRelaxationTests.cs` (NEW)

**Notes / learnings:**
- The SDK client (`workflow-authoring-client.ts`) was already on `/publish` — no
  TS rename or Playwright re-run was required. The `/save` removal is a
  server-side delete with no client churn.
- `BuildMinimalEnvelope` in the endpoint tests previously emitted `Ops = []`;
  with the new "empty-ops → 400" rule that helper now embeds a real
  `update-transition` op that matches an existing planning transition by
  (source, target, trigger), which `WorkflowPatchService` treats as a no-op
  overwrite. Two adjacent security tests that hand-rolled anonymous-object
  envelopes had to be updated the same way to keep exercising their
  identity-mismatch / approver-from-claims assertions instead of tripping the
  new ops-empty check first.
- Order of `/apply` validations now: (1) safe key, (2) parseable body, (3)
  ops non-empty, (4) authenticated approver, (5) agent kind / cross-stamp,
  (6) workflow exists. Each step returns the most specific 400/401/404 it can.
- `WorkflowPatchService` needed no changes — its `Apply` loop is naturally
  empty-safe and it never reads `Agent` or `Rationale`.

**Explicitly deferred (still open):**
- `WorkflowPatchService` covert insert (Copper MEDIUM)
- `WorkflowRuntimeEngine` join-arrival forgery (Copper MEDIUM)
- Multi-tenant scoping (V1 single-tenant by directive)
- Backoffice editor re-introduction (permanently rejected)

- 2026-05-31 — Slice A legacy purge (backend, branch `squad/82-named-lanes-editor-slice`). Stripped every `Legacy*`/`[Obsolete]` shim from `AuthoredStage` (LegacyStageKey/LegacyDisplayName/LegacyKindLiteral/LegacyKindRaw/LegacyWaitingPayload/HasLegacyWaitingPayload + private `_legacyKindRaw`/`_hasLegacyWaitingPayload`) and `AuthoredTransition` (LegacyFromStage/LegacyToStage/LegacyAction/LegacyCondition init shims + 3 `[Obsolete]` FromStage/ToStage/Action getters). Replaced silent-rewrite-to-Question with hard error: unknown stage kinds are now captured on a private `_unknownKindToken` (exposed as `UnknownKindToken`) and the schema validator surfaces a new **PROJ005 "Unknown stage kind '<x>'. Allowed kinds: Question, CheckAnswers, Confirmation, TaskList."** Deleted the PROJ140 path entirely (waiting-payload binding is gone — empty `type` still defaults to Question to mirror `Enum.TryParse`'s early return; only an explicit non-empty unknown token errors). Tests: replaced the legacy shim round-trip test with a "retired alias does not populate Source" assertion, replaced PROJ140 tests with PROJ005, renamed the legacy-route 404 test. **Audit finding beyond Tom Nook's plan:** `WorkflowPatchServiceTests` and `WorkflowPatchServiceFailureTests` were using `stageKey`/`displayName`/`kind` legacy aliases in their anonymous-object payloads — sed-migrated to `key`/`title`/`type`. **Reminder for Slice C:** `AuthoredHandoff.FromStage`/`ToStage` are *canonical* on that type (different record); do not conflate with the deleted `AuthoredTransition` aliases. Final: 860/860 Core tests green; build clean.

- 2026-05-31 — Slice C (server portion) — gateways own routes. Deleted `AuthoredTransition` entirely. `AuthoredGateway` gained `Source` (required on Split, forbidden on Join) + `Routes` (`IReadOnlyList<AuthoredRoute>`). New `AuthoredRoute` record (`Id`, `Target`, `Trigger`, `Condition`, `RequiresRole`, `Actions`). `AuthoredWorkflow.Transitions` removed. Rewrote `AuthoredWorkflowSchemaValidator` (new PROJ141–PROJ152; retired PROJ106–109 + old PROJ141/142), `WorkflowProjector` (emits transitions from `gateway.Source × routes`), `WorkflowSimulationService` (full rewrite — `gatewayBySourceStage` lookup, `ResolveNextStage` chains through gateways), `WorkflowPatchService` (`add-route` / `update-route` / `delete-route` ops on path `/gateways/{key}/routes/{id}`). Schema dropped top-level `transitions`; gateway shape now conditionally requires `source` only for Split. Multi-target fan-outs require `(trigger, target)` uniqueness — deliberate evolution from spec wording for routers like payment-demo. All four reference workflows reshaped (planning, community-enquiry, information-request, payment-demo) in MockBusinessApp + Core.Tests fixtures + client planning fixture. Test status: 811/811 Core.Tests green, full solution build 0/0. **Outstanding for follow-up:** TS types collapse, graph (3350 LOC), inspector (1688 LOC), wire-format, fixtures/index.ts, stories, Playwright specs, MockBusinessApp admin-page strip, walkthrough corrections. See `.squad/decisions/inbox/copilot-slice-c-gateways-own-routes.md`.
