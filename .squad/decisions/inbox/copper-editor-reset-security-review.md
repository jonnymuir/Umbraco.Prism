---
date: 2026-05-30T13:00:00+01:00
agent: copper
area: workflow-editor
branch: squad/82-named-lanes-editor-slice
head: a251bcd (was b03ee38 at task issue)
scope: read-only security review
status: open — findings to triage
---

# Workflow Editor Reset — Security Review (CIA + tenant isolation)

## Threat posture summary

The reset *reduced* attack surface (preview endpoint, conversation pane, mock drafter, IWorkflowPreviewService and SemanticDiff are gone) but *increased* the integrity risk on what remains. The single biggest issue is structural and pre-existing: `/api/workflow-authoring/*` runs **without authentication**, and `/apply` reads the approver identity from the request body (`ApplyWorkflowRequest.Approver`) rather than from `HttpContext.User`. Removing the preview step also removes the one place that semantic-diff inspection could have caught a spoofed approver/agent pairing before the publish hit disk. Schema validators now do more load-bearing work (PROJ140/141/142) and the `LegacyWaitingPayload` sentinel design is property-name-coupled in a way that any future legacy alias will silently bypass.

Top-level CIA:
- **C:** roughly unchanged; response body exposes absolute server paths.
- **I:** **regressed.** Self-asserted authorship + no auth + path traversal in filesystem stores.
- **A:** unchanged; validator cost bounded by `System.Text.Json` default depth (64).

## Findings

### Authoring endpoints (attack surface)

- **CRITICAL — I — auth — `src/UmbracoPrism.WorkflowEditor/Extensions/WorkflowEditorEndpointExtensions.cs:34-44`, `src/UmbracoPrism.MockBusinessApp/Program.cs:139-140` — endpoints are unauthenticated.** `MapPrismWorkflowEditor` adds *no* `.RequireAuthorization()` on the group or any route; the only middleware added is `RequireCors("WorkflowAuthoringDevCors")` in Development, which is `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` (Program.cs:54-56). The non-Dev `/admin` 404 guard at Program.cs:107-118 does **not** match `/api/workflow-authoring`. The inline comment ("no auth required, Development CORS applied") confirms intent.  
  **Exploit:** any browser session — including a third-party origin in Dev or an unauthenticated network attacker in any deployment that follows the reference wiring — can `POST /api/workflow-authoring/workflows/{key}/save` or `/publish` or `/apply` and overwrite any workflow. CSRF in Dev is trivial (no auth + AllowAnyOrigin).  
  **Recommended action:** require an authenticated principal on the group (`group.RequireAuthorization("WorkflowAuthor")`); explicitly include `/api/workflow-authoring` in any non-Dev "admin paths off" middleware; tighten CORS to a named origin.

- **HIGH — CI — tenant isolation — `WorkflowEditorEndpointExtensions.cs:85-107, 151-177, 203-242` — no tenant scoping on workflow keys.** Routes are `/workflows/{key}` with no tenant in the path or in the store contract; `IAuthoredWorkflowStore` is a global singleton. There is no concept of "this workflow belongs to tenant X" — keys are globally writable. The runtime engine *does* per-tenant scope its instance state via `LookupKey(tenantId, userId, workflowKey)` (`WorkflowRuntimeEngine.cs:1173-1174`), but the *definitions* are shared.  
  **Exploit:** in a multi-tenant deployment, any caller (once auth is added) can read or overwrite another tenant's authored definitions.  
  **Recommended action:** scope `IAuthoredWorkflowStore` by tenant (route prefix `/tenants/{tenantId}/workflows/{key}` or claim-derived); document that V1 is single-tenant only if that is the intended posture.

