# Post-reset audit + slice plan — three architectural corrections

**By:** Tom Nook (Lead)
**For:** Jonny Muir
**Date:** 2026-05-31
**Branch:** `squad/82-named-lanes-editor-slice` at `66ea003 + 6d84e39`
**Inputs:** `copilot-directive-20260531T091300Z.md` (three directives)

This is a plan, not code. It audits the current tree against the three directives and proposes the slices that land them. Bias: fewer, larger slices that each leave the system coherent.

---

## 1. Audit findings

### Directive 1 — Legacy cleanup

**What "legacy" means in this codebase:** `[Obsolete]` shims on `AuthoredTransition`, `Legacy*` JSON setters on `AuthoredTransition` and `AuthoredStage`, the `HasLegacyWaitingPayload` / `LegacyKindRaw` sentinel pair, and the matching TS-side normalisers + validation issue.

Workflow-domain hits (the only ones in scope — the OIDC/Codespace/`appsettings-schema.Umbraco.Cms.json`/`PrismComponentTagHelper.cs`/`WorkflowRenderShellResolver.cs` "legacy" matches are unrelated and stay):

**Backend:**
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredTransition.cs:23-31` — `LegacyFromStage` JSON setter
- `…/AuthoredTransition.cs:34-40` — `[Obsolete] FromStage` shim
- `…/AuthoredTransition.cs:50-58` — `LegacyToStage` setter
- `…/AuthoredTransition.cs:61-67` — `[Obsolete] ToStage` shim
- `…/AuthoredTransition.cs:77-85` — `LegacyAction` setter
- `…/AuthoredTransition.cs:87-94` — `[Obsolete] Action` shim
- `…/AuthoredTransition.cs:100-114` — `LegacyCondition` single-string setter
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredStage.cs:15-16, 26-35` — `_legacyKindRaw`, `_hasLegacyWaitingPayload`, `LegacyStageKey`
- `…/AuthoredStage.cs:45-54` — `LegacyDisplayName`
- `…/AuthoredStage.cs:81-94, 96-112` — `LegacyKindLiteral`, `LegacyKindRaw`, `ApplyKindToken` token capture
- `…/AuthoredStage.cs:141-157` — `LegacyWaitingPayload`, `HasLegacyWaitingPayload`
- `src/UmbracoPrism.WorkflowEditor/Authoring/WaitingMetadata.cs:5` — comment about "legacy stage-level waiting payloads still deserialize"
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredWorkflowSchemaValidator.cs:49-55` — PROJ140 reads `LegacyKindRaw` + `HasLegacyWaitingPayload`

**Frontend:**
- `src/UmbracoPrism.Client/src/workflow-editor/types.ts:50` — `legacyKindRewrittenFrom?: 'Waiting' | 'StatusTimeline'` on `AuthoredStage`
- `…/workflow-validation.ts:28, 231-247, 287` — `stage-legacy-kind-rewritten` issue code + emitter
- `…/workflow-authoring-client.ts:26-45` — `stripLegacyStageSurface` outbound scrubber
- `…/workflow-authoring-client.ts:104-123` — `mapStageKind` Waiting/StatusTimeline downgrade
- `…/workflow-authoring-client.ts:47-65` — `serialiseTransition` translating `fromStage/toStage/action` → `source/target/trigger`
- `…/workflow-authoring-client.ts:198, 230-247` — inbound dual-key normaliser (`raw.source ?? raw.fromStage` etc.)

**Tests:**
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/AuthoredWorkflowSerializationTests.cs:325-…` — `AuthoredTransition_LegacyShimRoundTrip_FromStageToStageAction_ReadBackViaSourceTargetTrigger` (with `#pragma warning disable CS0618`)
- `…/AuthoredWorkflowValidationTests.cs:130-165` — bare-sentinel test pinning the `HasLegacyWaitingPayload` branch
- `…/WorkflowAuthoringEndpointsTests.cs:348` — `PostSave_LegacyAliasRoute_IsRetiredAndReturnsNotFound` (legacy *route* — already a deletion test; safe to keep semantically but rename)
- `src/UmbracoPrism.Client/tests/walkthroughs/planning-notification.walkthrough.spec.ts:1` — file-level "Legacy" comment

