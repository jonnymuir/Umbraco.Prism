# Prism Workflow Forms Engine Demo Proposal

**Author:** Tom Nook (Lead)  
**Requested by:** Jonny Muir  
**Status:** Proposal for review (no implementation in this document)  
**Date:** 2026-04-08

---

## 1) Executive summary

This proposal defines a Prism-native demo feature for workflow-driven forms where workflow configuration is the source of truth and each channel/UI is only a renderer of that workflow state.

The core idea is:

- Model each form as a versioned workflow definition (states, transitions, guards, field-groups, tasks).
- Run a lightweight workflow emulator for demo purposes by extending the existing `UmbracoPrism.MockBackOffice` concept.
- Expose a stable runtime API from `UmbracoPrism.Core` that any renderer can consume (mobile shell, web component pages, back office simulator).
- Keep ownership boundaries clear so Prism owns execution semantics and contracts, while channel teams own rendering.

The proposal intentionally generalizes the originating brief into Prism language and removes product-specific naming. It is designed to be implemented incrementally in this repository with testable milestones.

---

## 2) Prism architecture model

### 2.1 Principle

Workflow definition is authoritative. UI never decides process order, eligibility, or completion rules; it requests the next interaction from the workflow runtime.

### 2.2 Logical layers in this repo

1. `UmbracoPrism.Core` (authoritative runtime + contracts)
- Stores workflow definitions and workflow instances.
- Evaluates transitions and guards.
- Validates field-group payloads against schema.
- Emits canonical "render instructions" for channels.

2. `UmbracoPrism.MockBackOffice` (emulator + authoring simulator)
- Hosts demo-only endpoints for workflow definition CRUD/import/export.
- Simulates assignment queues and approval actions.
- Uses the same runtime contracts as Core to avoid demo-only divergence.

3. `UmbracoPrism.Client` (renderer examples)
- Renders page archetypes from runtime payloads.
- Sends field-group submissions and workflow actions.
- Contains Storybook stories for archetypes driven by fixture payloads.

4. `UmbracoPrism.TestSite` (end-to-end demo host)
- Seeds one or more demo workflow templates.
- Demonstrates a complete request-to-approval lifecycle.

### 2.3 Data model (conceptual)

- `WorkflowDefinition`
  - `workflowKey`, `version`, `status` (Draft/Published/Retired)
  - states, transitions, guard references, page archetype mapping
  - field-group references with per-state visibility/editability
- `FieldGroupDefinition`
  - `fieldGroupKey`, `version`, schema, validation rules
- `WorkflowInstance`
  - `instanceId`, `workflowKey`, `workflowVersion`, `currentState`, actor/tenant metadata
- `WorkflowTask`
  - queueable work item for reviewer/approver/ops role
- `WorkflowEvent`
  - append-only audit stream (state changes, submissions, decisions)

### 2.4 Runtime contracts

`UmbracoPrism.Core` should expose a small contract set (names indicative):

- `POST /umbraco/prism/workflows/instances` create instance from definition
- `GET /umbraco/prism/workflows/instances/{id}/render` get current render payload
- `POST /umbraco/prism/workflows/instances/{id}/submit/{fieldGroupKey}` submit group data
- `POST /umbraco/prism/workflows/instances/{id}/actions/{actionKey}` transition action
- `GET /umbraco/prism/workflows/instances/{id}/timeline` audit and status

Render payload shape should include:

- current state metadata
- available actions
- required/optional field groups
- canonical validation descriptors
- UI hints only (not business rules)

### 2.5 Interaction protocol and response-state model

To keep channel renderers simple, all workflow dialog endpoints should return a consistent envelope.

Suggested response envelope (indicative):

```json
{
  "instanceId": "wf_123",
  "responseState": "ask_now",
  "stateVersion": 7,
  "correlationId": "7f8b4f0d-2bbd-470f-a3f2-1544b502b9b1",
  "serverTimeUtc": "2026-04-08T10:30:00Z",
  "pollAfterMs": null,
  "render": {
    "archetype": "Collect",
    "fieldGroups": [],
    "availableActions": []
  },
  "problems": []
}
```

Response states:

1. `ask_now`
- Meaning: backend has one or more items/questions to render immediately.
- Client behavior: render `render` payload now and allow submit/action.

