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

---

### 2026-06-05T06:20:10.339+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Remove lanes fully and model workflows as top-level queues containing states, gateways, and routes; states route only to gateways, gateways route to states or gateways, and payment must be the clean demo with validations working across the whole stack.
**Why:** User request — captured for team memory

---

# 2026-06-05T06:20:10.339+01:00 — Queue-only runtime contract follow-through

**Author:** Blathers  
**Requested by:** Jonny Muir

## Decision

The backend/runtime now treats the queue-only workflow graph as canonical:
- `queues` replace top-level `lanes`
- `queueKey` replaces `laneKey` on states and gateways
- `routes` live on states and gateways
- join gateways wait on `requiredIncomingQueues`
- payment-demo is the reference proof flow

## Compatibility rule

To keep existing fixtures, publishing, and runtime tests working during the migration, the backend still reads legacy `lanes`, `laneKey`, `queueName`, `source`, `requiredIncomingLanes`, and `transitions` when present. New queue-only payloads write and validate against the canonical queue-first shape.

## Runtime rule

When a route lands on a join gateway, the runtime preserves the arriving queue identity on the parked cursor. Join release only happens once every required incoming queue has arrived, even when the final arrival came through an intermediate split gateway.

---

# Queue-only editor contract landed

**Date:** 2026-06-05T06:20:10.339+01:00  
**Author:** Isabelle (Frontend Dev & Accessibility Lead)

## Decision

The workflow editor now treats the queue-only contract as canonical:

- `queues[]` is the top-level ownership model for the editor and Definition tab
- states own outbound `routes[]`
- gateways own outbound `routes[]`
- the editor derives any legacy flat-transition view only as a compatibility helper
- payment demo is the reference proof for applicant → payments-team queue handoff via split/join gateways

## Why

Jonny's directive was to remove lanes and separate transitions from the authoring experience. Keeping routes on the owning state/gateway makes the canvas, validation rail, Definition tab, and payment proof all teach the same model.

## Frontend implications

1. Editor serialisation now writes queue-first JSON instead of flat `transitions[]`.
2. Canvas and inspector use queue labels from the host when available, with workflow queues as the fallback source of truth.
3. Validation and definition-sync tests now prove queue-owned routing instead of lane/transition semantics.

## Follow-up

If any remaining backend/runtime compatibility code still reads legacy lane or flat-transition fields, it should be treated as read-only fallback and removed once the wider stack is fully queue-only.

---

## 2026-06-05T06:20:10.339+01:00: Queue-only behavioural test gate

**Author:** Tangy  
**Requested by:** Jonny Muir

### Backend behavioural tests that must change

- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/PaymentDemoReferenceWorkflowTests.cs` — currently proves lane metadata (`definition.Lanes`, `RequiredIncomingLanes`, cursor `LaneKey`) instead of a queue-only contract.
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/MultiLaneGatewayContractTests.cs` — every core assertion is phrased in lane ownership terms and must move to queue ownership terms.
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/ProjectorEngineGatewayIntegrationTests.cs` — join release proof is tied to “required lanes”; the same runtime proof needs queue wording and queue-owned fixtures.
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowGatewayProjectionTests.cs` — projection proof currently sorts and emits `RequiredIncomingLanes`; this must become the queue-only routing rule.
- `src/UmbracoPrism.Core.Tests/Workflow/Authoring/WorkflowSimulationServiceTests.cs` and `AuthoredWorkflowValidationTests.cs` — both need explicit queue-only validation cases proving states never route directly to states and gateways only target states or gateways.

### Client behavioural tests that must change

- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-parallel-lanes.spec.ts` — entire spec is lane-column proof and should become queue-column proof.
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-canvas-lane-fit.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-layout-proof.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-visual.spec.ts`
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-gateways.spec.ts` — currently asserts `data-prism-lane` ownership.
- `src/UmbracoPrism.Client/tests/workflow-editor/workflow-editor-shell.spec.ts` — payment-demo proof is stale (`provider-processing`) and must assert the real queue-only demo instead.
- `src/UmbracoPrism.Client/tests/workflow-editor/support/canvas-helpers.ts` — shared geometry helpers still measure lane containers and will keep the visual suite dishonest until moved to queue terminology.

### Acceptance criteria for Blathers

1. Backend validation rejects any queue-only workflow where a state routes directly to a state, or a gateway routes to anything other than a state or gateway.
2. Queue-only fixtures and projection/runtime tests use queues as the first-class owner model; no behavioural proof should depend on `lanes` or `laneKey`.
3. Payment demo persists as the clean reference: queue-owned states/gateways/routes, no legacy placeholder stage, applicant submit defers on the waiting gateway, business queue receives the confirmation work item, confirm releases to `payment-complete`.

### Acceptance criteria for Isabelle

1. Editor/shell/canvas tests speak in queue terms, not lane terms, and render queue ownership honestly in DOM/data attributes.
2. Payment demo in the editor loads the real queue-only graph and passes the validation rail with zero issues.
3. The editor no longer proves the retired `provider-processing` story; it must instead show the waiting gateway plus the back-office confirmation state from the real payment demo.

---

# Queue-Only Workflow Model — Definition & Implementation Plan

**Author:** Tom Nook (Lead)  
**Date:** 2026-06-05T06:20:10Z  
**Status:** ✅ Locked for Blathers & Isabelle implementation  
**Directive:** User input: "We can remove lanes fully, we only need queues. A queue is a lane. Lets make this as simple as possible. A workflow is made up of queues. Each queue contains states and gateways between states... replace the concept of a separate transition with routes which belong to states... A gateway may route to another gateway or a state... work across all of the workflow code... use the payment workflow... make sure validations work..."

---

## 1. Queue-Only JSON Contract

### Top-Level Shape
```typescript
{
  definitionKey: string                // "payment-demo"
  displayName: string                  // "Payment Demo"
  version: number                      // 1
  initialState: string                 // "enter-details" (stateKey, not gatewayKey)
  instancePolicy: string               // "single"
  description?: string
  
  // NEW: Queues become the organizational unit (replaces lanes)
  queues: QueueDefinition[]            // Top-level item replaces lanes

  // UNCHANGED at top level
  states: StepDefinition[]
  gateways: WorkflowGatewayDefinition[]
  
  // DEPRECATED
  transitions?: WorkflowTransitionFile[] // Now only in gateway.routes
  lanes?: WorkflowLaneDefinition[]        // REMOVED—queues replace this
}
```

### Queue Definition (replaces Lane)
```typescript
interface QueueDefinition {
  key: string                        // "web-user", "business-user", "applicant" etc
  displayName: string                // User-facing name
  