- **HIGH — I — path traversal — `FilesystemAuthoredWorkflowStore.cs:36, 81, 110, 123`; `FilesystemPublishedWorkflowStore.cs:20, 32`; `FilesystemWorkflowAuthoringProvenanceStore.cs:19-20` — `{key}` flows into `Path.Combine` unsanitised.** No `Path.GetInvalidFileNameChars()` check; `Path.Combine(base, "../../etc/passwd.workflow.json")` escapes. MockBusinessApp dodges this only because it pre-registers `InMemoryAuthoredWorkflowStore` / `InMemoryWorkflowAuthoringProvenanceStore` and the `TryAddSingleton` factory in `WorkflowEditorServiceExtensions.cs:26-32` never fires. Downstream consumers that follow the documented `AddPrismWorkflowEditor(path)` pattern get the filesystem store as the default.  
  **Exploit:** `POST /api/workflow-authoring/workflows/..%2F..%2Fseeds%2Fdemo/save` overwrites or reads workflow definitions outside the configured directory (subject to extension).  
  **Recommended action:** validate `workflowKey` against `^[a-zA-Z0-9\-_]+$` at the endpoint layer (and again in the store as defence in depth). Refuse paths that resolve outside `Path.GetFullPath(basePath)`.

- **LOW — C — info disclosure — `WorkflowEditorEndpointExtensions.cs:235-241, 286` — apply/save responses return absolute server paths.** `savedPath` and `provenancePath` are absolute filesystem paths echoed to the client.  
  **Recommended action:** return store-relative tokens or omit; never echo `Path.GetFullPath` results.

- **LOW — A — error handling — `WorkflowEditorEndpointExtensions.cs:249-253` — `ReadBodyAsync` swallows all exceptions and returns `default`.** Indistinguishable from "well-formed empty body". Acceptable today but masks parser-bomb signals and any future JSON exhaustion attacks.

### Schema validation bypass

- **MEDIUM — I — schema validation — `AuthoredStage.cs:146-153`, `AuthoredWorkflowSchemaValidator.cs:49-55` — `HasLegacyWaitingPayload` sentinel is property-name-coupled.** The sentinel fires only when JSON contains a non-null `"waiting"` property. `{ "waiting": null }` slips past (no payload carried, so not exploitable today), but more importantly the design assumes legacy payloads are *only ever* called `"waiting"`. Any future legacy alias or attacker-crafted alternate spelling (e.g. capitalised, snake_case via a custom naming policy) silently bypasses PROJ140.  
  **Exploit (theoretical):** if a future shim ever accepts `"waitConfig"` or `"timeline"`, an authored stage carrying that payload would project to a Question stage with no diagnostic, smuggling waiting semantics back into stages.  
  **Recommended action:** invert the rule. Reject any unknown top-level stage property at the JSON boundary (System.Text.Json `JsonExtensionData` capture, then validator flags non-empty extension data) instead of allow-listing legacy names.

- **MEDIUM — I — patch surface — `WorkflowPatchService.cs:184-197` — `update-transition` doubles as `insert-transition`.** When no matching `(FromStage, ToStage, Action)` tuple is found, the patch service silently appends. There is no `insert-transition` op declared in `ProposalEnvelope.cs:14-21`, so this is the *only* way to add edges via the apply path. Defence in depth means the projector rejects PROJ141/142 violations, but the schema validator is now the only gate.  
  **Recommended action:** require an explicit `insert-transition` op (or refuse the implicit-insert branch); rename the op or add a `requireExisting: true` flag.

- **LOW — I — patch surface — `WorkflowPatchService.cs:208-220` — JSON-pointer `op.Path` segments aren't sanitised before becoming stage keys.** `parts[1]` is treated as a literal stage key. In-memory model so no filesystem concern, but the value is logged at `WorkflowEditorEndpointExtensions.cs:231-233` and the log line includes `envelopeId`, `approver`, and the resolved `savedPath` — attacker-controlled strings land in structured logs.  
  **Recommended action:** clamp path tokens to the canonical key charset before resolution.

- **LOW — A — validator cost — `AuthoredWorkflowSchemaValidator.cs:280-296, 421-522` — parameter validation is recursive over `definition.Properties` and `definition.Items`.** Bounded by `System.Text.Json` default `MaxDepth = 64`, so not currently exploitable. Worth keeping if the default depth is ever increased.

