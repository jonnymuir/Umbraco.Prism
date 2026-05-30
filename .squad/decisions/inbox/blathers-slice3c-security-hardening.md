---
date: 2026-05-30T13:30:00+01:00
agent: blathers
area: workflow-editor
branch: squad/82-named-lanes-editor-slice
parent: copper-editor-reset-security-review.md
status: shipped — three CRITICAL/HIGH findings closed
---

# Slice 3c — Security hardening of `/api/workflow-authoring/*`

Closes Copper's must-fix-before-merge items (#1, #2, #3) from the editor-reset security
review. Multi-tenant scoping (#2 HIGH), `WorkflowPatchService` covert-insert (MEDIUM),
and `WorkflowRuntimeEngine` join-arrival forgery (MEDIUM) are explicitly out of scope
and deferred to follow-up slices.

## What changed (server-side, integrator-facing)

### 1. Authentication required on every authoring route

- `WorkflowEditorEndpointExtensions.MapPrismWorkflowEditor` now calls
  `.RequireAuthorization(WorkflowAuthoringPolicies.WorkflowAuthor)` on the
  `/api/workflow-authoring` group.
- A new constant `WorkflowAuthoringPolicies.WorkflowAuthor = "WorkflowAuthor"`
  is exported from `UmbracoPrism.WorkflowEditor.Extensions` and **hosts must
  register a policy by that name in DI**, otherwise every authoring request
  returns 500 at startup. The MockBusinessApp wires it as
  `policy => policy.RequireAuthenticatedUser()`; downstream apps tighten by
  replacing that policy with their own claim/role gates.
- The non-Development `/admin` 404 middleware in MockBusinessApp now also covers
  `/api/workflow-authoring` — defence-in-depth so the reference app's authoring
  surface is unreachable outside dev even if the policy somehow becomes
  permissive.
- The development CORS policy `WorkflowAuthoringDevCors` is tightened from
  `AllowAnyOrigin` to a named-origin list defaulting to
  `http://localhost:5173,http://127.0.0.1:5173` (overridable via
  `PrismBusinessApp:WorkflowAuthoringDevOrigins`).

### 2. Approver bound to the authenticated principal (BREAKING)

- **`ApplyWorkflowRequest.Approver` is deleted.** The DTO now contains only
  `Envelope`. Any caller still sending `{ envelope, approver }` will have the
  body's `approver` silently ignored — System.Text.Json drops unknown
  properties — and the persisted provenance will name the calling principal.
- The `/apply` handler now resolves the approver from `HttpContext.User` via
  the same claim ordering as `PrismIdentityExtensions.GetEmail`:
  `preferred_username → email → name → Identity.Name`. If no usable claim is
  present the handler returns 401 (this only fires if a custom policy admits
  an anonymous principal — `RequireAuthenticatedUser` already rejects upstream).
- When `envelope.Agent.Kind == "human-assisted"`, the handler cross-stamps
  `envelope.Agent.Identity` against the resolved approver and returns 400 on
  mismatch — closing the authorship-laundering path Copper called out. Agent
  kinds `github-copilot` / `custom-agent` name the agent rather than the human
  and are deliberately not cross-checked.

### 3. Workflow keys validated, filesystem stores enforce containment

- The `/save`, `/publish`, and `/apply` handlers validate the route `{key}`
  against `^[a-zA-Z0-9_-]+$` and return 400 on rejection. `..%2Fevil`,
  `foo/bar`, `foo.bar`, etc. never reach the store.
- `FilesystemAuthoredWorkflowStore`, `FilesystemPublishedWorkflowStore`, and
  `FilesystemWorkflowAuthoringProvenanceStore` each gained a private
  `ResolveSafePath` helper that asserts
  `Path.GetFullPath(combined).StartsWith(Path.GetFullPath(basePath))` and
  throws `InvalidOperationException` on violation. This is defence-in-depth:
  the endpoint sanitiser already rejects, but downstream consumers that
  bypass `TryAddSingleton` and inject a key from a different source now still
  get containment for free.

## Regression test surface (net new)

| File | Tests |
|---|---|
| `Workflow/Authoring/WorkflowAuthoringEndpointSecurityTests.cs` (new) | 13 tests covering unauthenticated → 401 (theory ×3), endpoint-layer path traversal on `/save` (theory ×5) + `/apply` + `/publish`, store-layer path traversal on all three filesystem stores, approver-from-claims (body `approver: bob` ignored, persisted approver = caller `alice`), and human-assisted agent identity mismatch → 400. |
| `Workflow/Authoring/AuthoredWorkflowValidationTests.cs` | +1 test: `Project_StageWithBareWaitingPayloadOnly_ReportsProj140` — pins Tangy's bare-sentinel branch (waiting payload on a `Question`-typed stage, no retired `LegacyKindRaw`). |
| `Workflow/Authoring/AuthoredWorkflowSerializationTests.cs` | +1 test: `AuthoredTransition_LegacyShimRoundTrip_FromStageToStageAction_ReadBackViaSourceTargetTrigger` — pins the obsolete-shim properties for as long as they remain. |

The previous `PostApply_WithMissingApprover_ReturnsBadRequest` test was deleted
(approver no longer comes from the body, so the case is no longer meaningful;
unauthenticated callers now hit the broader 401 case).

## Test infrastructure changes

- `WorkflowAuthoringWebFactory` and `FourWorkflowReferenceContractTests.ReferenceWorkflowContractWebFactory`
  install a header-driven `Test` authentication scheme (`X-Test-User`) as the
  default authenticate/challenge scheme. Tests that omit the header land on
  the policy challenge and receive 401, which is exactly the unauthenticated
  case the new security tests need to assert.
- Both auth-touching test classes share a single `WorkflowAuthoringFactoryCollection`
  so they run serially through one factory instance, avoiding
  `IOException: file in use` races on `Fixtures/planning.workflow.json` when
  `WithWebHostBuilder` re-invokes `ConfigureWebHost`.
- `ResetAuthoredFixturesDirectory` now skips File.Copy when the target already
  exists (csproj `<Content Include>` mirrors the source on build), eliminating
  the reset-vs-read race observed when multiple authoring test classes start
  near-simultaneously. Per-process `EnsureFixturesInitialised` / `EnsureCleanPublishedDirectory` /
  `EnsureCleanProvenanceDirectory` gates ensure the dir-reset side-effects fire
  at most once per process.

## Breaking changes — read this

1. **`ApplyWorkflowRequest.Approver` removed.** Downstream callers — agents,
   scripts, the editor UI — must stop sending `approver` in the request body.
   No silent migration: it is simply ignored (no error), and the persisted
   provenance will name the authenticated caller.
2. **`/api/workflow-authoring/*` is now authenticated.** Hosts that wire
   `MapPrismWorkflowEditor()` must register a `"WorkflowAuthor"` policy in DI
   *before* `MapPrismWorkflowEditor()`, or the app will fail at startup with
   `InvalidOperationException: The AuthorizationPolicy named: 'WorkflowAuthor' was not found.`
3. **Dev CORS is now origin-restricted.** Editor host pages on a port other
   than 5173 must override `PrismBusinessApp:WorkflowAuthoringDevOrigins` in
   configuration. `AllowAnyOrigin` is gone.

## Dashboard iframe interaction — known follow-up for Isabelle/Brewster

The TestSite Umbraco dashboard mounts the editor as an iframe pointing at the
BusinessApp origin (`https://localhost:7245/workflow-editor`). The editor JS
inside the iframe then fetches `/api/workflow-authoring/*` on the BusinessApp
origin. Before Slice 3c those calls were anonymous and worked from any context.

**After Slice 3c, those fetches require an authenticated principal on the
BusinessApp origin.** Since the user is authenticated to Umbraco/TestSite
rather than directly to BusinessApp, the iframe inherits no auth context and
the requests will return 401.

This is integrator-facing and beyond a backend slice's reach. Options
(deferred — not in this slice):

- **Short-term:** the editor host page (`workflow-editor.html`) acquires a
  Bearer token from the embedding Umbraco session and attaches it to every
  fetch (e.g. via a postMessage handshake or a signed cookie issued by
  TestSite that BusinessApp accepts via its JWT bearer events).
- **Medium-term:** adopt Brewster's recommendation
  (`brewster-editor-reset-umbraco-dx-review.md`, SHOULD-FIX #1) — render
  `<prism-workflow-editor>` directly inside the Umbraco dashboard as a web
  component, so the API calls are same-origin to Umbraco and inherit the
  member cookie.

I am flagging this for Squad to route; this slice intentionally trades the
dashboard's anonymous-fetch convenience for correctness on the integrity axis.

## Explicitly deferred (NOT in this slice)

- **Multi-tenant scoping** (Copper HIGH #2). V1 is single-tenant; the
  `IAuthoredWorkflowStore` contract has no tenant axis. Documented here.
- **`WorkflowPatchService` covert insert** (`update-transition` doubling as
  `insert-transition`, Copper MEDIUM). Separate slice.
- **`WorkflowRuntimeEngine` join-arrival forgery** (Copper MEDIUM). Pre-existing
  before the editor reset; separate slice.
- **Endpoint info disclosure** — `savedPath` / `provenancePath` still echo
  absolute server paths (Copper LOW). Acceptable for V1 dev; revisit when
  hardening for prod hosting.
- **`/save` vs `/publish` vs `/apply` consolidation** — Tom Nook's worth-noting,
  separate slice.

## Quality gate

- `dotnet build UmbracoPrism.sln` — 0 warnings, 0 errors.
- `dotnet test UmbracoPrism.sln -c Release` — **862 passed**, 0 failed
  (was 845 baseline; net +17: 16 new behavioural tests + 1 removed
  body-approver test + 2 Tangy regression tests).
- Both `dotnet test` invocations re-run to confirm green-on-repeat — the
  fixture-race flake is gone.