  // CHANGED: No longer separate lane + queue concept
  // Queue IS the organizational container and access boundary
  
  // Optional metadata
  description?: string
  actor?: string                     // Optional human role label
  roleGates?: string[]               // Optional access-control roles
  tags?: Record<string, string>
}
```

### State Definition (owned by a single queue)
```typescript
interface StepDefinition {
  stateKey: string                   // "enter-details"
  displayName: string
  
  // REQUIRED: Every state belongs to exactly one queue
  queueKey: string                   // NOT laneKey—now queueKey (queue required, not optional)
  
  // Routes now live here (CHANGED)
  routes?: WorkflowRouteDefinition[] // Outbound routes FROM this state
  
  // Existing
  description?: string
  stageType?: string
  actor?: string
  roleGates?: string[]
  components: PrismComponent[]
  actions?: WorkflowActionDefinition[]
}
```

### Gateway Definition (owned by a single queue)
```typescript
interface WorkflowGatewayDefinition {
  key: string                        // "submit-payment"
  displayName: string
  description?: string
  
  // Gateway type (existing)
  gatewayType: "Split" | "Join"
  
  // CHANGED: Queue ownership (not lane)
  queueKey: string                   // "web-user" or "business-user"
  
  // REMOVED: laneKey is redundant with queueKey
  // REMOVED: source field (see below for routing model)
  
  // Routes now live here (CHANGED)
  routes: WorkflowRouteDefinition[]  // Outbound routes FROM this gateway
  
  // Join-only metadata
  waitingContent?: string
  waitingExpectedSeconds?: number
  waitingPollIntervalMs?: number
  waitingAllowDefer?: boolean
  waitingDeferMessage?: string
  requiredIncomingQueues?: string[]  // CHANGED: Was requiredIncomingLanes
}
```

### Route Definition (new structure)
```typescript
interface WorkflowRouteDefinition {
  id: string                         // "submit-payment--release--payment-complete"
  
  // Target can be a state or gateway key
  target: string                     // "await-payment-confirmation" or "payment-complete"
  
  // What action/event triggers this route
  trigger: string                    // "submit", "confirm", "release"
  
  // Conditions & side effects (existing)
  requiresRole?: string
  conditions?: WorkflowConditionDefinition[]
  actions?: WorkflowActionDefinition[]
}
```

### Payment Demo in New Contract
```json
{
  "definitionKey": "payment-demo",
  "displayName": "Payment Demo",
  "version": 1,
  "initialState": "enter-details",
  "instancePolicy": "single",
  "description": "Payment flow showing the web queue handing off to the business queue before completion",
  
  "queues": [
    {
      "key": "web-user",
      "displayName": "Applicant",
      "actor": "applicant",
      "description": "Web user entering payment details and awaiting confirmation"
    },
    {
      "key": "business-user",
      "displayName": "Payments team",
      "actor": "reviewer",
      "description": "Back-office team confirming payment receipt"
    }
  ],
  
  "states": [
    {
      "stateKey": "enter-details",
      "displayName": "Enter payment details",
      "queueKey": "web-user",
      "stageType": "Question",
      "actor": "applicant",
      "description": "Provide the payment details for this application.",
      "components": [
        {
          "type": "fieldset",
          "legend": "Payment details",
          "children": [
            {"type": "text", "fieldKey": "cardholderName", "label": "Cardholder name", "required": true},
            {"type": "decimal", "fieldKey": "amount", "label": "Amount (£)", "required": true}
          ]
        }
      ],
      "routes": [
        {
          "id": "enter-details--submit--split-gateway",
          "target": "submit-payment",
          "trigger": "submit"
        }
      ]
    },
    {
      "stateKey": "confirm-payment-received",
      "displayName": "Confirm payment received",
      "queueKey": "business-user",
      "stageType": "Question",
      "actor": "reviewer",
      "description": "Back-office confirmation step for reconciling the payment.",
      "components": [
        {
          "type": "fieldset",
          "legend": "Confirmation details",
          "children": [
            {"type": "text", "fieldKey": "confirmationReference", "label": "Confirmation reference", "required": true},
            {"type": "decimal", "fieldKey": "amountReceived", "label": "Amount received (£)", "required": true}
          ]
        }
      ],
      "routes": [
        {
          "id": "confirm-payment-received--confirm--route-gateway",
          "target": "confirm-payment-route",
          "trigger": "confirm"
        }
      ]
    },
    {
      "stateKey": "payment-complete",
      "displayName": "Payment complete",
      "queueKey": "web-user",
      "stageType": "Confirmation",
      "actor": "applicant",
      "description": "Confirms that the payment has been matched and the receipt is on its way.",
      "components": []
    }
  ],
  
  "gateways": [
    {
      "key": "submit-payment",
      "displayName": "Submit payment → notify back-office",
      "gatewayType": "Split",
      "queueKey": "web-user",
      "description": "Applicant submits payment; routes to both waiting state and back-office processing.",
      "routes": [
        {
          "id": "submit-payment--split--await-payment-confirmation",
          "target": "await-payment-confirmation",
          "trigger": "submit"
        },
        {
          "id": "submit-payment--split--confirm-payment-received",
          "target": "confirm-payment-received",
          "trigger": "submit"
        }
      ]
    },
    {
      "key": "await-payment-confirmation",
      "displayName": "Awaiting payment confirmation",
      "gatewayType": "Join",
      "queueKey": "web-user",
      "description": "Join point where applicant waits for back-office confirmation.",
      "requiredIncomingQueues": ["web-user", "business-user"],
      "waitingContent": "We're waiting for the payments team to confirm receipt of your payment.",
      "waitingExpectedSeconds": 60,
      "waitingPollIntervalMs": 5000,
      "waitingAllowDefer": true,
      "waitingDeferMessage": "You can leave this page and return later.",
      "routes": [
        {
          "id": "await-payment-confirmation--release--payment-complete",
          "target": "payment-complete",
          "trigger": "release"
        }
      ]
    },
    {
      "key": "confirm-payment-route",
      "displayName": "Record payment confirmation",
      "gatewayType": "Split",
      "queueKey": "business-user",
      "description": "Back-office confirms the payment and signals the join gateway.",
      "routes": [
        {
          "id": "confirm-payment-route--confirm--await-payment-confirmation",
          "target": "await-payment-confirmation",
          "trigger": "confirm",
          "requiresRole": "reviewer"
        }
      ]
    }
  ]
}
```

---

## 2. Routing Model Semantics

### Key Change: Routes belong to source nodes
- **States can have outbound routes** → point to gateways or terminal states
- **Gateways can have outbound routes** → point to states or other gateways
- **No direct state-to-state transitions** (all must go through a gateway for explicit routing logic)
- **No separate `transitions` array** (routes are now embedded in source nodes)

### Source Inference
- **For gateways:** If a route target is a state, the runtime knows where the route came from (the gateway's key)
- **For states:** If a route target is a gateway, the state is the source
- **NO `source` field needed in gateways** because the gateway itself IS the routing node

### Example Flow
```
enter-details (state)
  └─ routes: [submit-payment (gateway)]
    