**What's wrong with it:** these aliases are why Slice 3a's Stage rename couldn't fully close. Current data flow is: TS still emits `fromStage/toStage/action` on the wire **on every save** (see `serialiseTransition`), then the C# `LegacyFromStage` setter rewrites it back to `Source`. The "obsolete" shim is the live path. PROJ140 is the only real value left in `HasLegacyWaitingPayload` / `LegacyKindRaw`, and that rule disappears entirely with directive 3 (gateways own waiting metadata; stages can't carry it because they don't carry routes).

**Regression risk:** none expected. Pre-1.0, no external authors. The four reference fixtures already use canonical `key/title/type/source/target/trigger`. Verify by grepping `workflow-seeds/` and `Fixtures/` for `fromStage|toStage|stageKey|displayName|kind\b|waiting` once Slice A lands.

---

### Directive 2 — Editor abstraction

**Current coupling (the symptom site):**

- `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts:358-466` exports five HTTP functions: `listWorkflows`, `fetchActionCatalog`, `fetchWorkflow`, `publishWorkflow`, `projectWorkflow`. Line 397 throws the `Failed to fetch workflow "<key>": <status>` error Jonny saw.
- `prism-workflow-editor.ts:11-15, 258, 278, 559, 1354` consumes all four save/load functions directly, parameterised only by `_resolvedAuthoringApiBase`.
- `prism-workflow-editor-shell.ts:5-10, 47-52` consumes `listWorkflows` directly.
- Both elements expose `authoring-api-base` as an attribute — there is no other seam.
- Stories (`prism-workflow-editor.stories.ts:42`, `prism-workflow-editor-shell.stories.ts:203`) already work around this by intercepting `fetch` and routing to `projectWorkflowLocally`. That is a tell: the abstraction wants to live one level up.
- Backend `src/UmbracoPrism.WorkflowEditor/Extensions/WorkflowEditorEndpointExtensions.cs` maps `/api/workflow-authoring/{action-catalog,workflows,workflows/{key},…/validate,…/project,…/publish,…/simulate,…/apply}` — this is the authenticated surface added in Slice 3c.

**Call chain today:**
`<prism-workflow-editor-shell>` → `listWorkflows(apiBase)` → fetch → `<prism-workflow-editor>` → `fetchWorkflow(key, apiBase)` / `fetchActionCatalog` / `projectWorkflow` / `publishWorkflow` → fetch.

**What's wrong:** the editor depends on a network protocol it doesn't own. An integrator without HTTP infrastructure can't host the editor without standing up the whole `/api/workflow-authoring/*` surface. Tests, stories, and Storybook all have to fake the network.

**Proposed abstraction (suggested name `WorkflowSource`):**

```ts
// One interface. Plain product language. Lives in src/workflow-editor/workflow-source.ts.
export interface WorkflowSource {
  list(): Promise<WorkflowSummary[]>;
  load(key: string): Promise<AuthoredWorkflow>;
  save(key: string, workflow: AuthoredWorkflow): Promise<void>;
  // Action catalog stays here — the editor needs it to render dropdowns,
  // and the in-memory implementation can return the static catalog.
  actionCatalog(): Promise<ActionCatalogEntry[]>;
  // Optional. If absent, editor falls back to projectWorkflowLocally().
  project?(key: string, workflow: AuthoredWorkflow): Promise<ProjectWorkflowResult>;
}
```

**Two implementations ship:**
1. `InMemoryWorkflowSource` (lives in `src/UmbracoPrism.Client/src/workflow-editor/`, exported as part of the package). Constructor takes an array of `AuthoredWorkflow` to seed with; `save` mutates the in-memory copy. Used by stories, tests, MockBusinessApp's editor page. Seeded from the four reference fixtures (`fixtures/index.ts` + community-enquiry/information-request/payment-demo/planning JSON).
2. `HttpWorkflowSource` (existing functions, repackaged as a class). For integrators who *want* HTTP; thin wrapper around the existing `/api/workflow-authoring/*` endpoints. Keeps the door open without forcing it.

**How the editor receives the source:** Lit `@property({ attribute: false })` on both `<prism-workflow-editor>` and `<prism-workflow-editor-shell>`. JS-property assignment is the Lit-friendly idiom for non-serialisable values, and we already use it for `_workflow`. Story/test/host code does `editor.workflowSource = new InMemoryWorkflowSource([...]);` before adding to DOM, the same way stories already inject mock fetch handlers. **No constructor injection, no IoC** — explicit assignment matches Jonny's standing preference.

If `workflowSource` is unset, the editor renders an empty state with a clear message ("No workflow source configured"). No automatic HTTP fallback — that would re-create the coupling.

