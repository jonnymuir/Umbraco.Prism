## 2026-06-01: Queue access stays in the host, not in the shared runtime

**Author:** Blathers

- Shared workflow definitions can name the queue that owns a lane.
- The shared runtime now accepts a queue access profile from the host to decide which queues can be started, viewed, and moved on.
- MockBusinessApp uses that profile to show business-user queue work on the admin page and move items on without teaching the shared runtime about business users.
- TestSite-style web flows keep their own queue profile, so the same runtime can support different host rules without hard-coded web or business assumptions.

---

## Payment Demo Editor Inspection — Findings & Decisions

**Date:** 2026-06-01  
**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Context:** Post-payment-flow-slice editor inspection, triggered by screenshot showing "Validation 2" badge and all stages landing in the Public lane.

---

### What the 2 validation errors actually are

Browser inspection of the **Gateway Representation** story (LEAVE_REQUEST_STARTER_WORKFLOW — a workflow with the same Split/Join structure) confirmed:

1. **Error:** `Stage "Decision confirmed" is unreachable from the workflow start. Add or retarget a route through a gateway so authors can get there.`  
   (In the payment demo this reads: `Stage "Payment Complete" is unreachable…`)

2. **Error:** `Route "continue" points to a missing source stage "". Reconnect it to an existing stage before you save or simulate this workflow.`  
   (In the payment demo this reads: `Route "release" points to a missing source stage ""…`)

**These are false positives.** Both errors are triggered by the same root cause.

---

### Root cause: `flattenRoutes()` doesn't understand Join gateways

`workflow-routes.ts` → `flattenRoutes()`:

```ts
const source = gateway.source ?? '';
```

Join gateways intentionally have no `source` (they receive from multiple upstream branches — their source is implicit in the incoming routes, not a single stage). The `?? ''` fallback creates phantom routes with `fromStage: ''`.

This causes:
- `workflowRoutesWithMissingStages` to flag the route (error 2 above)
- `workflowReachableStageKeys` to never traverse the `''` key, so the stage downstream of the Join appears unreachable (error 1 above)

**This is a pre-existing, systemic bug** — it affects every workflow that uses a Join gateway. It was present before the payment slice; the payment slice just made it visible for the first time in the app.

---

### Root cause: `normaliseStage()` reads `actor` but C# serialises `laneKey`

`workflow-wire-format.ts` → `normaliseStage()`:

```ts
actor: typeof raw.actor === 'string' ? raw.actor : undefined,
```

The C# `AuthoredStage` has a `LaneKey` property, which serialises to `"laneKey"` in JSON. But `normaliseStage` reads `raw.actor`, which is always undefined for server-loaded stages. So every stage lands in the `'public'` lane via `stageLaneKey()`'s fallback.

Gateways are NOT affected — `normaliseGateway` correctly reads `raw.laneKey`.

**Effect on canvas:** All stages appear in the Public lane band, even though their gateway nodes appear in the correct lane. The canvas tells the wrong story and is visually misleading.

---

### The workflow IS structurally correct

The payment-demo definition itself (committed in `8619e90`) is correct:
- Split gateway `submit-payment` fans out from `enter-details` to both branches
- Split gateway `payment-confirmed` in the Payments lane collects the payments-side acknowledgement
- Join gateway `await-payment-confirmation` has `RequiredIncomingLanes: ["applicant", "payments"]` and routes to `payment-complete`

The graph logic, the route topology, and the gateway types are all right. The bugs are in the editor's normalisation and validation layers, not the definition.

---

### Decisions

#### Decision 1: Fix `flattenRoutes` to skip Join gateway `source` resolution

Join gateways should NOT contribute a `fromStage` entry via their own `source` field. Their routes already express the join semantics via the incoming routes targeting their `gatewayKey`.

**Proposed fix in `workflow-routes.ts`:** Guard `flattenRoutes` to skip adding a `fromStage` for routes emitted by a Join gateway, or to substitute the gateway's own key as the logical source for reachability purposes.

#### Decision 2: Fix `normaliseStage` to read `laneKey` as well as `actor`

The wire format sent by the C# API uses `laneKey` on stages. `normaliseStage` must accept both field names to support both legacy (`actor`) and current (`laneKey`) payloads.

**Proposed fix in `workflow-wire-format.ts`:**
```ts
actor: typeof raw.actor === 'string' ? raw.actor
     : typeof raw.laneKey === 'string' ? raw.laneKey
     : undefined,
```

#### Decision 3: Next slice is "Fix Join gateway handling in editor" (two sub-tasks)

- **Sub-task A (tiny, safe):** `normaliseStage` laneKey fallback — one-line change, no risk of regression
- **Sub-task B (bounded):** Fix validation to correctly handle Join gateways — extends `flattenRoutes` + reachability algorithm

These are targeted corrections to existing logic, not a broad redesign. Both bugs should be fixed together as they make the payment demo (and leave request) unusable in the editor.

#### Decision 4: The Shell story's payment demo does not reflect the real split/join flow

`prism-workflow-editor-shell.stories.ts` uses `buildWorkflow()` for the payment demo, producing a simple linear flow. This is misleading. The story should be updated (in a separate slice or alongside the above fixes) to load the actual payment-demo fixture from `fixtures/index.ts` so the real canvas is testable in isolation.

---

### Recommended next slice title

**"Fix Join gateway normalisation and validation false-positives"**