### Provenance / integrity

- **CRITICAL — CI — authorship — `ApplyWorkflowRequest.cs:6-9`, `WorkflowEditorEndpointExtensions.cs:213-233`, `FilesystemWorkflowAuthoringProvenanceStore.cs:27`, `InMemoryWorkflowAuthoringProvenanceStore.cs:13-22` — `approver` is self-asserted in the request body.** The apply endpoint takes `request.Approver` as the canonical "who published this" identity and writes it verbatim into provenance. There is no cross-check against `HttpContext.User`, claims, or any signed token. With the preview-stage agent loop gone, this is now the *only* identity binding on a publish.  
  **Exploit:** any caller passes `{ "approver": "ceo@example.com" }` and the provenance record names that user as the publisher. Combined with finding #1 (no auth), this is authorship laundering at zero cost.  
  **Recommended action:** delete `Approver` from the request DTO; derive from `HttpContext.User.GetEmail()` / `name`. Reject if unauthenticated. Cross-stamp `envelope.Agent` against the calling principal.

- **LOW — I — provenance — `FilesystemWorkflowAuthoringProvenanceStore.cs:19-20` — provenance filenames embed unsanitised `workflowKey`.** Same path-traversal class as the authored store; also limits one provenance record per second per workflow (utcStamp granularity).  
  **Recommended action:** sanitise `workflowKey`; include millisecond + GUID suffix.

### Runtime gateway semantics

- **MEDIUM — I — join arrival forgery — `WorkflowRuntimeEngine.cs:253-256, 974-985` — transition resolution ignores role gates.** `AdvanceAsync` selects `transition.RequiresRole == null` only, which means role-gated transitions never fire from this path *and* arriving cursors are not authenticated against the lane's `RoleGates`/`Actor`. A hostile actor with the ability to call `Advance` on any workflow instance can deposit an arrival at a join gateway, satisfying `arrivedLanes` for a lane they shouldn't own.  
  **Exploit:** in a workflow that joins lanes A and B before releasing to a privileged stage, a caller authorised only for lane A can advance from "A complete" → join, then forge an arrival for lane B by spoofing a cursor on lane B (no per-cursor authorisation check exists in `HandleJoinGatewayAdvance`). Release proceeds.  
  **Note:** likely pre-existing, not introduced by 3a. Calling out because Slice 3a is the first time the join-release semantics are load-bearing.  
  **Recommended action:** at `HandleJoinGatewayAdvance` (and the matching split path), assert the calling principal is a member of `arrivingCursor.LaneKey`'s `RoleGates` / `Actor`; resurrect the role-gated transition lookup so `RequiresRole != null` is honoured.

- **LOW — A — unbounded wait — `WorkflowRuntimeEngine.cs:1015-1035` — no timeout on join arrivals.** A hostile or stuck workflow can sit in `defer` indefinitely (`PollAfterMs` floor of 3000ms; no max wait). Not catastrophic but resource use grows with the number of stuck instances.  
  **Recommended action:** require `WaitingExpectedSeconds` to have a hard ceiling enforced by the schema validator; consider a runtime-side `MaxWaitSeconds` that emits `WORKFLOW_TIMEOUT`.

- **LOW — C — deferred message leakage — `WorkflowRuntimeEngine.cs:1100-1135` — `DeferMessage` is author-controlled text rendered to whoever is polling.** Renders via `PrismComponentRenderPayload.DeferMessage`; the front-end is Lit-templated (no `unsafeHTML` found in workflow-editor), so no XSS, but any author can place arbitrary content in front of any polling user, including users not in the lane that authored the message.  
  **Recommended action:** treat `DeferMessage` as plain text only (current behaviour); ensure the consuming runtime UI does not switch to HTML rendering in future.

### Leftovers from removed features