**Where seeds come from:** A new `src/workflow-editor/fixtures/reference-workflows.ts` module that exports the four reference workflows as plain `AuthoredWorkflow` objects (parsed from the existing JSON). Reused by stories, tests, and MockBusinessApp's editor page.

**Documentation home:** New top-level guide `docs/guides/embedding-the-workflow-editor.md` covering: (a) what `WorkflowSource` is, (b) the in-memory reference, (c) implementing your own (one short example), (d) the optional HTTP adapter for hosts that want it. README plus `docs/guides/README.md` get a one-line pointer. The existing `docs/guides/workflow-editor-composition.md` either redirects here or is rewritten in this same slice.

**Migration order (single slice — see Slice B):** introduce the interface and the in-memory implementation → switch stories and tests to use them → switch `<prism-workflow-editor>` and `<prism-workflow-editor-shell>` to read from `workflowSource` instead of calling fetch helpers → wire MockBusinessApp's editor page to construct an `InMemoryWorkflowSource` from its four authored JSON files → keep `/api/workflow-authoring/*` and `HttpWorkflowSource` as the optional HTTP path.

---

### Directive 3 — Gateways ARE transitions

**Survey:**

- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredTransition.cs` — first-class type, 123 lines, owns `Source/Target/Trigger`, `Conditions`, `Actions`, `RequiresRole`.
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredGateway.cs` — 47 lines. Today carries `key`, `title`, `description`, `kind` (Split/Join), `laneKey`, `actor`, `roleGates`, `waitingInfo`, `requiredIncomingLanes`. **Has no outgoing routes.** The graph edges all live in `AuthoredWorkflow.Transitions`.
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredWorkflow.cs:57-59` — `Transitions` is a top-level collection.
- `AuthoredWorkflowSchemaValidator.cs:148-193` — PROJ106/107/108 validate transition source/target/trigger; PROJ141 forbids stage→stage; PROJ142 forbids gateway→split-gateway; PROJ109 validates conditions; transition action validation. **PROJ141 and PROJ142 disappear when transitions don't exist as an independent concept.**
- `WorkflowProjector.cs:75-88, 411` — transitions ordered + projected 1:1 to `WorkflowTransitionFile`.
- `WorkflowSimulationService.cs:39-148` — walks `workflow.Transitions` to find next stage; when it lands on a Split gateway, it follows the first ordered outgoing transition; on a Join gateway it stops with `waiting-gateway`.
- `WorkflowPatchService.cs:180-197` — `update-transition` patch op against the top-level `Transitions` collection.
- `src/UmbracoPrism.WorkflowEditor/Authoring/Schemas/authored-workflow.schema.json:13, 48-51, 60-63, 119, 152` — `transitions` and `gateways` are sibling top-level arrays; `transitions` is in the required set.
- TypeScript:
  - `types.ts:11-23, 19, 160-180` — `AuthoredWorkflow.transitions: AuthoredTransition[]`, `gateways?: AuthoredGateway[]`.
  - `prism-workflow-graph.ts` (3350 lines) — reads both, with `affectedTransitions`, `_transitionDescriptor`, etc.
  - `prism-step-inspector.ts:155-247, 551-…` — `_renderRouteEditor(transition, transitionIndex)` is *already* the gateway's outgoing-route panel (Slice 3b.1) but it still operates on a flat transitions array indexed by number. The data model didn't catch up.
  - `workflow-gateway-representation.ts` — derives gateway "bindings" by *inferring* anchor stages from the transition graph. This whole file is workaround scaffolding for a model that should have gateways own their routes.
  - `workflow-runtime-projection.ts:172-…`, `workflow-validation.ts`, `fixtures/index.ts` all read `workflow.transitions`.
  - `workflow-canonical-json.ts:11-23` — top-level key order ends `..., stages, gateways, transitions` — change to `..., stages, gateways` (transitions removed).
- Walkthrough/design docs: `docs/walkthroughs/authoring-a-workflow.md`, `…/planning-workflow-editor.md`, `docs/design/workflow-editor-v1/02-runtime-projection.md` ("transitions project to `WorkflowTransitionFile`"), `…/01-authoring-ux.md`, `docs/design/workflow-validation.md` — all mention transitions as authored entities.

**Proposed model collapse — pseudocode shape:**

```csharp
public record AuthoredGateway
{
    public string GatewayKey { get; init; }
    public string DisplayName { get; init; }
    public string? Description { get; init; }
    public GatewayKind Kind { get; init; }            // Split | Join
    public string LaneKey { get; init; }
    public string? Actor { get; init; }
    public IReadOnlyList<string> RoleGates { get; init; } = [];
    public WaitingMetadata? WaitingInfo { get; init; }                       // Join only
    public IReadOnlyList<string> RequiredIncomingLanes { get; init; } = [];  // Join only
    public string Source { get; init; }                                      // the stage (or upstream gateway) feeding in
    public IReadOnlyList<AuthoredRoute> Routes { get; init; } = [];          // outgoing edges
}