submit-payment (gateway)
  └─ routes: [
       await-payment-confirmation (gateway),
       confirm-payment-received (state)
     ]

confirm-payment-received (state)
  └─ routes: [confirm-payment-route (gateway)]

confirm-payment-route (gateway)
  └─ routes: [await-payment-confirmation (gateway)]

await-payment-confirmation (gateway)
  └─ routes: [payment-complete (state)]

payment-complete (state) — terminal, no routes
```

---

## 3. Code Areas for Implementation

### Blathers (Runtime)

#### 3a. Workflow Definition Loading & Validation
- **File:** `UmbracoPrism.Shared/Models/Workflow/WorkflowDefinitionFile.cs`
  - Update `WorkflowDefinitionFile` record:
    - Remove `Transitions` (legacy field, routes now in states/gateways)
    - **ADD** `Queues: IReadOnlyList<QueueDefinition>`
    - Remove `Lanes` from top-level (kept only in legacy Metadata)
  - Update `StepDefinition`:
    - Rename `LaneKey` → `QueueKey` (required, not nullable)
    - **ADD** `Routes: IReadOnlyList<WorkflowRouteDefinition>`
  - Update `WorkflowGatewayDefinition`:
    - Rename `LaneKey` → `QueueKey`
    - Remove `Source` field (no longer needed—gateway IS the routing node)
    - Rename `RequiredIncomingLanes` → `RequiredIncomingQueues`
  - **DELETE** `WorkflowLaneDefinition` record (replaced by `QueueDefinition`)
  - **DELETE** `WorkflowTransitionFile` record (routes now in source nodes)

#### 3b. Backward Compatibility Layer
- Keep `WorkflowDefinitionMetadata` for loading legacy seeds
- Add migration logic in `FilesystemWorkflowDefinitionStore.cs`:
  - Detect old `lanes`/`transitions`/`gateways` in Metadata
  - Convert to new `queues` + embedded routes on first load
  - Persist converted form so old seeds are never loaded twice
  - **Quality gate:** Payment-demo.json roundtrip test proves conversion works

#### 3c. Workflow Routing Engine
- **File:** `UmbracoPrism.WorkflowRuntime/Services/WorkflowRuntimeEngine.cs`
  - Update routing logic to traverse routes from `states` and `gateways` instead of `transitions`
  - Update `GetNextStates()` method:
    - For current state: read `state.routes[]` to find target gateways
    - For current gateway: read `gateway.routes[]` to find target states/gateways
  - Update join gateway logic to use `requiredIncomingQueues` instead of `requiredIncomingLanes`
  - **RISK:** If routing incorrectly defaults to first route or crashes on missing routes, workflow instances can get stuck

#### 3d. Validation & Quality Gates
- **File:** Create `UmbracoPrism.Core/Services/Workflow/WorkflowDefinitionValidator.cs` (if not exist)
  - **Validation 1:** Every state must have `queueKey` (required, never null)
  - **Validation 2:** Every gateway must have `queueKey` (required)
  - **Validation 3:** Every route's `target` must resolve to an existing state or gateway key
  - **Validation 4:** `InitialState` must be a state key, never a gateway
  - **Validation 5:** Join gateway must have `requiredIncomingQueues` (not optional)
  - **Validation 6:** Terminal states must have empty or null routes
  - **Validation 7:** No cycles in route graph (DFS detection)
  - **Quality gate test:** `PaymentWorkflowDefinitionValidationTests` covering all scenarios

#### 3e. Payment Demo Roundtrip Test
- **File:** `UmbracoPrism.Core.Tests/Workflow/Components/SeedFileRoundtripTests.cs`
  - Load `payment-demo.json` from seed
  - Assert all states have `queueKey` set
  - Assert all gateways have `queueKey` set
  - Assert no `transitions` array (routes now embedded)
  - Assert `queues` array is present and populated
  - Assert `routes` on first state point to `submit-payment` gateway
  - Assert join gateway has `requiredIncomingQueues: ["web-user", "business-user"]`
  - Assert routing engine can traverse from start to end

#### 3f. Impact Analysis — Runtime Instance Projection
- **File:** `UmbracoPrism.WorkflowRuntime/Services/WorkflowInstanceProjector.cs` (if exists)
  - Instance state is keyed by state/gateway key (unchanged)
  - Route traversal now reads from source node instead of querying transitions
  - **RISK:** If instances exist with unknown state keys, validation will fail—add fallback logging

### Isabelle (Editor)

#### 4a. Authored Workflow Schema
- **File:** `UmbracoPrism.WorkflowEditor/Authoring/AuthoredWorkflow.cs`
  - Rename `AuthoredLane` → `AuthoredQueue` (or consolidate into definition)
  - Update `AuthoredStage` / `AuthoredGateway`:
    - Add `laneKey` property as legacy alias
    - Add `queueKey` as canonical property
    - Routes: ensure they live on source stages/gateways, not in a flat array
  - **File:** `UmbracoPrism.WorkflowEditor/Authoring/AuthoredTransition.cs`
    - **DEPRECATE** — routes now live on source nodes
    - If legacy data still references transitions, convert to embedded routes during load

#### 4b. Workflow Projector
- **File:** `UmbracoPrism.WorkflowEditor/Authoring/WorkflowProjector.cs`
  - Update projection logic:
    - Input: `AuthoredWorkflow` (with queues, states with routes, gateways with routes)
    - Output: `WorkflowDefinitionFile` (with `queues`, no `transitions`, routes embedded)
    - Ensure all routes are migrated from old transition/transition-metadata format
  - **Quality gate test:** `WorkflowProjectorTests` with payment demo proves correct projection

#### 4c. Canvas Editor — Visual Model
- **File:** `UmbracoPrism.Client/src/workflow-editor/canvas/...`
  - Update graph rendering:
    - Nodes: stages + gateways (unchanged visually)
    - Edges: now read from `state.routes` and `gateway.routes` instead of flat transitions
    - Lane/queue swim lanes: update to read from `queues` instead of `lanes`
  - Update node inspector:
    - States: show `queueKey` dropdown instead of `laneKey`
    - Gateways: show `queueKey` dropdown, remove `source` field input
    - Routes: now edited inline on state/gateway instead of in a separate transitions tab
  - **Quality gate test:** `workflow-parallel-queues.spec.ts` proves visual read matches model

#### 4d. Authoring API / Save Path
- **File:** `UmbracoPrism.WorkflowEditor/Authoring/AuthoringWorkflowService.cs` (or equivalent)
  - On save:
    - Validate all states have `queueKey`
    - Validate all gateways have `queueKey`
    - Convert embedded routes to canonical form (no source field needed)
    - Emit only `queues` (not `lanes`), no `transitions` array
  - On load (legacy):
    - If old format (lanes + transitions), migrate to queues + embedded routes
    - Persist migrated form immediately to prevent re-migration

#### 4e. Live Authored Seed
- **File:** `src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json`
  - Migrate to new schema:
    - Add `queues` array
    - Move routes from `transitions` to embedded in states/gateways
    - Remove `lanes` top-level (keep only in legacy Metadata if needed)
    - Remove `source` field from gateways
  - **Quality gate:** `workflow-authoring-live-seed-contract` skill demands the live authored seed be valid for real API load

---

## 4. Risky Edge Cases & Mitigation

### 4.1 Backward Compatibility Risk
**Edge Case:** Old seeds with `lanes` + `transitions` + `laneKey` still exist; old fixtures reference them.

**Mitigation:**
- Keep `WorkflowDefinitionMetadata` for legacy payloads
- Add `LoadAsync()` converter: detect old format, convert to new, log migration
- Quality gate: Both old and new formats must roundtrip successfully
- **Test:** `LegacyWorkflowMigrationTests` covering all old payment-demo variants

### 4.2 Route Graph Integrity
**Edge Case:** A gateway's route points to a non-existent state/gateway; routing engine crashes or hangs.

**Mitigation:**
- Validation rule: every route target must resolve in the definition
- Runtime: add defensive checks before routing (throw clear error if target missing)
- Quality gate: cyclic path detection (DFS) to prevent infinite loops
- **Test:** `WorkflowRouteGraphValidationTests`

### 4.3 Join Gateway Required Incoming Queues
**Edge Case:** Join gateway lists `requiredIncomingQueues: ["web-user", "business-user"]` but a route from `business-user` state points directly to a terminal state, bypassing the join.

**Mitigation:**
- Validation rule: if a queue has a route to a join gateway, verify all queues in `requiredIncomingQueues` have reachable routes to the join
- Document expected behavior: join waits for paths from ALL required queues to arrive
- Quality gate: payment-demo's join validates that both queues contribute
- **Test:** `JoinGatewayRequiredQueuesTests`

### 4.4 Initial State Accidentally Set to Gateway
**Edge Case:** `initialState: "await-payment-confirmation"` (a gateway, not a state).

**Mitigation:**
- Validation rule: `initialState` must resolve to a state key, not a gateway
- Quality gate test catches this immediately
- **Test:** `InitialStateValidationTests`

### 4.5 State Orphaned (No Incoming Routes)
**Edge Case:** A state has no incoming routes (unreachable except as initial state); workflow stalls if instance lands in wrong queue.

**Mitigation:**
- Validation warning: highlight unreachable states (not fatal, but warn)
- Document expected: every non-initial state should have at least one incoming route
- Quality gate: payment-demo has no orphans
- **Test:** `UnreachableStateWarningTests`

### 4.6 Routes Array Empty or Null
**Edge Case:** A state is terminal but has `routes: []` (correct) vs. `routes: null` (ambiguous).

**Mitigation:**
- Standardize: terminal states can omit `routes` or have `routes: []`
- Runtime treats both as terminal
- Serialization: omit empty routes to keep JSON clean
- **Test:** `TerminalStateRoutingTests`

### 4.7 Queue Key Typos or Refactoring
**Edge Case:** During refactor, `queueKey` changed from `"web-user"` to `"web"` in one state but not others.

**Mitigation:**
- Validation rule: all states/gateways with routes to each other must share queue context or be explicitly crossing queues
- Quality gate: payment-demo hard-codes queue keys—catch typos early
- **Test:** `QueueKeyConsistencyTests`

### 4.8 Legacy Authored Workflow with Mixed Schema
**Edge Case:** An authored workflow has both `transitions` array AND embedded `routes` in gateways (migration half-applied).

**Mitigation:**
- Converter: if both exist, prefer embedded routes; log warning about mixed schema
- Quality gate: live authored seed must be canonical (no mixing)
- **Test:** `MixedSchemaRejectionTests`

---

## 5. Testing Strategy & Quality Gates

### Blathers (Runtime)

1. **WorkflowDefinitionValidationTests**
   - Every state must have `queueKey` (not null)
   - Every gateway must have `queueKey` (not null)
   - `initialState` must be a state, not a gateway
   - All route targets must exist
   - No cycles in route graph
   - Join gateway must have `requiredIncomingQueues`

2. **PaymentWorkflowSeedRoundtripTests**
   - Load `payment-demo.json`
   - Assert structure: `queues`, `states` with `routes`, `gateways` with `routes`
   - Assert `web-user` queue contains enter-details, await-payment-confirmation, payment-complete
   - Assert `business-user` queue contains confirm-payment-received
   - Assert no `transitions` array
   - Assert no `lanes` at top level
   - Assert join gateway has two required queues
   - Assert runtime engine can traverse all paths

3. **WorkflowRuntimeEngineRoutingTests**
   - Payment demo: applicant enters details → split gateway → two paths (waiting + back-office)
   - Back-office confirms → routes to join gateway
   - Join releases → payment-complete (terminal)

### Isabelle (Editor)

1. **WorkflowProjectorTests**
   - Input: `AuthoredWorkflow` with queues/states/gateways (new model)
   - Output: `WorkflowDefinitionFile` matching new schema
   - Assert embedded routes are preserved
   - Assert no `source` field on gateways (unnecessary)

2. **CanvasRenderingTests** (`workflow-parallel-queues.spec.ts`)
   - Render payment workflow
   - Assert swim lanes labeled by queue name (not lane name)
   - Assert states have outbound edges to gateways
   - Assert gateways have outbound edges to states/gateways
   - Assert no direct state-to-state edges

3. **LiveAuthoredSeedTests**
   - Load `workflow-authored/planning.workflow.json`
   - Assert structure matches new schema
   - Assert authoring API can load it successfully
   - Assert editor can open and display it

---

## 6. Migration Timeline

### Phase 1: Model Definition & Backward Compatibility (Blathers)
- Update `WorkflowDefinitionFile.cs` schema
- Add migration converter for legacy seeds
- Add validation rules
- Ensure payment-demo roundtrips correctly

### Phase 2: Runtime Routing (Blathers)
- Update `WorkflowRuntimeEngine` to traverse embedded routes
- Test with payment demo instance flow
- Verify join gateway waits for both queues

### Phase 3: Editor Schema & Projection (Isabelle)
- Update `AuthoredWorkflow` to use queues
- Update projector to emit new schema
- Canvas rendering updated
- Live authored seed migrated

### Phase 4: Quality Gates & Validation (Both)
- Comprehensive validation tests
- Payment demo walkthrough (end-to-end)
- Cleanup legacy test fixtures

---

## 7. Success Criteria

✅ **Payment demo workflow loads, validates, and runs without errors**
✅ **All states have `queueKey` (required, not optional)**
✅ **All gateways have `queueKey` (required, not optional)**
✅ **Routes live on source nodes (states and gateways), not in flat `transitions` array**
✅ **No `lanes` at top level; `queues` is the new organizational unit**
✅ **Runtime engine routes correctly through split/join gateways**
✅ **Backward compatibility: old seeds load and migrate automatically**
✅ **Editor canvas displays queues (not lanes) and embedded routes correctly**
✅ **All validation tests pass; no orphaned or unreachable states**

---

## 8. Open Questions for Clarification

- **Q1:** Should join gateway routes be allowed to specify the target state's queue, or always infer from definition?
  - *Recommendation:* Always infer (no ambiguity); validation ensures consistency.

- **Q2:** If a state has multiple outbound routes with the same trigger, how does runtime choose?
  - *Recommendation:* Order matters (first route matching conditions wins); validation warns if ambiguous.

- **Q3:** Can a route cross queues (e.g., state in web-user queue routes directly to state in business-user queue)?
  - *Recommendation:* No—all cross-queue handoffs must go through a gateway (enforced by validation).

- **Q4:** Should legacy `transitions` array be preserved in seeds for compatibility, or removed entirely?
  - *Recommendation:* Remove on first load; converted to embedded routes; legacy Metadata persists for reference only.

---

## 9. Implementation Checklist

### Blathers
- [ ] Update `WorkflowDefinitionFile.cs`: Add Queues, rename LaneKey→QueueKey, remove Transitions
- [ ] Create migration converter for legacy seeds
- [ ] Add comprehensive validation rules
- [ ] Update `WorkflowRuntimeEngine` routing logic
- [ ] Payment demo seed roundtrip test passes
- [ ] All validation tests pass

### Isabelle
- [ ] Update `AuthoredWorkflow` schema
- [ ] Update `WorkflowProjector` to emit new contract
- [ ] Canvas rendering updated (queues, embedded routes)
- [ ] Live authored seed migrated
- [ ] Editor tests pass
- [ ] Payment demo editor walkthrough passes

### Tom Nook (Oversight)
- [ ] Code review: both Blathers and Isabelle implementations
- [ ] Validate payment demo end-to-end
- [ ] Merge decisions into `.squad/decisions.md`

---


---

## 2026-06-06T10:27:40.932+01:00 — Payment join cleanup

**Author:** Blathers

- The payment demo no longer needs the extra `confirm-payment-route` split gateway.
- `confirm-payment-received` now routes straight to the `await-payment-confirmation` join gateway, which preserves the same split/join behaviour with less graph noise.
- Backend regression coverage now proves three things together: the simplified payment workflow still publishes cleanly, validation accepts direct stage→join routing, and the MockBusinessApp source API can save and reload the simplified payment definition.

---

### 2026-06-06T10:27:40.932+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Remove implementation-detail graph copy and unnecessary split/join labels when the visual form already communicates the behaviour; keep the payment workflow as a clean demo and simplify away any unnecessary gateway.
**Why:** User request — captured for team memory

---

## 2026-06-06T10:27:40.932+01:00 — Graph cleanup and payment demo simplification

**Author:** Isabelle

- The payment demo graph now uses a direct `confirm-payment-received -> await-payment-confirmation` route, so the extra `confirm-payment-route` split gateway is removed from the client fixture and MockBusinessApp seed.
- Gateway chrome on the graph should stay product-facing: remove visual Split/Join badges and "related routes" meta from the canvas, while keeping gateway type in accessible names and the inspector.
- Route chips should avoid node collisions, and single-route pills should size to their trigger text instead of truncating it.
- MockBusinessApp client saves should send canonical workflow JSON (`serializeAuthoredWorkflow`) so the live editor posts the persisted workflow contract rather than the hydrated in-memory shape.

---

## 2026-06-06T10:27:40.932+01:00 — Tangy graph cleanup regression checks

**Author:** Tangy

### Decision

Treat the current payment-demo cleanup as four separate regression contracts:

1. **Save behaviour:** a validation-clean payment demo must keep Save enabled and confirm a successful save, while existing validation tests continue to prove blocking errors disable Save.
2. **Graph readability:** the payment demo canvas must keep node text readable and node boxes non-overlapping.
3. **Graph copy cleanup:** once Isabelle removes implementation-detail canvas copy, the payment graph should stop rendering gateway-kind badges and "related route(s)" meta copy.
4. **Payment route shape:** the core authoring tests now prove the flattened payment contract uses a direct `confirm-payment-received -> await-payment-confirmation` route with no extra confirmation gateway, and the shell still carries a pending canvas contract so the editor fixture/rendering drops any leftover `confirm-payment-route` node.

### Why

- Jonny's directive asks for cleaner graph copy and a simpler payment demo, so Tangy's checks should separate what already works from what is still pending.
- Keeping the copy-cleanup and shell-side route-shape checks as pending behavioural contracts lets Isabelle finish the graph cleanup without losing the target outcome, while the backend contract stays locked by the new passing authoring test.

---

## 2026-06-06T11:06:20.868+01:00: User directive

**By:** Jonny Muir (via Copilot)

**What:** Fix the save error and replace the current flashing error behaviour with a more standard, safer error-reporting approach that users can read and copy reliably.

**Why:** User request — captured for team memory

---

## 2026-06-06T11:06:20.868+01:00 — Workflow save errors use structured Problem Details

**Author:** Blathers

- The MockBusinessApp workflow save endpoint now validates nested workflow components before deserializing the payload.
- Save failures return `application/problem+json` with a stable `errorCode`, `traceId`, and per-error `code` / `message` / `path` entries instead of leaking raw serializer exceptions or stack traces.
- Missing or unsupported component `type` discriminators are reported as a client-safe `workflow-component-invalid` problem so Isabelle can map save failures onto a durable editor error surface without depending on server exception text.

---

## 2026-06-06T11:06:20.868+01:00 — Stable save errors in the workflow editor

**Author:** Isabelle

- Save failures in `prism-workflow-editor` now stay visible in a persistent inline error surface instead of disappearing in a toast.
- The error surface shows a short title, a user-safe summary, optional structured detail lines, and an optional reference id, with a copyable text area plus a copy button for support/debugging.
- Host save adapters should throw structured save errors when they can (`title`, `summary`, `detailLines`, `traceId`); the editor sanitises fallback errors so stack traces and exception dumps are not shown to authors.
- A successful retry clears the save error surface and returns the editor to the normal saved state.

---

## 2026-06-06T11:06:20.868+01:00 — Tangy save error regression coverage

**Author:** Tangy

- Save error coverage now uses focused Playwright contracts for four user-facing outcomes: successful save, structured save failure, persistent/copyable error reporting, and recovery after retry.
- The failure fixtures deliberately include stack-trace-shaped noise so the tests prove authors only see sanitised copy plus the support reference id.
- These checks stay at the workflow editor boundary by swapping the host `workflowSource`, which keeps the regression signal on host/editor save behaviour instead of implementation details.

---

## 2026-06-06 — AllowOutOfOrderMetadataProperties on mockWorkflowJsonOptions

**Author:** Blathers (Backend Dev)  
**Status:** Accepted

### Context

`workflow-canonical-json.ts`'s `sortKeys()` function sorts all object keys alphabetically before serialising. This moves the `type` discriminator away from first position on `AuthoredComponent` objects (e.g. `body` → `{ content: ..., type: "body" }`). The server's `PrismComponent` uses `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]`, which by default requires the discriminator to be the **first** property. When it isn't, .NET throws `NotSupportedException`, caught and returned as "Invalid workflow payload".

### Decision

Set `AllowOutOfOrderMetadataProperties = true` on the `JsonSerializerOptions` instance (`mockWorkflowJsonOptions`) used by the `/mockapp/workflows/{key}` PUT endpoint in `MockBusinessApp/Program.cs`.

**Note:** This property is on `JsonSerializerOptions`, not on `JsonPolymorphicAttribute`. The attribute has no such property in .NET 10.0.

### Alternatives Considered

- **Sorting `type` first in the TS serialiser** — fragile, requires ongoing maintenance if new properties are added.
- **Custom `JsonConverter`** — heavier than necessary; `AllowOutOfOrderMetadataProperties` is the idiomatic solution.

### Consequences

- The deserialiser buffers each component object in memory before committing to a strategy (minor memory overhead on large payloads — acceptable for an authoring-time API).
- Any future `JsonSerializerOptions` instances used to deserialise `WorkflowDefinitionFile` must also set this flag if they handle polymorphic components.
- The frontend `sortKeys()` behaviour is left unchanged; the fix is purely server-side.

### Files Changed

- `src/UmbracoPrism.MockBusinessApp/Program.cs` — added `AllowOutOfOrderMetadataProperties = true` to `mockWorkflowJsonOptions`

---

## 2026-06-06 — Workflow Editor Save Error Dismiss + Y-Axis Layout Algorithm

**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Branch:** fix/workflow-editor-save-and-layout  
**Commit:** 901fa79

### Issue 2: Dismiss button on save error banner

**Decision:** Added a "Dismiss" button to `_renderSaveErrorSurface()` in `prism-workflow-editor.ts` that clears both `_saveError` and `_saveErrorCopyStatus` when clicked. The button carries `aria-label="Dismiss save error"` for screen-reader accessibility and uses the same `toolbar-btn govuk-button govuk-button--secondary` class as the adjacent "Copy details" button for visual consistency. A `data-prism-dismiss-save-error` attribute is included for test selectors.

**Rationale:** The persistent error surface had no exit path for the user — once a save error appeared, it could only be cleared by a successful save. A dismiss action is required so users can acknowledge and clear the error without being forced to retry.

### Issue 3: Y-axis layout algorithm — longest-path replaces parity-stepped Kahn

**Decision:** Replaced the parity-stepped Kahn topological sort in `prism-workflow-graph.ts` with a standard longest-path algorithm. All nodes start at rank 0; each node's rank is propagated as `max(currentRank, parentRank + 1)` with a uniform step of 1. The post-sort parity-adjustment block (which bumped nodes to even/odd ranks based on kind) has been removed entirely.

**Root cause fixed:** The parity adjustment was designed to enforce stage → gateway → stage reading order, but it could assign rank 0 to cross-lane nodes that should have inherited a higher rank from upstream nodes in a different lane. The symptom was `payment-complete` rendering at the top of its lane despite being downstream of several stages.

**Downstream compatibility:** The `rowRank` field on `StageLayout` and `GatewayLayout` objects is now set to the longest-path rank value. The `data-prism-row-rank` attribute (used only for debugging/testing) reflects this. The `_rowBandCenter(rowRank)` helper and all X-position logic are unchanged.

**Trade-off:** Gateways may now share the same row rank as adjacent stages (e.g., rank 1 rather than forced to rank 1 by parity). Visual separation between stages and gateways is maintained by the `ROW_BAND_PITCH` constant (152 px) relative to `NODE_HEIGHT` (128 px), not by row rank parity.

---

## 2026-06-06 — Validation: workflow-editor-save-and-layout fixes

**Author:** Tangy (Tester & Validation Lead)  
**Verdict:** ✅ APPROVED

### Fix 1 — JSON polymorphism discriminator order (Blathers)

`AllowOutOfOrderMetadataProperties = true` is correctly applied to the `mockWorkflowJsonOptions` instance in `UmbracoPrism.MockBusinessApp/Program.cs`. This options object is used for all three workflow endpoints (list, load, save PUT). The fix is in the right place.

**Coverage gap:** No Storybook-level Playwright test can cover backend JSON deserialization. An API-level test (sending a PUT with `type` not first) would require the live MockBusinessApp to be running — this is excluded from the current Playwright baseline by infrastructure.

### Fix 2 — Save error dismiss button (Isabelle)

Dismiss button confirmed in `prism-workflow-editor.ts` `_renderSaveErrorSurface()`:
- `aria-label="Dismiss save error"` ✅
- `data-prism-dismiss-save-error` ✅
- Click handler: `this._saveError = null; this._saveErrorCopyStatus = null;` ✅

**New test added:** `workflow-editor-validation.spec.ts` — "dismiss button removes the save error surface without needing a retry". Verifies `[data-prism-save-error]` disappears and `[data-prism-save-error-copy-status]` is gone after click. 7/7 tests pass in the validation spec.

### Fix 3 — Y-axis layout algorithm (Isabelle)

Confirmed in `prism-workflow-graph.ts` lines 472–504: parity-stepped Kahn sort replaced with longest-path algorithm. The `ranks.set(nextId, Math.max(..., currentRank + 1))` update guarantees cross-lane nodes get correct ranks. No residual parity (`% 2`) code in the rank assignment path.

**Coverage gap:** Cross-lane node Y-position tests require a running app for pixel-accurate assertions. The existing `graph-layout-proof.spec.ts` is a live-app test and in the known-failing baseline. A visual regression test is the appropriate long-term contract here, but requires infrastructure.

### Baseline failures (pre-existing, not regressions)

20 Playwright tests fail in the full run — all confirmed pre-existing:
- Walkthrough tests (8) and four-workflow-contract (1): require the live MockBusinessApp to be running.
- `add-route-affordance` (b/c/d/e): confirmed failing on the baseline branch before these fixes were applied.
- Other editor tests: confirmed pre-existing failures unrelated to these three fixes.

The 137 passing tests are unaffected by the branch changes.

- CI will use same baselines as local development
- No platform-specific maintenance burden
- Visual tests remain behavioral (UI layout contract), not implementation mirrors
---
author: tangy
date: 2026-05-25T09:32:35.455+01:00
status: proposed
area: workflow-testing
---

# Decision: Slice concurrent-lane proof into editor, showcase, and live-walkthrough tracks

## Context

The current behavioural coverage proves four showcase workflows, editor shell switching, one branch simulation, and several straight-line walkthroughs. It does not yet give a clean migration path for the move from linear waiting stages to concurrent lanes with join gateways.

If we change all of that in one step, we risk losing the green behavioural gate and losing the simple showcase stories that currently make the product easy to demo.

## Decision

Track the redesign in three linked slices:

1. **Editor behavioural contract** — prove that authors can see parallel lanes, understand join conditions, and trust simulation/validation.
2. **Showcase workflow evolution** — redesign the four showcase workflows so each one demonstrates a clear user-visible parallel-work story.
3. **Live walkthrough proof** — prove that public/member/admin journeys show honest progress before, during, and after the join.

Keep one simple straight-line workflow proof in place as a control until the concurrent slices are green.

## Consequences

- We can add concurrent coverage without breaking the existing four-workflow catalogue all at once.
- Demo clarity stays high because each issue is written in product language and tied to visible proof.
- The team gets an explicit rule for keeping the behavioural gate green during the transition: add the concurrent proof first, then retire linear-only assumptions.

---
author: tom-nook
date: 2026-05-25T09:32:35.455+01:00
status: proposed
area: concurrent-multi-lane-workflows
---

# Decision: sequence the concurrent multi-lane redesign as seven delivery slices

## Context

Jonny asked for the redundant workflow surface logic to be removed and for the rest of the transition redesign to be turned into a safe, ordered backlog. The redesign introduces lane-owned stages and gateways, replaces waiting stages with join gateways, and requires careful editor UX plus preserved behavioural proof across the four showcase workflows.

The open backlog did not already contain clean matches for these slices. The only open issues were #28 (biometric auth pen-test checklist), #63 (editor undo/redo), and #73 (AI proposal editing), so new issues were needed.

## Decision

Create and order the redesign as the following GitHub issues:

1. #81 — Clean up duplicate workflow surface rules before lane work
2. #82 — Let workflow stages and gateways belong to named lanes
3. #83 — Make lane transitions and gateways easy to read in the editor
4. #84 — Replace waiting stages with lane join gateways
5. #85 — Run parallel lanes safely without one lane overwriting another
6. #86 — Keep workflow history clear when people and systems act in parallel
7. #87 — Evolve the four showcase workflows and behavioural tests for lane-based flow

## Why this order

- Start with cleanup so the Umbraco-facing projection contract stays clean before engine changes begin.
- Lock the lane/gateway language next so editor and runtime work share the same model.
- Set the editor’s visual language before deeper behaviour changes so transitions and joins stay understandable.
- Land join gateway semantics before full concurrent execution so the waiting-story replacement is explicit.
- Add history clarity after the concurrent engine slice so behavioural proof matches real runtime behaviour.
- Finish by evolving the four showcase workflows and behavioural tests to prove the shipped story end to end.

## Guardrails

- Use plain product language in titles and bodies.
- Keep issues small enough to land one slice at a time.
- Keep behavioural tests green throughout the sequence.
- Avoid implementation-mirror framing; describe the user-visible intent and safety bar instead.

---
author: Tom Nook
date: 2026-05-25T11:48:05.065+01:00
status: implemented
area: workflow-assignment-contract
---

# Decision: Issue #81 workflow assignment contract cleanup

## Context

Issue #81 removes duplicate workflow surface rules before the concurrent-lane redesign. The working slice already replaced stored `editorSurface` hints with shared assignment derivation, updated preview and inspector language, and tightened behavioural tests around visible lane and assignment copy.

## Decision

- Treat `actor` and `roleGates` as the only authored source of truth for assignment and lane meaning.
- Remove `editorSurface` from the authored stage contract and strip any legacy value before preview, project, or publish requests.
- Keep the projected Umbraco-facing runtime contract clean: assignment data stays, editor-only surface metadata does not.
- Keep behavioural coverage pinned to author-visible outcomes (lane labels, assignment copy, validation jumps) rather than internal `front-stage` / `back-stage` plumbing.
- When a validation issue is opened from a non-canvas tab, return the author to Canvas before focusing the affected inspector target.

## Outcome

This cleanup preserves current linear workflow behaviour and the four showcase workflows while making later lane work safer. The lane presentation can now evolve without changing the authored payload or the runtime projection contract.

## References

- `.squad/decisions/inbox/isabelle-surface-cleanup.md`
- `.squad/decisions/inbox/tangy-issue-81-tests.md`
- `.squad/skills/workflow-assignment-source-of-truth/SKILL.md`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-stage-assignment.ts`
- `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts`