2. `wait`
- Meaning: instance is valid but temporarily not ready for next question (async guard, queue, external check, or reviewer decision pending).
- Client behavior: show waiting state and poll after `pollAfterMs`.

3. `complete`
- Meaning: workflow reached terminal outcome.
- Client behavior: render completion payload and stop poll loop.

4. `error`
- Meaning: non-happy-path result with typed failure details in `problems`.
- Client behavior: branch by problem type (validation/auth/conflict/system).

### 2.6 HTTP status guidance for workflow dialog

Use transport status for protocol category and `responseState` for workflow meaning.

| Scenario | HTTP status | `responseState` | Notes |
|---|---:|---|---|
| More UI items/questions to ask now | `200 OK` | `ask_now` | Includes render payload and available actions. |
| Backend not ready yet, ask later | `202 Accepted` | `wait` | Include `pollAfterMs`; may also include `Retry-After` header. |
| Complete | `200 OK` | `complete` | Include final outcome and summary metadata. |
| Validation failure | `422 Unprocessable Entity` | `error` | Populate `problems` with field-group and field-level issues. |
| Authentication missing/invalid | `401 Unauthorized` | `error` | Do not leak instance details. |
| Authorization denied | `403 Forbidden` | `error` | Actor authenticated but not allowed for this transition/state. |
| Optimistic concurrency or state mismatch | `409 Conflict` | `error` | Include expected vs actual `stateVersion` when safe. |
| Instance not found/hidden | `404 Not Found` | `error` | Prefer 404 over 403 when existence should be concealed. |
| Transient infrastructure fault | `503 Service Unavailable` | `error` | Include retry guidance for client policy. |
| Unhandled server failure | `500 Internal Server Error` | `error` | Include correlation ID only; keep internals in logs. |

Guidance:

- Do not use `204` for dialog steps; always return the envelope.
- Reserve `202` for explicit wait/poll behavior; avoid long request holds.
- Keep problem format stable across all workflow endpoints.

---

## 3) Reusable interaction/page archetypes

### 3.1 Umbraco v17-appropriate UI option sets

For Umbraco v17 contexts (including backoffice extensions), use one of these renderer options:

1. Option A: Umbraco UI Library-first (UUI components)
- Shape: map archetype payloads directly to UUI controls and backoffice extension patterns.
- Pros: native v17 look/feel, accessibility baseline, consistent editor experience.
- Cons: tighter coupling to backoffice UI stack; weaker portability to non-Umbraco channels.

2. Option B: Prism web-component-first
- Shape: Prism-owned components render archetypes, with thin adapters for channel shells.
- Pros: strongest cross-channel reuse, stable contract-driven rendering, lower divergence across hosts.
- Cons: requires extra styling work to feel fully native inside Umbraco backoffice.

3. Option C: Hybrid adapter model
- Shape: keep archetype renderer contract Prism-generic; use adapter layer to map to UUI in backoffice and Prism/native components elsewhere.
- Pros: balances Umbraco v17 fidelity with cross-channel consistency.
- Cons: introduces adapter maintenance and mapping test overhead.

Recommended default: Option C (Hybrid adapter model).

Rationale: this demo needs Prism-generic runtime behavior while still feeling correct in v17 backoffice experiences.

Archetypes are renderer primitives that map to workflow state intent, not specific business domains.

1. `Collect`
- Purpose: gather user input.
- Typical components: form sections, validation summary, save-draft.

2. `Review`
- Purpose: read-only confirmation before a transition.
- Typical components: grouped answers, change links, submit action.

3. `TaskQueue`
- Purpose: present pending workflow tasks for operators.
- Typical components: filters, SLA badges, assignment status.

4. `Decision`
- Purpose: approve/reject/request-changes with reason capture.
- Typical components: decision buttons, decision rationale, policy hints.

5. `RequestChanges`
- Purpose: route instance back with targeted remediation.
- Typical components: required correction items, due date, notes.

6. `StatusTimeline`
- Purpose: visualize instance progress and audit events.
- Typical components: state timeline, actor history, timestamps.

7. `Completion`
- Purpose: final outcome with next-step guidance.
- Typical components: receipt, downloadable summary, follow-up actions.

Renderer mapping example:

- State `Draft` -> `Collect`
- State `Submitted` -> `StatusTimeline`
- State `UnderReview` -> `TaskQueue` + `Decision`
- State `NeedsChanges` -> `RequestChanges`
- State `Approved`/`Rejected` -> `Completion`