- **INFO — none material.** `grep -r 'IWorkflowPreview|preview-proposal|ProposalDiff|SemanticDiff|MockDrafter|prism-proposal-diff'` across `src/` returned zero hits. DI graph and endpoint group are clean.  
- **INFO — stale comment — `src/UmbracoPrism.MockBusinessApp/Program.cs:44`.** "AddPrismWorkflowEditor registers the projector, patch service, **preview service**, etc." — the preview service no longer exists. Cosmetic; no DI registration backing it.

### Frontend injection / XSS

- **INFO — no findings.** `prism-step-inspector.ts` and `prism-workflow-outline.ts` render every author-controlled string (display names, descriptions, lane keys, waiting copy, defer messages, validation messages) through Lit `html``` tagged templates with `${…}` interpolation — Lit escapes by default. Grep for `unsafeHTML | innerHTML | insertAdjacentHTML | document.write` across `src/UmbracoPrism.Client/src/workflow-editor/` returns zero hits.  
- **INFO — `condition.expression`** (`prism-step-inspector.ts:689-692`) is bound to an `<input>`'s `.value` — DOM property assignment, not HTML. Safe.

### Confidentiality of in-flight data

- **LOW — C — wire payload — `ProposalEnvelope.cs:44-55`, `WorkflowEditorEndpointExtensions.cs:235-241` — apply response body echoes the full `updated` workflow plus absolute server paths.** The envelope itself carries no secrets (rationale text, op list, agent identity). The response, however, leaks absolute filesystem paths. Browser session storage in the editor host page (none found in `src/UmbracoPrism.Client/src/workflow-editor/`) would inherit any future leak.  
  **Recommended action:** omit `savedPath` / `provenancePath` from the public response or replace with opaque IDs.

## Verification strategy (regression tests to add)

For each MEDIUM-or-higher finding:

| Finding | Test |
|---|---|
| Unauthenticated endpoints | Integration test that hits each `/api/workflow-authoring/*` route with no `Authorization` header and asserts `401`. Add a second test asserting the routes are not exposed in `Environments.Production`. |
| Tenant isolation | Test that a request authenticated as tenant A receives `404` (not `200`) when loading a workflow belonging to tenant B. |
| Path traversal | Integration test posting `key = "..%2Fevil"` to `/save`, `/apply`, and `/publish`, asserting `400` and that no file is created outside the base directory. Repeat for the provenance store. |
| Authorship spoofing | Integration test: authenticated as user "alice", POST `/apply` with `{ approver: "bob" }`, assert the persisted provenance record names "alice" (or the request is rejected). |
| `update-transition` implicit insert | Unit test of `WorkflowPatchService`: `update-transition` with a non-existing tuple → expect explicit error, not silent append. |
| Sentinel coverage | Author-time test that POSTs a stage carrying an unknown stage-level property (e.g. `"waitConfig"`) and asserts PROJ140-equivalent diagnostic fires. |
| Join arrival forgery | Runtime test: principal authorised only for lane A drives a workflow whose join requires lanes {A, B}; assert the join does *not* release. |
| Validator cost ceiling | Author-time test posting a parameter schema with deeply nested `properties` / `items`; assert refusal at a documented depth limit. |

## Top-3 must-fix-before-merge

1. **Add authentication + authorisation on `/api/workflow-authoring/*`.** Without it, every other finding here is reachable from an unauthenticated network position. `group.RequireAuthorization("WorkflowAuthor")` + extend the non-Dev `/admin` 404 middleware to cover `/api/workflow-authoring`.
2. **Derive `approver` from `HttpContext.User`, not the request body.** Delete `ApplyWorkflowRequest.Approver`; stamp from claims. Cross-check `envelope.Agent.Identity` against the calling principal if `Agent.Kind == "human-assisted"`. Restores integrity of the provenance record.
3. **Sanitise `{key}` route params.** Validate against `^[a-zA-Z0-9_-]+$` at the endpoint layer and assert `Path.GetFullPath(combined).StartsWith(Path.GetFullPath(basePath))` inside every filesystem store. Closes the path-traversal hole that survives `TryAddSingleton`-style overrides being skipped by downstream consumers.

---

Filed for Scribe pickup; no code modified.