Scope:
1. `workflow-wire-format.ts`: `normaliseStage` reads `laneKey` as fallback for `actor`
2. `workflow-routes.ts`: `flattenRoutes` skips empty-source Join gateways correctly
3. `workflow-validation.ts`: Ensure `workflowReachableStageKeys` traverses through Join gateway keys
4. Optional: Update shell story payment demo to use the real fixture

Confidence: 🟢 Frontend only, well-bounded, no API changes needed.

---

## 2026-06-01T23:34:47+01:00: User directive

**By:** Jonny (via Copilot)

**What:** Treat each workflow lane as a queue with a queueName. Host apps, not the workflow runtime or editor, decide who can start or act in each queue. For the reference apps, the TestSite web user queue can start workflows and act in its queue, while the MockBusinessApp business user queue can only move workflow instances on from the admin page for now. The editor must take the available queues from its host interface instead of hard-coding them.

**Why:** Jonny wants the queue model cleanly divided so developers can wire their own applications against the workflow runtime and editor without web/business assumptions being baked into shared components.

---

# Flattened Workflow Runtime — Direct Definition Loading

**Date:** 2026-06-04T22:31:07.531+01:00  
**By:** Blathers (Backend Dev)  
**For:** Isabelle and Tangy

---

## Decision

The backend/runtime now treats `WorkflowDefinitionFile` as the persisted source of truth and reads it directly from the reference seed JSON. Queues, gateways, routes, and stage metadata should be expressed on the top-level persisted contract; the old nested `metadata` shape remains read-compatible only as a fallback while Isabelle finishes the editor side.

## Why

- Jonny's directive was to remove the split authored-vs-runtime contract and avoid extra lookup structures.
- The runtime no longer needs projector-built reference definitions or the cached `_instanceLookup` map.
- Keeping legacy `metadata` as read compatibility lets the backend simplify immediately without forcing a flag day across every producer in one commit.

## Backend implications

1. Reference workflows now load from `workflow-seeds/*.json`, not hard-coded authored C# plus projection.
2. Runtime instance reuse is resolved by scanning persisted in-memory instances by workflow/user/tenant rather than maintaining a separate cache.
3. Gateway execution can run from first-class `gateways[].routes[]` data, with flat `transitions[]` retained only for compatibility/workflows that have not been rewritten yet.

## Testing implications

- Backend behavioural proof should assert the persisted payment demo contract directly.
- Gateway/payment regression tests should prove the runtime still handles split + join waiting from the flattened definition.

---

# Payment Demo JSON Transformation Analysis

**Date:** 2026-06-04  
**By:** Blathers (Backend Dev)  
**For:** Jonny — Understanding why seed JSON and editor JSON differ

---

## The Two Shapes

### 1. Seed JSON (`payment-demo.json` — the runtime definition)
This is the **WorkflowDefinitionFile** shape after projection by C#'s `WorkflowProjector`.
- Uses `states` (list of `StepDefinition`)
- Uses `transitions` (list of `WorkflowTransitionFile`)
- Puts metadata **inside** the states: `state.metadata.stageType`, `state.metadata.actor`, `state.metadata.laneKey`
- Puts lanes and gateways **into** `metadata.lanes` and `metadata.gateways` at the workflow level
- Gateways are preserved in metadata so the runtime can understand them

### 2. Editor JSON (`payment-demo.workflow.json` — the authored definition)
This is the **AuthoredWorkflow** shape that the editor UI works with.
- Uses `stages` (list of `AuthoredStage`)
- Uses `gateways` (list of `AuthoredGateway`) — gateways own all routing
- Uses `lanes` at the top level (list of `AuthoredLane`)
- No `transitions` array — routing is entirely within gateways
- `stageKey`, `displayName`, `kind`, `laneKey`, `actor` are all top-level properties on stages

---

## Why They're Different: The Transformation Path

### The Projection Pipeline (C# WorkflowProjector)

**Source:** `ReferenceWorkflowRepository` creates `AuthoredWorkflow`  
↓  
**Projector:** `WorkflowProjector.Project(AuthoredWorkflow)` → `WorkflowDefinitionFile`  
↓  
**Output:** Runtime seed file (`payment-demo.json`)

#### Key transformations in WorkflowProjector:

1. **`stages` → `states`** and **`stageKey` → `stateKey`**
   - **Why?** Runtime uses `StateKey` and `States` terminology; authored workflows use `StageKey` and `Stages`
   - This is **normalisation** — naming distinction between authoring and runtime contract

2. **`kind` (enum) → `metadata.stageType` (string)**
   - Example: `StageKind.Question` becomes `"Question"` in `state.metadata.stageType`
   - **Why?** Metadata is optional JSON that preserves authoring intent without affecting the core runtime contract
   - Runtime doesn't care about stageType; it's there for introspection and diagnostics

3. **`stage.actor` / `stage.laneKey` → `state.metadata.actor` / `state.metadata.laneKey`**
   - **Why?** Actor and lane assignment is authoring metadata, not part of the core step definition
   - The runtime doesn't execute different logic based on actor; it's informational

4. **`gateways` → `metadata.gateways`**
   - Gateways are preserved in metadata because the runtime needs them for workflow simulation (UI projection)
   - **Why?** The core runtime contract is just `transitions`; gateways are an authoring convenience