### 3.2 Client-server dialog loop contract

All renderer clients should use the same loop for create/render/submit/action:

1. Call runtime endpoint.
2. Read HTTP status and parse envelope.
3. Branch by `responseState`:
- `ask_now`: render and await user action.
- `wait`: show pending UI and schedule poll using `pollAfterMs`.
- `complete`: show terminal view and stop polling.
- `error`: route to typed error handling.
4. Include `stateVersion` on mutating requests to enforce optimistic concurrency.
5. Attach and log `correlationId` end-to-end for support diagnostics.

---

## 3.3 Helper utility design (server and client)

### Server utilities (Core)

- `WorkflowResponseFactory`
  - Creates canonical envelope for `ask_now`, `wait`, `complete`, and `error`.
  - Ensures consistent headers (`Retry-After` where relevant) and `correlationId` propagation.

- `WorkflowProblemFactory`
  - Builds typed problems for validation/auth/conflict/system categories.
  - Keeps error payloads stable across endpoints.

- `WorkflowHttpResultMapper`
  - Maps domain outcome -> HTTP status + `responseState`.
  - Centralizes status rules so controllers stay thin.

- `WorkflowConcurrencyGuard`
  - Validates submitted `stateVersion`/ETag and produces conflict outcomes.

### Client utilities (Client)

- `workflowApiClient`
  - Typed methods for create/render/submit/action/poll that always return parsed envelope.

- `workflowDialogOrchestrator`
  - Single state machine for `idle -> asking -> waiting -> complete -> error`.
  - Handles timers, retry policy, and cancel/dispose behavior.

- `workflowErrorMapper`
  - Converts HTTP/problem payloads into user-safe messages and telemetry events.

- `workflowTraceContext`
  - Propagates `correlationId` through logs and UI diagnostics panel.

Design rule: channel components should not interpret raw HTTP responses directly; they consume orchestrator state only.

### 3.4 Example dialog sequence (including wait/poll)

1. Client: `POST /instances` -> `200` + `ask_now` + `Collect` payload.
2. User submits first field group.
3. Client: `POST /submit/personal-details` with `stateVersion=3`.
4. Server: `202` + `wait` + `pollAfterMs=2000` (async verification running).
5. Client shows pending message and polls `GET /render` after 2s.
6. Poll #1: `202` + `wait` + `pollAfterMs=3000`.
7. Poll #2: `200` + `ask_now` + next `Review` payload.
8. User confirms and submits decision action.
9. Server: `200` + `complete` + outcome summary.
10. Client renders `Completion` and exits loop.

---

## 4) Field-group model and versioning strategy

### 4.1 Field-group model

Field groups are reusable, versioned schema blocks that can be mounted in multiple workflows.

- Group identity: `fieldGroupKey` + `version`
- Group schema: fields, types, constraints, conditional visibility rules
- Group policy: editability by state/role, requiredness by transition
- Group projection: renderer hints (layout density, order, labels) without enforcing UI framework details

Recommended storage split:

- `FieldGroupDefinition` (immutable published versions)
- `FieldGroupBinding` (workflow-state mapping to group/version)
- `FieldGroupSubmission` (instance-level values with submitted-by and submitted-at)

### 4.2 Versioning strategy

1. Immutable published versions
- Once published, a definition version never changes.

2. Draft-edit-publish lifecycle
- New edits create next draft version.
- Publish promotes draft to immutable runtime version.

3. Workflow-version pinning
- A workflow version pins exact field-group versions.
- Running instances continue on pinned versions.

4. Controlled migration path
- Optional migration scripts map old submissions to new version schema.
- Migration is explicit and auditable, never implicit.

5. Compatibility policy
- Patch version: non-breaking metadata/label changes.
- Minor version: additive fields with safe defaults.
- Major version: breaking schema or rule changes requiring migration.

---

## 5) Mock back-office workflow emulator integration plan

### 5.1 Goal

Reuse `UmbracoPrism.MockBackOffice` as a deterministic workflow authoring and operator simulation host, while keeping Prism Core runtime authoritative.

### 5.2 Integration shape

1. Extend MockBackOffice API surface
- Add emulator endpoints under `/api/backoffice/workflows/*` for:
  - definition import/export
  - publish/retire workflow versions
  - queue/assignment simulation
  - operator decisions and comments