public record AuthoredRoute
{
    public string Trigger { get; init; }                            // was AuthoredTransition.Trigger
    public string Target { get; init; }                             // stage key (or another gateway key — chained gateways still allowed)
    public IReadOnlyList<AuthoredCondition> Conditions { get; init; } = [];
    public IReadOnlyList<AuthoredAction> Actions { get; init; } = [];
    public string? RequiresRole { get; init; }
    public string? EditorComment { get; init; }
}
```

**Resulting model:**
- `AuthoredWorkflow.Transitions` — **deleted.**
- `AuthoredTransition` — **deleted.**
- A "simple" stage→stage move (single trigger, no fan-out) is modelled as a Split gateway with one route. Yes, that's slightly more verbose in JSON, but it makes the graph rule "every edge goes via a gateway" structurally true rather than validator-enforced. Editor UX can render a 1-route gateway as a thin pill with the trigger label, so users don't see extra ceremony.
- Validators removed: PROJ106, PROJ107, PROJ108, PROJ109 (now per-route), PROJ141 (impossible by construction), PROJ142 (impossible — gateway→split is now expressible as `Routes[].Target = anotherSplit.GatewayKey` if the user wants chained branching; rule restated as a route-target validity check).
- New/restated validators: per-route trigger required, target valid (stage or gateway), unique route triggers per gateway, etc.
- `WorkflowProjector` — emits one `WorkflowTransitionFile` per `(gateway.Source, route)` pair, with the gateway as the conceptual hop. Runtime contract is unchanged because runtime already understands flat transitions.
- `WorkflowSimulationService` — rewrites: from `currentStage`, find gateways with `Source == currentStage`, match `Trigger`, follow `Route.Target` (stage → return; gateway → recurse; loop guarded by visited set; Join → `waiting-gateway`).
- `WorkflowPatchService` — `update-transition` op replaced by `update-route` (gatewayKey + routeIndex/trigger).
- `workflow-canonical-json.ts` — drop `transitions` from top-level order; routes are nested inside gateways.
- `prism-workflow-graph.ts` — biggest single change. Iterates gateways → routes → renders edges. `workflow-gateway-representation.ts` mostly **deletes** because gateway anchors are now explicit (`gateway.Source`).
- `prism-step-inspector.ts` — `_renderRouteEditor` already operates on a route concept; switch its argument from `(transition, transitionIndex)` to `(gateway, routeIndex)`. That's the alignment Slice 3b.1 promised.
- TS `types.ts` — drop `AuthoredTransition`, add `AuthoredRoute`, add `source` + `routes` to `AuthoredGateway`, drop `transitions` from `AuthoredWorkflow`.
- `authored-workflow.schema.json` — remove `transitions` array; add `source` + `routes` under `gateway`; remove `transitions` from required.
- All four reference fixtures (`Fixtures/*.workflow.json`, `MockBusinessApp/workflow-authored/*.json`) rewritten to the gateway-owned shape. This is a one-time data migration, hand-edited or via a small script kept out of the package.

**MockBusinessApp `/admin/workflow` simplification:**
- Today: ~700 lines of HTML, mermaid state-diagram builder, per-instance action buttons, per-definition JSON edit modal, reset/reset-all, link to editor.
- Keep: workflow list with description and `↗ Edit workflow` link per definition; per-instance state + reset (because the demo needs a way to drive the runtime).
- Remove: the in-page mermaid diagram (the editor does this better), the in-page JSON edit modal at `/admin/workflow/definition/{key}/json` and its endpoints (the editor owns workflow JSON now), action-button generation that re-derives transitions from `def.Transitions` (replace with a generic "advance" prompt or remove entirely if the runtime tests don't need the buttons).

---

## 2. Proposed slice plan

Three slices. One legacy purge, one editor abstraction, one gateway-collapse-plus-doc-and-admin-cleanup.

### Slice A — Legacy purge

**Goal:** delete every "legacy" code path in the workflow domain. After this slice, grepping the workflow surface for `Legacy|legacy|\[Obsolete\]|legacyKindRewrittenFrom` in `src/UmbracoPrism.WorkflowEditor`, `src/UmbracoPrism.Client/src/workflow-editor`, and the four-workflow tests should return empty.

**Owner:** Blathers (backend), Isabelle (frontend) in lockstep — single PR.

**Files in scope:**
- `src/UmbracoPrism.WorkflowEditor/Authoring/AuthoredTransition.cs` — remove `LegacyFromStage`, `LegacyToStage`, `LegacyAction`, `LegacyCondition`, the three `[Obsolete]` shims. (Note: the *type* survives this slice; directive 3 is what deletes it. Don't conflate.)
- `…/AuthoredStage.cs` — remove `LegacyStageKey`, `LegacyDisplayName`, `LegacyKindLiteral`, `LegacyKindRaw`, `LegacyWaitingPayload`, `HasLegacyWaitingPayload`, `_legacyKindRaw`, `_hasLegacyWaitingPayload`. Simplify `ApplyKindToken` — unknown tokens become a hard validation error (new code, e.g. `PROJ005 "Unknown stage kind '<x>'"`) rather than a silent rewrite.
- `…/WaitingMetadata.cs` — remove the "legacy" line in the doc comment.
- `…/AuthoredWorkflowSchemaValidator.cs` — delete PROJ140 (lines ~49-55).
- `src/UmbracoPrism.Client/src/workflow-editor/types.ts` — remove `legacyKindRewrittenFrom` from `AuthoredStage`.
- `…/workflow-validation.ts` — remove `stage-legacy-kind-rewritten` issue code, `legacyKindIssues` block, and its inclusion in `…issues`.
- `…/workflow-authoring-client.ts` — delete `stripLegacyStageSurface`, the Waiting/StatusTimeline branch in `mapStageKind` (return `'Question'` only for the canonical four; unknown becomes an error or default Question — mirror the C# decision), the `fromStage/toStage/action`-emission in `serialiseTransition` (just emit `source/target/trigger` cleanly), the dual-key fallback in `normaliseTransition`.
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/AuthoredWorkflowSerializationTests.cs` — delete `AuthoredTransition_LegacyShimRoundTrip…` test.
- `…/AuthoredWorkflowValidationTests.cs` — delete the bare-sentinel test (PROJ140 is gone).
- `…/WorkflowAuthoringEndpointsTests.cs:348` — rename `PostSave_LegacyAliasRoute_IsRetiredAndReturnsNotFound` to `PostSave_RetiredAliasRoute_ReturnsNotFound`. Word "legacy" goes.
- `src/UmbracoPrism.Client/tests/walkthroughs/planning-notification.walkthrough.spec.ts:1` — drop the "Legacy" prefix from the comment, keep the screenshot test.

**Dependencies:** none. Lands first.

**Behavioural tests to add/rewrite:**
- New unit test: posting JSON with `fromStage` returns a 400 with a clear validation error (no silent rewrite).
- New unit test: posting JSON with `type: "Waiting"` returns a 400 (no silent downgrade).
- Existing fixture round-trip tests must still pass — confirms canonical names already in use.

**Risk + mitigation:**
- Risk: a hidden caller (a test fixture, a seed file) still uses `fromStage/stageKey/displayName/kind/waiting`. **Mitigation:** before merging, grep all `*.json` under `src/UmbracoPrism.MockBusinessApp/workflow-authored`, `src/UmbracoPrism.MockBusinessApp/workflow-seeds`, `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures`, and `src/UmbracoPrism.Client/src/workflow-editor/fixtures` for the dropped keys. Pre-1.0, fix in place.
- Risk: `mockBusinessApp/workflow-seeds/planning.json` is the *runtime* projected shape (states/transitions, not authored stages) — it stays, it's a different file class. Don't accidentally edit it.

---

### Slice B — Editor abstraction (`WorkflowSource`)

**Goal:** the editor no longer calls `fetch` directly. Hosts provide a `workflowSource` property; in-memory is the reference; HTTP is opt-in.

**Owner:** Isabelle (frontend lead), Brewster (MockBusinessApp wiring), Mabel (the new guide in `docs/guides/`).

**Files in scope:**
- New: `src/UmbracoPrism.Client/src/workflow-editor/workflow-source.ts` — interface + `InMemoryWorkflowSource` + `HttpWorkflowSource` (the latter is the existing 5 functions packaged as a class).
- New: `src/UmbracoPrism.Client/src/workflow-editor/fixtures/reference-workflows.ts` — exports the four reference workflows for hosts/tests.
- `prism-workflow-editor.ts` — replace `fetchWorkflow/fetchActionCatalog/projectWorkflow/publishWorkflow` calls with `this.workflowSource.{load,actionCatalog,project?,save}`. Add `@property({ attribute: false }) workflowSource!: WorkflowSource;`. Render an empty state when unset.
- `prism-workflow-editor-shell.ts` — replace `listWorkflows` call with `this.workflowSource.list()`. Remove the `authoring-api-base` attribute machinery (or keep it as a convenience for `HttpWorkflowSource` only — see Open Question 3).
- `prism-workflow-editor.stories.ts`, `prism-workflow-editor-shell.stories.ts`, `prism-workflow-graph.stories.ts`, `prism-step-inspector.stories.ts` — switch from fetch interception to `new InMemoryWorkflowSource([...])`. Stories get *simpler*.
- `src/UmbracoPrism.MockBusinessApp/Program.cs` — the editor page (served at `/workflow-editor.html`) constructs an `InMemoryWorkflowSource` seeded from the four authored JSON files on the server side, and assigns it to the element. (If MockBusinessApp's editor page is currently a static HTML file that just hosts the element via attribute config, this may require a small JS bootstrap — verify during implementation.)
- New: `docs/guides/embedding-the-workflow-editor.md` — the integration recipe (Mabel's voice, plain product language, ~1 page).
- `docs/guides/workflow-editor-composition.md` — rewrite or redirect to the new guide (the existing guide is the half-baked predecessor).
- `docs/guides/README.md`, root `README.md` — pointers.
- `src/UmbracoPrism.Client/tests/workflow-editor/*.spec.ts` — switch any test that mocks `fetch` to instead instantiate `InMemoryWorkflowSource` and assign it. This is a test simplification, not a rewrite.

**Dependencies:** Slice A merged first (so the in-memory source doesn't have to deal with legacy shapes).

**Behavioural tests to add/rewrite:**
- New: editor renders empty state when `workflowSource` is unset (no console errors, no failed fetches).
- New: `<prism-workflow-editor-shell>` lists exactly the workflows the in-memory source returns; selecting one loads it; saving roundtrips through `save → load`.
- New: implementing a custom `WorkflowSource` works — a tiny bespoke source in the test confirms the interface is what hosts actually need.
- Existing: all 88 Playwright specs continue green after switching from fetch-mock to source-injection.

**Risk + mitigation:**
- Risk: `HttpWorkflowSource` adapter has surface drift from the existing functions. **Mitigation:** keep the existing functions as the class's private implementation in this slice; refactor in a future cleanup if they ever need it.
- Risk: MockBusinessApp loses the ability to *edit* workflows from `/admin/workflow` (currently has a JSON modal). **Mitigation:** that admin surface is being simplified anyway (Slice C); confirm with Jonny that "edit JSON via the editor only" is acceptable for the demo (Open Question 2).
- Risk: the `/api/workflow-authoring/*` endpoints are now **only** consumed by `HttpWorkflowSource`, which itself has no in-tree consumer. They're effectively dead weight after this slice unless someone implements an HTTP host. **Mitigation:** flag in Open Question 3.

---

### Slice C — Gateways own routes (model collapse + admin/docs sweep)

**Goal:** `AuthoredTransition` and `AuthoredWorkflow.Transitions` are deleted. Every edge is a route on a gateway. Validators, simulator, projector, frontend, schema, fixtures, walkthroughs, and the MockBusinessApp admin page all reflect this.

**Owner:** Blathers (server model + projector + simulator + validator + tests + JSON schema + fixtures), Isabelle (TS types + graph + inspector + canonical JSON + fixtures + Playwright suite), Brewster (MockBusinessApp admin page), Mabel + Celeste (walkthroughs + design docs). Single coordinated PR. **Largest slice in this arc.**

**Files in scope (high level, not exhaustive):**

Backend:
- Delete `AuthoredTransition.cs`.
- Rewrite `AuthoredGateway.cs` to add `Source` + `Routes` (with new `AuthoredRoute` record).
- `AuthoredWorkflow.cs` — drop `Transitions`.
- `AuthoredWorkflowSchemaValidator.cs` — drop PROJ106-109, PROJ141, PROJ142; add per-route validators (route trigger required, route target resolves to stage or gateway, unique triggers per gateway). Keep PROJ129 (waiting on stage was a thing — but actually this also goes once stages can't have routes/waiting at all? — re-check).
- `WorkflowProjector.cs` — emit `WorkflowTransitionFile` from gateway.Source × routes.
- `WorkflowSimulationService.cs` — full rewrite per the pseudocode above (~80 lines).
- `WorkflowPatchService.cs` — replace `update-transition` op with `update-route` (and probably `add-route`/`delete-route`).
- `Schemas/authored-workflow.schema.json` — drop `transitions`; add `source` + `routes` under `gateway`.
- All `Fixtures/*.workflow.json` — rewritten by hand to the new shape (4 files).
- `MockBusinessApp/workflow-authored/planning.workflow.json` — same.
- All affected backend tests in `src/UmbracoPrism.Core.Tests/Workflow/Authoring/` — rewritten or deleted: `AuthoredWorkflowSchemaValidationTests`, `AuthoredWorkflowSerializationTests`, `WorkflowGatewayProjectionTests`, `WorkflowSimulationServiceTests`, `WorkflowPatchServiceTests`, `MultiLaneGatewayContractTests`, `FourWorkflowReferenceContractTests`, `PlanningWorkflowFixtureTests`, `WorkflowAuthoringApplyRelaxationTests`.

Frontend:
- `types.ts` — drop `AuthoredTransition`; add `AuthoredRoute`; update `AuthoredGateway` (add `source`, `routes`); drop `transitions` from `AuthoredWorkflow`.
- `prism-workflow-graph.ts` — iterate gateways×routes for edges. Expect a substantial diff (~few hundred lines), but the slot-matrix layout itself doesn't change.
- `prism-step-inspector.ts` — `_renderRouteEditor` consumes `(gateway, routeIndex)` directly. Selection state moves from `selectedTransitionIndex` to `selectedRoute = { gatewayKey, routeIndex }` (also collapses one of the parallel selection state fields flagged in your 2026-05-30 history note).
- Delete or shrink `workflow-gateway-representation.ts` — anchors are explicit now.
- `workflow-canonical-json.ts` — drop `transitions` from top-level key order.
- `workflow-validation.ts`, `workflow-runtime-projection.ts` — read from `gateways[].routes` instead of `transitions`.
- `workflow-authoring-client.ts` (or its successor `HttpWorkflowSource` from Slice B) — `serialiseTransition`/`normaliseTransition` deleted; gateway serialisation grows routes.
- `workflow-action-editing.ts`, `gateway-route-conditions.ts` — already largely route-shaped; minor signature updates.
- All `fixtures/*.workflow.json` and `fixtures/index.ts` — update to new shape.
- Playwright specs in `src/UmbracoPrism.Client/tests/workflow-editor/` — most stay (behavioural), the gateway/route specs gain assertions on the new model.

MockBusinessApp:
- `Program.cs` — strip `/admin/workflow` page back to: workflow list (description + `↗ Edit workflow` link per definition), instance list (state badge + reset). Delete the mermaid builder, the JSON edit modal, the `/admin/workflow/definition/{key}/json` GET+PUT endpoints, the per-instance reviewer-action buttons (or keep a generic "advance" if the runtime tests need it — verify).

Docs:
- `docs/walkthroughs/authoring-a-workflow.md`, `…/planning-workflow-editor.md`, `…/workflow-administration.md` — rewrite the "transitions" passages to "routes on gateways". Mabel.
- `docs/design/workflow-editor-v1/02-runtime-projection.md`, `…/01-authoring-ux.md`, `docs/design/workflow-validation.md` — rewrite the model section. Celeste.
- `docs/guides/workflow-customisation.md`, `…/reference-workflow-contract.md` — same.
- `docs/design/workflow-editor-v1/04-agentic-surfaces.md` — already retired in scope-reset; check it's marked historical or delete it.

**Dependencies:** Slices A and B merged. Slice B's `InMemoryWorkflowSource` makes test/story rewrites here much cheaper.

**Behavioural tests to add/rewrite:**
- A "stage submit moves to next stage" test — model expressed as `Split` gateway with one route. Confirms the simplest case still reads naturally in JSON.
- Multi-lane parallel test (planning notification): split gateway fans out, join gateway waits — confirm route-level conditions and required-incoming-lanes still work.
- Simulator test: walking a chain stage → split → join → stage produces the right transcript.
- Validator test: a gateway with no routes is an error; duplicate triggers per gateway are an error; route target unknown is an error.
- Schema-roundtrip test: each of the four reference fixtures parses, projects, and re-emits identically.
- Playwright: editing a route's trigger/condition/target via the inspector saves and reloads correctly through `InMemoryWorkflowSource`.

**Risk + mitigation:**
- Risk: this is the largest single change of the arc. **Mitigation:** the slice can land green because (a) we have ~860 backend + 88 frontend + 3 visual tests as a safety net, (b) Slice B already removed the network coupling so test rewrites are cheap, and (c) the runtime contract (`WorkflowDefinitionFile` with flat transitions) is unchanged — only the *authored* shape collapses.
- Risk: visual regression on the canvas. **Mitigation:** the 3 visual baselines run in CI; expect intentional updates and review them carefully. New baselines committed in this slice.
- Risk: hidden semantic difference in the simulator's handling of multiple outgoing routes from a stage (today: any matching trigger; new model: route under that gateway with matching trigger — same semantics, just clearer location). **Mitigation:** port the existing `WorkflowSimulationServiceTests` cases verbatim and confirm they pass.
- Risk: schema changes break `MockBusinessAppPlanningWorkflowSeedTests` and `StartupWorkflowPublishingTests` in subtle ways. **Mitigation:** these are part of the slice's edit set; rewrite alongside.

---

## 3. Open questions for Jonny

1. **Name of the abstraction.** I've proposed `WorkflowSource` because it's plain product language and reads well in host code (`editor.workflowSource = …`). Alternatives: `WorkflowStore` (matches the C# `IAuthoredWorkflowStore` naming), `WorkflowProvider`. **Default to `WorkflowSource` unless you say otherwise.**
2. **MockBusinessApp `/admin/workflow` JSON edit modal.** It currently lets a demo user paste JSON to update a definition. The directive's spirit is "the editor owns workflow JSON". Are you happy losing that admin-page modal entirely in Slice C? (If you still want a "raw JSON" escape hatch, the editor's Definition tab already provides it.)
3. **Fate of `/api/workflow-authoring/*` and `HttpWorkflowSource`.** After Slice B, no in-tree consumer hits these endpoints — `InMemoryWorkflowSource` is the path. Three options: **(a)** keep them as the documented HTTP integration story (default in my plan), **(b)** mark them experimental/unsupported until someone asks, **(c)** delete them now and tell future HTTP integrators to write their own `WorkflowSource`. I lean (a) but (c) is fully consistent with the directive's "the editor depends on an interface, not a hardcoded API" framing — endpoints existing isn't the issue, the editor *requiring* them is, and once it doesn't, they're optional infrastructure. **Your call.**
4. **Handling of unknown stage kinds after Slice A.** Today: silently rewrite to `Question` (the legacy normaliser). Proposal: hard validation error (`PROJ005 "Unknown stage kind"`). Confirm hard error is what you want, given pre-1.0.
5. **"Simple" stage→stage moves through a 1-route gateway.** This is the structural consequence of "gateways ARE transitions" plus "stages can't go to stages directly". Editor UX rendering can disguise the 1-route gateway as a thin pill. Confirm you're happy with the model shape; the alternative (treat single-route moves as a special case) reintroduces a transition concept by another name and I think you don't want that.
6. **Do we keep `AuthoredHandoff`?** Not in the directives, but it's an authored type that lives alongside transitions/gateways and carries similar semantics. Out of scope for this arc unless you flag it.

---

## 4. Out of scope for this arc

- Copper MEDIUMs deferred from before (security audit follow-ups).
- Multi-tenant scoping of the authoring API.
- Any backoffice integration of the editor (the editor is not in the Umbraco backoffice, now or ever).
- Renaming `AuthoredStage.Kind` / `StageKind` enum values, or any further runtime-projection contract changes.
- Action catalog reshaping (the catalog stays as-is; only the route's `actions: AuthoredAction[]` location changes).
- `AuthoredHandoff` (see Open Q 6).
- Storybook deployment / visual regression infrastructure.
- The non-workflow "legacy" hits across OIDC/Codespace code (`PrismComponentTagHelper`, `WorkflowRenderShellResolver`, `appsettings-schema.Umbraco.Cms.json`, `BackchannelRewriteTests`, etc.) — these are unrelated to the workflow domain and stay untouched.

---

**Recommended execution order:** A → B → C, single PRs, green throughout, no slice merged with stale tests. Each slice is a coherent milestone: after A, the tree has no legacy dialect; after B, the editor is integrator-friendly; after C, the model matches the mental model.