5. **`gateway.routes` → `transitions` array**
   - Each gateway + route pair emits one or more transitions
   - **Example from payment demo:**
     - Gateway `submit-payment` has routes to `await-payment-confirmation` and `confirm-payment-received`
     - Both routes have the same trigger (`submit`), so this is a **parallel fork**
     - Projector emits:
       1. `enter-details → submit-payment [submit]` (entry into the gateway)
       2. `submit-payment → await-payment-confirmation [split-auto]`
       3. `submit-payment → confirm-payment-received [split-auto]`

6. **`lanes` → `metadata.lanes`**
   - Lane definitions (with queueName, roleGates) are preserved in metadata
   - **Why?** Lanes are an authoring construct; the runtime doesn't enforce lane-based routing

#### Special case: The extra `payment-confirmed` gateway

The editor JSON shows a gateway called `payment-confirmed` that appears to have a Split kind with one route to `await-payment-confirmation`. This is authored in `ReferenceWorkflowRepository.cs` at line 472:

```csharp
new AuthoredGateway
{
    GatewayKey = "payment-confirmed",
    DisplayName = "Payment confirmed",
    Kind = GatewayKind.Split,
    LaneKey = "payments",
    Source = "confirm-payment-received",
    Routes = [
        new AuthoredRoute {
            Id = "confirm-payment-received--confirm--await-payment-confirmation",
            Target = "await-payment-confirmation",
            Trigger = "confirm",
            RequiresRole = "reviewer"
        }
    ]
}
```

**Why does this exist?**
- The applicant lane needs routes from the payments lane
- A split gateway from `confirm-payment-received` ensures the confirmation step can target the join properly
- This is an **authoring pattern**: split gateways can have a single route (exclusive choice), which flattens to a direct transition in the runtime
- **In the seed JSON:** this becomes `confirm-payment-received → await-payment-confirmation [confirm]` with `requiresRole: "reviewer"`

---

## Explaining Each Transformation Point

| Seed JSON (Runtime) | Editor JSON (Authored) | Classification | Why |
|---|---|---|---|
| `states` | `stages` | Normalisation | Naming convention: runtime vs authoring terminology |
| `stateKey` | `stageKey` | Normalisation | Same distinction |
| (no kind property) | `kind: "Question"` | Harmless normalisation | Kind is in metadata.stageType in seed; promoted to top-level in authored |
| `metadata.stageType` | `kind` | Normalisation | Reverse of above |
| `metadata.actor`, `metadata.laneKey` | `stage.actor`, `stage.laneKey` | Harmless normalisation | Actor and lane moved from metadata to top-level in authored |
| (no top-level gateways) | `gateways: [...]` | Schema redesign | Authored model makes gateways explicit; runtime buries them in metadata |
| `transitions: [...]` | (no transitions) | Schema redesign | Routes are embedded in gateways in authored model; flattened to transitions in runtime |
| `metadata.lanes` | `lanes: [...]` | Harmless normalisation | Lanes moved from metadata to top-level |
| (no roleGates on lane) | `lane.roleGates: []` | Accidental complexity | See below |

---

## The `roleGates` Mystery

**In the editor JSON:**
- Every `lane` has an empty `roleGates: []`
- Every `stage` has an empty `roleGates: []`
- Every `gateway` has an empty `roleGates: []`

**What are roleGates?**
- They're meant to restrict **who can enter a stage or lane** based on role (e.g., only "reviewer" can enter this stage)
- In the seed JSON: `metadata.lanes[x].roleGates: null` (serialised as absent because empty)

**Why are they always empty in payment-demo?**
- The payment demo doesn't use role-based stage access restrictions
- Restrictions are expressed via `requiresRole` on **transitions** (e.g., only reviewer can take the "confirm" action)
- `roleGates` are stage-level restrictions (all actions blocked unless you have the role); the payment demo uses action-level restrictions

**Classification:** Accidental complexity
- They're a feature placeholder that the current demo doesn't use
- They add visual noise to the authored JSON without serving a purpose here
- Candidate for removal if the feature isn't planned

---

## What Looks Unnecessary

### 1. The `payment-confirmed` gateway (split with single route)
- **Why it exists:** Authoring convenience — all routes go through gateways
- **What it projects to:** A single direct transition `confirm-payment-received → await-payment-confirmation [confirm]`
- **Verdict:** Harmless but arguably over-engineered
  - An exclusive-choice split gateway (distinct triggers per route, or only one route) adds no runtime benefit
  - Could be simplified to a direct route concept, but current design is consistent

### 2. The `metadata` wrapper in the seed
- **Why?** Preserves authoring intent (lanes, gateways, stageType) without mutating the core runtime contract
- **Verdict:** Reasonable separation of concerns, though it does add a level of indirection
  - Core runtime only cares about `states` and `transitions`
  - Metadata is preserved for UI introspection and simulation

### 3. Empty `roleGates` arrays
- **Why?** Pre-built support for a feature not yet used in payment-demo
- **Verdict:** Unnecessary visual clutter
  - Should either be populated (if the feature is in use) or removed (if not planned)

---

## The Real Source of Truth

**Is the seed file actually used by the running editor?**

**No, not directly.** Here's the actual flow:

1. **Editor loads workflows from the backend:**
   - Endpoint returns `AuthoredWorkflow` (or wire-format equivalent)
   - This is constructed from `ReferenceWorkflowRepository` (C# in-memory) or a filesystem store
   - Never loaded from `payment-demo.json` seed file

2. **Seed file is only for:**
   - Runtime workflow engine initialization (loading published definitions)
   - Tests that verify the projected shape

3. **The `payment-demo.workflow.json` test fixture:**
   - This is what the editor tests expect
   - It's the **verified correct** shape that the projector should produce
   - Tests fail if the projector doesn't emit this exact structure

---

## Summary

| Aspect | Answer |
|---|---|
| **Why different shapes?** | Seed = projected runtime contract; Editor = authored model. Different design goals. |
| **Source of truth?** | `ReferenceWorkflowRepository` (C#) is authoritative. Editor loads from backend, not from seed file. |
| **The extra gateway?** | Authored all routes through gateways. Single-route split gateways flatten to direct transitions. Harmless. |
| **`roleGates` always empty?** | Feature placeholder. Demo doesn't use role-based stage restrictions (uses action-level restrictions instead). |
| **Harmful complexity?** | No. Extra layers (metadata wrapper, gateway indirection, empty roleGates) are harmless normalisation or reasonable feature scaffolding. |
| **Simplification opportunity?** | Yes: remove empty `roleGates` arrays from payment-demo fixture; they add noise without value. Consider single-route gateway flattening in authoring model if complexity bothers authors. |

---

## Directive Alignment

The user directive (Jonny, 2026-06-04T22:07) asks for:
> "Simplify the workflow definition so each visual lane is modeled directly as a queue. Do not keep separate lane keys plus queueName indirection. Gateways are first-class workflow definition elements, not metadata."

**Current state:**
- ✅ Gateways **are** first-class (gateways own all routing)
- ✅ Lanes are defined separately (not buried in metadata)
- ⚠️ Lane indirection exists: `lane.key` + `lane.queueName` suggest separate concepts
- ⚠️ Gateways are in metadata in the seed file (runtime preservation, not first-class in runtime contract)

**Recommendation:** This analysis confirms Jonny's intuition. The authored model is already clean; the seed file's metadata wrapper is just runtime scaffolding and doesn't muddy the editor experience.

---

### 2026-06-04T22:31:07.531+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Keep the workflow model simple enough that it does not need persisted lookup tables, cached indexes, or separate authored-vs-runtime JSON contracts.
**Why:** User request — captured for team memory

---

### 2026-06-04T22:07:18+01:00: User directive
**By:** Jonny (via Copilot)
**What:** Simplify the workflow definition so each visual lane is modeled directly as a queue. Do not keep separate lane keys plus queueName indirection. Gateways are first-class workflow definition elements, not metadata. Keep the workflow definition simple and explicit.
**Why:** Jonny sees the current authored/editor shape as overcomplicated and wants the shared model to match what the workflow actually is, with queues, stages, and gateways expressed directly rather than indirectly through metadata.

---

---
date: 2026-06-04T22:31:07.531+01:00
authored_by: isabelle
title: Flattened Workflow Editor Uses Canonical Definition Contract
status: PROPOSED
scope: Client Workflow Editor
priority: HIGH
---

# Decision: Keep the editor on the canonical workflow definition contract

## Context

The workflow editor had drifted into an authored-only model with stage/gateway route structures that then had to be translated back into the persisted workflow definition contract. That extra shape made the Definition tab, canvas, and host integration disagree about what the source of truth actually was.

## Decision

- The client editor now persists and round-trips the same `WorkflowDefinition` contract used in JSON seeds:
  - `states`
  - `transitions`
  - `metadata.lanes`
  - `metadata.gateways`
- Queue and gateway intent stays explicit in `metadata`, not in a separate authored-only payload.
- Client load/save no longer uses a distinct wire-format translator.
- While UI components finish migrating off older property names, the client may expose **non-enumerable compatibility accessors** in memory so existing editor surfaces can read the canonical data without polluting saved JSON.

## Consequence

The Definition tab and visual editor now speak the same persisted contract, and the saved JSON stays simple and flat. Remaining UI migration can happen incrementally without reintroducing a second stored schema.

---

# Flattened Workflow Tests — Behavioural Gate

**Date:** 2026-06-04T22:31:07.531+01:00  
**By:** Tangy (Tester)  
**For:** Blathers and Isabelle

---

## Decision

For the flattened-workflow refactor, the behavioural gate should follow the new product contract literally: the persisted workflow definition must expose **queues, stages, and gateways as first-class concepts in one shared shape**. Do **not** preserve the current dual-shape proof where editor fixtures, backend authored fixtures, and runtime seeds can all disagree yet still pass in isolation.

## Why

- Jonny's directive is explicitly about simplifying to one persisted definition with queues, stages, and gateways first-class.
- The current evidence shows drift already exists:
  - backend/runtime seed `workflow-seeds/payment-demo.json` is still `states + transitions + metadata.gateways`
  - frontend/editor fixture `fixtures/index.ts` still uses a different authored payment flow with `provider-processing`
  - backend reference workflow already models the newer join-wait payment flow with `confirm-payment-received` and `await-payment-confirmation`
- If the tests keep proving those three shapes separately, the refactor can "pass" while shipping another mismatch.

## Implications for implementation

1. **Blathers** should replace projector-era behavioural assertions with direct persisted-contract assertions.
2. **Isabelle** should treat the Definition tab and Storybook fixtures as the same flattened contract the backend persists.
3. **Payment demo** becomes the canary: queue ownership, join waiting, and gateway routing must match across backend tests, editor tests, and the live seed.

## Minimum proof set

- One backend contract that loads the persisted definition and proves:
  - `queues` are first-class, not `laneKey + queueName` indirection
  - `gateways` are first-class, not buried under metadata
  - join waiting stays on the gateway
- One editor Definition-tab contract that proves the JSON authors edit is that same flattened shape.
- One cross-surface payment-demo contract that proves the editor fixture, reference workflow, and persisted seed all describe the same payment topology.

---

---
date: 2026-06-04T22:31:07.531+01:00
authored_by: tom-nook
title: Flattened Workflow Model — Single Persisted Contract
status: LOCKED
scope: Runtime + Editor Foundation
priority: HIGH
---

# Flattened Workflow Model — Single Persisted Contract

## Problem Statement

The workflow model currently layers multiple overlapping representations:
- **AuthoredWorkflow** (C# + TS authoring schema with gateways/lanes/roles)
- **WorkflowDefinitionFile** (JSON-deserialized runtime contract)
- **ProjectedWorkflowDefinition** (TS editor projection)
- **Lookup tables** (`_instanceLookup`, state-key maps) built on read in WorkflowRuntimeEngine
- **Cached indexes** regenerated on every definition load

This layering introduces accidental complexity: multiple stages validate the same constraints, duplicate metadata flows through all three schemas, and simple lookups spawn extra allocations at runtime.

**User directive:** "You shouldn't need cached indexes or extra lookup tables if you keep it simple anyway."

## Target State: Single Canonical Contract

One **published workflow definition contract** serves both authoring and runtime:

```typescript
// Single canonical persisted format (JSON/C#)
interface WorkflowDefinition {
  definitionKey: string;
  displayName: string;
  version: number;
  initialState: string;
  instancePolicy: 'single' | 'multiple' | 'prompt';
  
  // Flat state list — no gateways, no lanes in the persisted shape
  states: State[];
  
  // Flat transition list — all routing pre-computed
  transitions: Transition[];
  
  // Authored-intent metadata (preserved for tracing, actions, roles)
  // Contains original lane/gateway/handoff structure for diagnostics only
  metadata?: AuthoredMetadata;
}

interface State {
  stateKey: string;
  displayName: string;
  components: Component[];
  metadata?: StateMetadata;
}

interface StateMetadata {
  description?: string;
  stageType?: 'Question' | 'CheckAnswers' | 'Confirmation' | 'TaskList';
  actor?: string;
  laneKey?: string;           // Which lane owns this state (queue routing)
  queueName?: string;         // Queue assigned to this lane
  roleGates?: string[];       // Required roles to access
  actions?: ActionDefinition[];
}

interface Transition {
  fromState: string;
  toState: string;
  action: string;
  requiresRole?: string;
  metadata?: TransitionMetadata;
}

interface TransitionMetadata {
  conditions?: ConditionDefinition[];
  actions?: ActionDefinition[];
}

interface AuthoredMetadata {
  authoredWorkflowId?: string;
  description?: string;
  schemaVersion?: string;
  lanes?: LaneDefinition[];
  gateways?: GatewayDefinition[];
  handoffs?: HandoffDefinition[];
  tags?: Record<string, string>;
}
```

## What Changes

### 1. **Remove AuthoredWorkflow from persisted contract**
- **Currently:** Authoring produces `AuthoredWorkflow`, which gets projected to `WorkflowDefinitionFile`, which gets deserialized and cached
- **Target:** Authoring produces `WorkflowDefinition` directly (author model → projected states + flat transitions → checksum → persist)
- **Why:** One schema, one validation, one source of truth

### 2. **Flatten transitions in publication**
- **Currently:** Gateways own routing logic; runtime must reconstruct transitions from `gateway.source × gateway.routes`
- **Target:** `WorkflowProjector.Project()` emits all transitions flat in the `WorkflowDefinition`; gateways are metadata-only
- **Why:** Runtime reads `definition.Transitions` directly; no reconstruction needed

### 3. **Remove lookup tables from WorkflowRuntimeEngine**
- **Currently:** `_instanceLookup` (user→instance keying) rebuilt on every engine init
- **Target:** Store instances by ID only; add **optional** index layer (user+definitionKey → [instanceIds]) only if querying by user becomes performance-critical
- **Why:** Lookups are premature complexity; add only if profiling shows contention

### 4. **Lanes/Queues/Roles in metadata only**
- **Currently:** Lanes are a first-class authored concept reconstructed at runtime
- **Target:** Lanes exist in `metadata.lanes`; states carry `laneKey` + `queueName` in their metadata; runtime evaluates access based on `roleGates` + host's queue context
- **Why:** Keeps persisted shape minimal; authored intent preserved for diagnostics

### 5. **Components pass through unchanged**
- **Currently:** Components are identical in all three schemas
- **Target:** No change; they remain polymorphic, pass through unchanged
- **Why:** Already minimal

## Sequencing for Blathers & Isabelle

### Phase 1: Editor (Isabelle)
**Goal:** Isabelle's authoring model outputs `WorkflowDefinition` directly (not `AuthoredWorkflow` + projection)

**Changes:**
1. Replace `types.ts` `AuthoredWorkflow` shape with new `WorkflowDefinition` interface (keep gateways/lanes in `metadata` only)
2. Update `workflow-canonical-json.ts` to flatten gateways into `transitions` on output
3. Update `workflow-validation.ts` to validate flat transition list directly
4. Remove gateway/lane reconstruction from editor projection logic
5. Update `prism-definition-editor.ts` to emit `WorkflowDefinition` to backend

**Migration:** Old authored workflows in-flight can be converted client-side before save.

**File paths:**
- `src/UmbracoPrism.Client/src/workflow-editor/types.ts` — replace `AuthoredWorkflow` with `WorkflowDefinition`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-canonical-json.ts` — flatten to transitions on toJSON()
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-validation.ts` — validate flat contracts
- `src/UmbracoPrism.Client/src/workflow-editor/prism-definition-editor.ts` — emit `WorkflowDefinition`

### Phase 2: Runtime Backend (Blathers)
**Goal:** Runtime reads `WorkflowDefinition` directly; remove cached indexes

**Changes:**
1. Remove `WorkflowProjector` (or reduce to import adapter for legacy compat)
   - Move projection logic into editor (Isabelle's phase 1)
   - Blathers receives `WorkflowDefinitionFile` already flat
2. Update `WorkflowDefinitionFile.cs` to match new contract (remove AuthoredWorkflow references)
3. Update `WorkflowRuntimeEngine`:
   - Remove `_instanceLookup` dictionary
   - Keep `_definitions` (workflow lookups are legitimate)
   - Remove `LookupKey()` user-based keying (instances addressed by ID only)
4. Simplify `FindAccessibleWorkItems()` to evaluate `roleGates` + `queueName` from state metadata
5. Remove `ReferenceWorkflowProjector` projection step; seed files are now raw `WorkflowDefinitionFile` JSON

**File paths:**
- `src/UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs` — update schema
- `src/UmbracoPrism.WorkflowRuntime/Services/WorkflowRuntimeEngine.cs` — remove `_instanceLookup`, simplify lookup
- `src/UmbracoPrism.WorkflowEditor/Authoring/WorkflowProjector.cs` — reduce or remove (move logic to editor)
- `src/UmbracoPrism.MockBusinessApp/Services/ReferenceWorkflowDefinitionStore.cs` — no longer calls projector; seeds are raw definitions

### Phase 3: Seed Files
**Goal:** Update reference workflows to flat format

**Changes:**
1. `payment-demo.json` — remove `gateways` array; convert all routing to flat `transitions` with role/queue context in metadata
2. Validate round-trip: author model → persisted → runtime reads → no reconstruction needed

**File paths:**
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/payment-demo.json`
- `src/UmbracoPrism.MockBusinessApp/workflow-seeds/*.json` (all other demo workflows)

## Benefits

| Aspect | Before | After |
|--------|--------|-------|
| **Persisted schemas** | 3 (AuthoredWorkflow, WorkflowDefinitionFile, ProjectedWorkflow) | 1 (WorkflowDefinition) |
| **Validation passes** | 2–3 (authoring, projection, runtime) | 1 (on publish) |
| **Runtime lookups** | `_definitions` + `_instanceLookup` + on-demand state maps | `_definitions` only |
| **Allocation churn** | Rebuild indexes on engine init | None (deferred if needed) |
| **Editor publish payload** | AuthoredWorkflow → Project → Serialize → Deserialize | WorkflowDefinition → Serialize |
| **Authored intent** | Baked into runtime logic | Preserved in metadata for diagnostics |

## Risk Mitigation

**Migration:** Editor supports both old (AuthoredWorkflow) and new (WorkflowDefinition) formats during transition; server-side adapter converts old on read.

**Compat:** Existing instance data (current workflows in flight) unaffected; only definitions change shape on next publish.

**Validation:** Flatten-to-transitions logic is deterministic; tests lock byte-identical output before/after to catch regressions.

## Decision

✅ **APPROVED.** Proceed with single-contract model. Isabelle (editor) moves first; Blathers (runtime) follows once Isabelle publishes flat definitions; Scribe updates seed files.

---

**Locked by:** tom-nook (2026-06-04T22:31:07+01:00)
**Affects:** Blathers, Isabelle
**Dependencies:** None
**Follow-up:** Trace async migration path for in-flight workflows if needed (separate decision).

---

---
date: 2026-06-04T22:07:18+01:00
author: Tom Nook
decision: Flatten authorised model to queue-first; elevate gateways out of metadata
---

# Architectural Review: Is the Authored Model Overcomplicated?

**Jonny's hypothesis:** Each visual lane is really just a queue. We're carrying unnecessary indirection with separate lane keys and queueName. Gateways should not be buried in metadata; they are first-class workflow definition. The whole definition should be explicit and simple.

**Verdict:** ✅ **Jonny is right.** The current shared model has real indirection that adds accidental complexity. The payoff for flattening is high: both the model AND runtime will be simpler.

---

## Current State: What We Have

### Authored Model (AuthoredWorkflow)
```
+ lanes: [{ key, displayName, actor, queueName, roleGates }]
+ gateways: [{ key, displayName, laneKey, source, routes, ... }]
+ stages: [{ key, displayName, laneKey, ... }]
```

### Runtime Model (Published, WorkflowDefinitionFile)
```
+ states: [{ stateKey, displayName, metadata: { laneKey, actor, ... } }]
+ transitions: [{ fromState, toState, action, ... }]
+ metadata.lanes: [{ key, displayName, actor, queueName, ... }]
+ metadata.gateways: [{ key, displayName, laneKey, gatewayType, ... }]
```

**Problem:** Lanes and gateways are authored as "first-class" but dispatched to "optional metadata" at runtime. This treats workflow structure as decoration rather than definition.

---

## Core Issues: Identified and Ranked

### 1. **Lane Key + QueueName Redundancy** (HIGH) ❌

Currently:
```json
{
  "key": "applicant",
  "displayName": "Applicant",
  "actor": "applicant",
  "queueName": "web-user"
}
```

Both `key` and `queueName` name the same concept (a queue). You use `key` for internal references (in stages/gateways), but `queueName` is what the runtime actually cares about (for assignment, queuing, authorization).

**Result:** You pay lookup cost everywhere — resolve `laneKey` → look up lane → get `queueName` → assign to queue.

**Simplification:** Use ONE identifier. Either:
- **Option A (Recommended):** Make the `key` BE the queue identifier. Drop `queueName`.  
  ```json
  { "key": "web-user", "displayName": "Applicant", "actor": "applicant" }
  ```
  Stages/gateways reference `"web-user"` directly. No double lookup.
  
- **Option B:** If you need distinct "author-friendly names" vs "queue identifiers", rename for clarity but don't duplicate.

**Impact:** ✅ Model clarity, ✅ runtime efficiency, ✅ easier debugging.

---

### 2. **Gateways Buried in Metadata** (HIGH) ❌

Current design:
- **Authored:** Gateways in top-level array (good — first-class)
- **Runtime:** Gateways moved to `metadata.gateways` (bad — optional, decorative)

But gateways are NOT optional. They are the **routing skeleton** of the workflow:
- Split gateways determine which cursors go where.
- Join gateways determine synchronization barriers.
- Without gateways, you cannot understand how the workflow actually flows.

**Evidence from current code:**
- `WorkflowProjector` compiles gateways into `transitions` (line 80).
- At runtime, the engine reads gateways to handle Split/Join (not yet implemented, but core to the design).
- If you serialize a workflow without gateways metadata, you lose the routing intent.

**Simplification:** Elevate gateways out of metadata into the core `WorkflowDefinitionFile`:
```
WorkflowDefinitionFile {
  states: [...]
  transitions: [...]
  gateways: [...]  // ← TOP LEVEL, not buried
  queues: [...]    // ← TOP LEVEL, not buried
}
```

**Impact:** ✅ Gateways become explicit and non-optional, ✅ runtime implementations know gateways are always available, ✅ schema clarity.

---

### 3. **"Lane" Concept Obscures "Queue"** (MEDIUM) ⚠️

The term **"lane"** is borrowed from pool/swim-lane diagrams and is useful for visual modeling. But it conflates two concerns:
1. **Visual grouping** (how stages appear in the editor)
2. **Queue assignment** (who works on this stage)

At runtime, there are no "lanes" — there are **queues** (assignment groups). The lane is just the authored metaphor.

**Current confusion:**
- `stage.laneKey` — What lane does this stage belong to? (authoring concept)
- `metadata.lanes` — But then queueName is the actual queue. (runtime concept)
- Stages carry both `laneKey` AND implicit `actor` — which one takes precedence?

**Simplification:** Rename `lanes` → `queues` in the shared model. Be explicit about what a queue is:
```
AuthoredWorkflow {
  queues: [{
    key: "web-user",           // identifier (was: lane.key)
    displayName: "Applicant",
    actor: "applicant",
    permissions: ["read", "submit"]  // optional; was: roleGates
  }],
  stages: [{
    key: "enter-details",
    queueKey: "web-user",      // explicit: this stage is in web-user queue
    ...
  }],
  gateways: [{
    key: "await-payment",
    type: "Join",
    queueKey: "applicant",     // explicit: gate is owned by applicant queue
    requiredIncomingQueues: ["applicant", "business-user"],
    ...
  }]
}
```

**Impact:** ✅ Mental model clarity, ✅ removes indirection, ✅ easier to explain to non-technical stakeholders.

---

### 4. **Indirection in Stage/Gateway Assignment** (MEDIUM) ⚠️

Current pattern:
```
stage: {
  laneKey: "applicant",       // reference to lane
  actor: "applicant"          // direct override
}
```

You then have `ResolveAssignment()` logic (line 473 in WorkflowProjector) that:
1. Takes the `laneKey` and looks up the lane.
2. Falls back to lane's `actor` if stage doesn't override.
3. Same for `roleGates`.

This is hierarchy resolution (good for DRY) but also hidden behavior (bad for clarity).

**Simplification:** 
- For **simple workflows:** Embed queue assignment directly in stages/gateways.
  ```
  stage: { key: "...", queueKey: "web-user", actor: "applicant" }
  ```
- For **complex workflows with shared metadata:** Keep a queue definition but make it optional. If a stage doesn't have `actor`, it inherits from queue.

**Current code already supports this.** The `ResolveAssignment` logic is sound; we just need to simplify the presentation.

**Impact:** ✅ Fewer surprises for authors, ✅ explicit where assignment comes from.

---

### 5. **Are Current Indirections Genuinely Useful?** (LOW) ✅

One thing the current model does well:
- **Shared actor/permissions across stages:** If you have 10 applicant-facing stages, defining the queue once is cleaner than repeating `actor: "applicant"` on each.

**Keep this pattern,** but make it opt-in and clear. Queues should be a place to store shared metadata. This is not indirection; this is abstraction, and it's good.

---

## Recommended Target Shape

### New Authored Model (Simplified)

```typescript
AuthoredWorkflow {
  id: uuid                          // Surrogate ID (unchanged)
  definitionKey: string             // e.g., "payment-demo"
  displayName: string               // e.g., "Payment Demo"
  version: int                      // Monotonic version
  schemaVersion: string             // Migration guard
  initialStageKey: string           // Entry point
  instancePolicy: string            // "single" | "multiple" | "prompt"
  
  queues: [{
    key: string                     // e.g., "web-user" — PRIMARY IDENTIFIER
    displayName: string             // e.g., "Applicant"
    actor?: string                  // e.g., "applicant" (defaults to key if omitted)
    permissions?: string[]          // e.g., ["read", "submit"] (optional role gates)
  }]
  
  stages: [{
    key: string
    displayName: string
    queueKey: string                // Which queue owns this stage
    type: "Question" | "CheckAnswers" | "Confirmation" | "TaskList"
    components: Component[]         // Render payload (unchanged)
    actions?: Action[]              // (unchanged)
  }]
  
  gateways: [{
    key: string                     // e.g., "await-payment"
    displayName: string
    type: "Split" | "Join"          // (unchanged)
    source: string                  // Which stage sources this gateway (unchanged)
    queueKey: string                // Which queue owns this gateway
    routes: [{
      id: string
      target: string                // Destination stage
      trigger: string               // Action (unchanged)
      condition?: Condition
      actions?: Action[]
    }]
    // Join-specific:
    requiredIncomingQueues?: string[] // NOT lanes; explicit queues
    waitingInfo?: WaitingMetadata
  }]
  
  handoffs?: Handoff[]              // Agent insertion points (unchanged)
  parameterSchemas?: ParameterSchema[]  // Reusable action schemas (unchanged)
  metadata?: Record<string, string>     // Arbitrary tags (unchanged)
  authorNote?: string               // Editor comment (unchanged)
}
```

### Key Changes from Current

| Aspect | Current | New | Why |
|--------|---------|-----|-----|
| Lane keys + queueName | Separate | One `key` | Eliminate redundancy |
| "Lanes" concept | Top-level `lanes` array | Renamed to `queues` | Clarity about what they represent |
| Gateway location | Authored top-level; runtime metadata | Authored top-level; runtime top-level | Gateways are always first-class |
| Stage/gateway ownership | `laneKey` → lookup lane → inherit | `queueKey` direct reference | Direct, no lookup |
| Queue references | By `laneKey` (confusing name) | By `queueKey` (clear name) | Consistency |
| Required incoming | `requiredIncomingLanes: ["applicant", "payments"]` | `requiredIncomingQueues: ["web-user", "business-user"]` | Uses queue keys, not lane keys |

---

## Runtime Impact

### Published Model (Simplified)

```typescript
WorkflowDefinitionFile {
  definitionKey: string
  displayName: string
  version: int
  initialState: string
  states: StepDefinition[]          // (unchanged)
  transitions: WorkflowTransitionFile[]  // (unchanged)
  
  // NOW TOP-LEVEL, NOT METADATA:
  queues?: WorkflowQueueDefinition[]
  gateways?: WorkflowGatewayDefinition[]
  
  // OPTIONAL (for compatibility):
  metadata?: {
    description?: string
    handoffs?: HandoffDefinition[]
    tags?: Record<string, string>
  }
}
```

### Runtime Implementation Gains

1. **Eliminates lookup logic:** No `ResolveAssignment()` indirection. Queue is found directly by key.
2. **Explicit routing:** Engine sees gateways at top level. No need to fish them out of metadata.
3. **Clearer async model:** Join gateways are explicit synchronization points, not buried metadata.
4. **Better diagnostics:** "Why didn't this stage run?" → Check queue assignment directly.

---

## Migration Path (Not in This Session)

1. **Parser:** Extend JSON-schema to allow both old (metadata.lanes/gateways) and new (top-level) formats.
2. **Projector:** Normalize old format to new during projection.
3. **Editor:** Update UI to talk about queues, not lanes (already started per Isabelle's decision).
4. **Deprecation:** Phase out old format over one or two versions.

---

## Decision

**Yes, simplify.** The current model is overcomplicated by design debt (lane key + queueName) and missing elevation (gateways should not be metadata).

### What to Flatten

| Item | Action | Reason |
|------|--------|--------|
| Lane `key` + `queueName` | Merge into single queue identifier | Redundancy. Use key for all references. |
| "Lanes" terminology | Rename to "queues" in shared model | Clarity. Queues are what they represent. |
| Metadata.gateways | Move to top-level `gateways` in runtime | Gateways are first-class routing nodes, not decoration. |
| `ResolveAssignment()` helper complexity | Simplify; make queue inheritance opt-in | Reduce lookup indirection; keep shared metadata when useful. |
| Metadata.lanes | Move to top-level `queues` in runtime | Consistency; queues are structural, not optional metadata. |

### What to Keep

| Item | Reason |
|------|--------|
| Queues as a named collection | Allows shared metadata (actor, permissions) across multiple stages/gateways. DRY is good. |
| Inheritance from queue to stage | Optional but useful. If stage doesn't specify `actor`, inherit from queue. Keep simple. |
| Handoffs as separate | Orthogonal concern (agent insertion points). Not needed on every workflow. Keep optional. |

---

## Summary for the Team

**The model is indeed overcomplicated.** Flattening it means:

- **For authors:** Easier to reason about. "This stage is in the web-user queue. This gateway joins the web-user and business-user queues."
- **For runtime:** Simpler, no buried metadata lookups, gateways are explicit first-class nodes.
- **For the editor:** Continues the "queue-first" language shift Isabelle started.

**Do it.** The payoff in clarity and runtime simplicity far outweighs the refactoring cost. This is not premature optimization; it's clearing accidental complexity.

---

*— Tom Nook, Lead / Architect*  
*On behalf of Squad*