2. Share contracts with Core
- Introduce shared DTO package/namespace under `UmbracoPrism.Core` models.
- MockBackOffice consumes these DTOs directly to avoid drift.

3. Execution mode toggles
- `RuntimeMode = Emulator` in MockBackOffice for deterministic local demos.
- `RuntimeMode = Core` route-through mode to exercise actual Core runtime endpoints.

4. Seeded demo packs
- Add JSON fixtures for one baseline workflow and one variant.
- Load via `appsettings` or startup seeding in MockBackOffice.

5. Observability
- Record workflow events in structured logs.
- Include per-instance correlation IDs to trace render/submit/transition flow.

### 5.3 Ownership and governance boundaries

1. Prism Core ownership
- Workflow runtime semantics, transition rules, validation contracts, persistence schema.

2. MockBackOffice ownership
- Demo authoring UX, operator simulation, seeded content, emulator-only conveniences.

3. Client ownership
- Archetype rendering, channel-specific interaction details, accessibility implementation.

4. Governance rules
- No channel-specific branching in Core transitions.
- Any runtime contract change requires version bump and compatibility note.
- Emulator-only extensions must be namespaced and not leak into production runtime contracts.
- Security-sensitive guards must execute in Core even if initiated from emulator UI.

---

## 6) Implementation phases

### Phase 0: Discovery and contract freeze (short)

- Confirm archetype set and canonical render payload shape.
- Define first demo workflow (generic request -> review -> outcome).
- Produce contract examples in `docs/design`.

Deliverables:
- Approved contract doc and fixture payloads.

### Phase 1: Core runtime skeleton

- Add workflow definition/instance models in `UmbracoPrism.Core`.
- Implement minimal state machine execution and audit event append.
- Add runtime endpoints for create/render/submit/action/timeline.

Deliverables:
- Passing unit tests in `UmbracoPrism.Core.Tests` for transitions and validation.

### Phase 2: Field-group engine and versioning

- Implement field-group definition storage and binding to states.
- Add submission validation pipeline and version pinning.
- Add migration stubs and compatibility metadata.

Deliverables:
- Tests for version pinning, additive changes, and breaking-change handling.

### Phase 3: MockBackOffice emulator

- Add workflow authoring and queue simulation endpoints to `UmbracoPrism.MockBackOffice`.
- Add deterministic seeded workflows and operator personas in config.
- Add route-through mode to Core runtime for fidelity testing.

Deliverables:
- Emulator scriptable demo flow using `.http` requests.

### Phase 4: Client archetype renderer

- Implement reusable archetype components in `UmbracoPrism.Client`.
- Add Storybook stories using render payload fixtures.
- Wire submit/action calls to runtime endpoints.

Deliverables:
- Demo journey running in browser with clear state transitions.

### Phase 5: TestSite demo scenario

- Seed end-to-end demo workflow into `UmbracoPrism.TestSite`.
- Add walkthrough docs and screenshots.
- Add smoke tests (API + UI happy path).

Deliverables:
- Repeatable repo demo script for contributors.

---

## 7) Risks/open questions

### Risks

1. Contract drift between emulator and Core runtime
- Mitigation: shared DTOs and contract tests run against both modes.

2. Over-scoping into a full BPM product
- Mitigation: strict non-goals and demo-first state machine scope.

3. Version migration complexity
- Mitigation: pin-by-default strategy and explicit migration tooling only when needed.

4. Security bypass in emulator pathways
- Mitigation: enforce authorization/guard checks in Core runtime regardless of caller.

5. Renderer coupling to workflow internals
- Mitigation: renderer consumes only render payload contract, never raw transition graph.

### Non-goals

1. Building a production-grade low-code designer.
2. Supporting arbitrary executable scripts inside workflow definitions.
3. Implementing cross-tenant shared workflow execution in v1 demo.
4. Replacing existing Prism auth/tenant controls.
5. Shipping external integration connectors in the first demo.

### Open questions

1. Should workflow and field-group definitions be persisted in Umbraco content, dedicated tables, or hybrid storage for the demo?
2. Which actor model should be canonical for approvals in v1: role-based only, user assignment, or both?
3. Do we need optimistic concurrency tokens on all submit/action endpoints from day one?
4. Should timeline/audit be eventually consistent or strictly transactional with state transitions?
5. What minimum accessibility acceptance criteria should every archetype meet before demo sign-off?
