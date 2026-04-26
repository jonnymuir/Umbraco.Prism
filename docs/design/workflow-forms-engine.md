# Prism Workflow Forms Engine — Architecture Design

> **⚠️ v2.0 Schema Update:** This document is being updated for v2.0. References to legacy v1 concepts (`fieldType`, `fields[]`, `FieldGroupBuilder`) are being migrated to v2.0 polymorphic components. See [walkthroughs/](../walkthroughs/) for current v2.0 examples.

**Author:** Tom Nook (Lead)  
**Requested by:** Jonny Muir  
**Status:** Authoritative Design  
**Date:** 2026-04-08  
**Version:** 1.0

---

## 1. Executive Summary

The Prism Workflow Forms Engine is a **demonstration framework** for workflow-driven forms where workflow configuration is the source of truth and each channel (web, mobile, backoffice) is only a renderer of workflow state.

### Scope Boundary Decision

This is **not** a production-grade BPM/low-code designer. This is a **framework + demo** feature where:

- **Prism provides:** The runtime execution contract, state machine semantics, tenant-isolated persistence, and reference archetypes.
- **Implementors provide:** Specific workflow definitions, business domain logic, custom field groups, and integrations.

The demo ships with one canonical example workflow (Information Request: Draft → Submitted → UnderReview → [Approved|Rejected]) to demonstrate the framework contract in action.

---

## 2. Core Principles

1. **Workflow definition is authoritative.** UI never decides process order, eligibility, or completion rules; it requests the next interaction from the workflow runtime.

2. **Tenant isolation is non-negotiable.** All workflow instances are scoped by `TenantId` using the established Prism pattern (same as `prismDeviceCredentials`).

3. **Contract-driven rendering.** Channels consume only the render payload contract; they never interpret the raw transition graph.

4. **Prism integration first.** The workflow runtime consumes `IPrismContext` for tenant/user resolution and uses the established NPoco migration pattern.

5. **Demo-first implementation.** Ship a working example, not a configurable low-code designer. Production-grade authoring UI is out of scope for v1.

---

## 3. Architectural Decisions (Open Questions Resolved)

### Decision 1: Storage Model — Hybrid NPoco + JSON Definitions

**Question:** Should workflow and field-group definitions be persisted in Umbraco content, dedicated NPoco tables, or hybrid storage?

**Decision:** **Hybrid storage with dedicated NPoco tables for instances/events + JSON file seeding for definitions.**

**Rationale:**
- Workflow instance state (live runtime data) requires transactional integrity, optimistic concurrency, and efficient querying → **NPoco tables**.
- Workflow definitions (versioned configuration) need import/export, version control, and easy seeding → **JSON fixtures + optional table storage**.
- Field-group definitions (reusable schema blocks) follow the same pattern as workflow definitions.
- Umbraco content storage is inappropriate because workflows are not content; they are system configuration.

**Implementation:**
- `prismWorkflowInstances` table: live state, current version, tenant/user/actor metadata.
- `prismWorkflowEvents` table: append-only audit stream for state changes, submissions, decisions.
- `prismWorkflowTasks` table: queueable work items for reviewer/approver/ops roles.
- JSON fixtures in `src/UmbracoPrism.MockBackOffice/Fixtures/workflows/` for seeded definitions.
- Optional future enhancement: store published workflow definitions in `prismWorkflowDefinitions` table for runtime versioning, but seed from JSON in v1.

---

### Decision 2: Actor Model — Role-Based Only for v1

**Question:** Which actor model should be canonical for approvals in v1: role-based only, user assignment, or both?

**Decision:** **Role-based only for v1. User assignment deferred to v2.**

**Rationale:**
- Role-based routing ("any user in `approvers` group can claim this task") is the minimal viable actor model and covers 80% of demo scenarios.
- User assignment ("this task is assigned to alice@example.com") requires claim/release semantics, reassignment logic, and escalation rules — all unnecessary complexity for a demo framework.
- The contract is extensible: `WorkflowTask` schema includes `AssignedToUserId` column (nullable), but v1 only populates `RequiredRole`.
- Demo workflow (Information Request) routes to `backoffice-reviewers` role; any user in that role can claim and decide.

**Implementation:**
- `prismWorkflowTasks.RequiredRole` (non-nullable string): Umbraco backoffice group alias.
- `prismWorkflowTasks.AssignedToUserId` (nullable string): reserved for v2; always NULL in v1.
- `WorkflowTaskQueue` API filters by `IPrismContext.User.IsInRole(RequiredRole)` and `TenantId`.

---

### Decision 3: Optimistic Concurrency — Required from Day One

**Question:** Do we need optimistic concurrency tokens on all submit/action endpoints from day one?

**Decision:** **Yes. `stateVersion` enforcement is required from day one on all mutating endpoints.**

**Rationale:**
- Concurrent submissions by the same user (e.g., double-click, mobile app retry, multi-device) are realistic even in demo scenarios.
- Adding concurrency control retroactively is a breaking API change and requires client-side rework.
- Implementation cost is minimal: one `stateVersion` integer column + one validation check before state transitions.
- HTTP `409 Conflict` with `stateVersion` mismatch is a clear, recoverable error for clients.

**Implementation:**
- `prismWorkflowInstances.StateVersion` (integer, default 1, increments on every state change).
- All `POST /submit/{fieldGroupKey}` and `POST /actions/{actionKey}` require `stateVersion` in payload.
- Validation: `if (submitted != current) return 409 Conflict`.
- Response payload always includes current `stateVersion` for next request.

---

### Decision 4: Timeline/Audit — Strictly Transactional

**Question:** Should timeline/audit be eventually consistent or strictly transactional with state transitions?

**Decision:** **Strictly transactional. Audit events are written in the same transaction as state changes.**

**Rationale:**
- Demo/framework use case does not have the scale requirements that justify eventual consistency complexity.
- Event-sourced audit requires append-only guarantees: state transitions and audit events MUST succeed or fail together.
- Querying timeline/audit is a read-heavy operation; eventual consistency would require complex client-side handling of "incomplete" timelines.
- NPoco supports transactional writes; use them.

**Implementation:**
- Single database transaction for: (1) update `prismWorkflowInstances.CurrentState` + increment `StateVersion`, (2) append to `prismWorkflowEvents`, (3) insert/update `prismWorkflowTasks` if applicable.
- No async audit queue in v1.
- If performance bottlenecks emerge in production use, implementors can add async audit projection separately.

---

### Decision 5: Minimum Accessibility Criteria — WCAG 2.1 AA Baseline

**Question:** What minimum accessibility criteria should every archetype meet before demo sign-off?

**Decision:** **WCAG 2.1 Level AA compliance for all shipped archetypes (Collect, Review, TaskQueue, Decision, RequestChanges, StatusTimeline, Completion).**

**Acceptance Criteria:**
- Keyboard navigation: all interactive elements reachable and operable via keyboard only (tab order, Enter/Space activation, Escape dismissal).
- Screen reader support: semantic HTML, ARIA labels/descriptions where needed, live region announcements for state changes.
- Color contrast: 4.5:1 for body text, 3:1 for large text and UI components (aligned with GDS Design System standards).
- Focus indicators: visible focus ring on all interactive elements (not suppressed by CSS).
- Error identification: validation errors associated with specific fields via `aria-describedby`.
- Form labels: all inputs have associated `<label>` or `aria-label`.

**Testing:**
- Playwright accessibility tests using `axe-core` for automated checks.
- Manual keyboard-only testing for each archetype.
- Manual screen reader spot-check (VoiceOver on macOS or NVDA on Windows) for critical flows.

**Defer to v2:**
- AAA compliance, advanced cognitive accessibility (reading level), internationalization (RTL languages).

---

## 4. Prism Integration Points

### 4.1 Tenant Isolation Pattern

ALL workflow instances MUST be scoped by `TenantId`. This follows the established pattern from `prismDeviceCredentials`.

**Schema:**
- `prismWorkflowInstances.TenantId` (string, non-nullable, indexed)
- `prismWorkflowTasks.TenantId` (string, non-nullable, indexed)
- `prismWorkflowEvents.TenantId` (string, non-nullable, indexed)

**Enforcement:**
- All queries filter by `TenantId` from `IPrismContext.CurrentTenant.Id`.
- All creates/updates populate `TenantId` from `IPrismContext.CurrentTenant.Id`.
- No cross-tenant visibility, even for admins (use tenant-switching if needed).

**Migration:**
- Add foreign key constraints to `prismTenants.id` if referential integrity is desired (deferred to production hardening).

---

### 4.2 PrismContext Integration

Workflow runtime services consume `IPrismContext` (scoped per HTTP request) for tenant and user resolution.

**Pattern:**
```csharp
public class WorkflowInstanceService
{
    private readonly IPrismContext _prismContext;
    private readonly IDatabase _database;

    public WorkflowInstanceService(IPrismContext prismContext, IDatabase database)
    {
        _prismContext = prismContext;
        _database = database;
    }

    public async Task<WorkflowInstance> CreateInstanceAsync(string workflowKey)
    {
        var tenant = _prismContext.CurrentTenant 
            ?? throw new InvalidOperationException("No tenant context");
        
        var instance = new WorkflowInstance
        {
            TenantId = tenant.Id,
            WorkflowKey = workflowKey,
            CreatedByUserId = _prismContext.User?.FindFirstValue("oid"),
            // ...
        };

        _database.Insert(instance);
        return instance;
    }
}
```

**User Context:**
- Use `_prismContext.User.FindFirstValue("oid")` for Entra Object ID (consistent with existing Prism auth patterns).
- Use `_prismContext.User.IsInRole("backoffice-reviewers")` for role-based task filtering.

---

### 4.3 Database Migration Pattern

The NPoco migration pattern is established in `PrismMigrationPlan`. Use `AsyncMigrationBase`, NOT EF Core.

**Migration Plan Extension:**
```csharp
// In PrismMigrationPlan.DefinePlan():
.To<CreatePrismWorkflowInstancesTable>("add-workflow-instances")
.To<CreatePrismWorkflowEventsTable>("add-workflow-events")
.To<CreatePrismWorkflowTasksTable>("add-workflow-tasks");
```

**Migration Example:**
```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;

public class CreatePrismWorkflowInstancesTable : MigrationBase
{
    public CreatePrismWorkflowInstancesTable(IMigrationContext context)
        : base(context) { }

    protected override void Migrate()
    {
        if (TableExists("prismWorkflowInstances"))
            return;

        Create.Table<PrismWorkflowInstanceSchema>().Do();
    }
}
```

**Schema Annotation:**
```csharp
[TableName("prismWorkflowInstances")]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public class PrismWorkflowInstanceSchema
{
    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("InstanceId")]
    [Length(64)]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_prismWorkflowInstances_InstanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    [Index(IndexTypes.NonClustered, Name = "IX_prismWorkflowInstances_TenantId")]
    public string TenantId { get; set; } = string.Empty;

    [Column("WorkflowKey")]
    [Length(100)]
    public string WorkflowKey { get; set; } = string.Empty;

    [Column("WorkflowVersion")]
    public int WorkflowVersion { get; set; }

    [Column("CurrentState")]
    [Length(100)]
    public string CurrentState { get; set; } = string.Empty;

    [Column("StateVersion")]
    [Constraint(Default = "1")]
    public int StateVersion { get; set; }

    [Column("CreatedByUserId")]
    [Length(450)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? CreatedByUserId { get; set; }

    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime UpdatedAt { get; set; }

    [Column("CompletedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? CompletedAt { get; set; }

    [Column("Outcome")]
    [Length(100)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Outcome { get; set; }

    [Column("Metadata")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Metadata { get; set; } // JSON
}
```

---

### 4.4 MockBackOffice Integration

The existing `UmbracoPrism.MockBackOffice` has an established API surface. Extend it; do NOT duplicate.

**Namespace Convention:**
- New endpoints: `/api/backoffice/workflows/*` (matches existing `/api/backoffice/tenants/*` pattern).
- Keep emulator-only extensions clearly namespaced to avoid leaking into production-intended contracts.

**Seeding Pattern:**
- Add JSON fixture loader in `MockBackOfficeComposer` (same pattern as tenant seeding).
- Fixtures in `src/UmbracoPrism.MockBackOffice/Fixtures/workflows/information-request.json`.

**Execution Mode:**
- `RuntimeMode = Emulator` (default): MockBackOffice evaluates transitions locally for deterministic demo.
- `RuntimeMode = Core` (optional): MockBackOffice routes through actual Core runtime endpoints for fidelity testing.

---

### 4.5 GDS Design System Aesthetic

The spec mentions "GDS design system type stuff" (UK Government Digital Service). This means:

- **Progressive disclosure:** Show users only what they need at each step; don't overwhelm with the full form upfront.
- **Clear, plain language:** Avoid jargon; use active voice; write for a reading age of 9.
- **One thing per page:** Each archetype renders one conceptual task (collect one section, make one decision).
- **Accessible by default:** WCAG 2.1 AA baseline (see Decision 5).
- **Mobile-first responsive:** Touch targets ≥44×44px, legible text at default zoom, no horizontal scroll.

**Archetype Design Guidance:**
- `Collect`: Single-column form layout, labels above inputs, validation summary at top, clear primary action.
- `Review`: Read-only data grouped logically, "Change" links next to editable sections, single confirmation action.
- `TaskQueue`: Sortable table with clear status badges, pagination, filters in sidebar.
- `Decision`: Decision options as large radio buttons or buttons, mandatory reason text area, clear approve/reject language.
- `StatusTimeline`: Vertical timeline with datetime, actor, and event description; most recent at top.
- `Completion`: Success/failure banner, plain summary, next-step actions or download links.

**Umbraco Backoffice Considerations:**
- When rendered in Umbraco v17 backoffice extensions, adapt to UUI component aesthetic using hybrid adapter pattern (Decision from Section 3.1).
- When rendered in standalone web/mobile, use Prism-generic web components with GDS-inspired styling.

---

## 5. Data Model

### 5.1 Conceptual Entities

1. **WorkflowDefinition** (JSON fixture in v1, optional table in v2)
   - `workflowKey` (string, unique): Canonical identifier (e.g., `information-request`).
   - `version` (integer): Semantic version for breaking changes.
   - `status` (enum): `Draft`, `Published`, `Retired`.
   - `states` (array): State names, archetypes, field-group bindings.
   - `transitions` (array): From/to state, action key, guard references.
   - `metadata` (JSON): Title, description, owner.

2. **FieldGroupDefinition** (JSON fixture in v1, optional table in v2)
   - `fieldGroupKey` (string, unique): Canonical identifier (e.g., `personal-details`).
   - `version` (integer): Schema version.
   - `schema` (JSON Schema): Field types, constraints, visibility rules.
   - `validation` (array): Server-side validation rules.
   - `metadata` (JSON): Title, description, layout hints.

3. **WorkflowInstance** (NPoco table: `prismWorkflowInstances`)
   - `instanceId` (string, UUID): Globally unique instance identifier.
   - `tenantId` (string): Tenant isolation.
   - `workflowKey` + `workflowVersion` (string + int): Which definition this runs.
   - `currentState` (string): Current state name.
   - `stateVersion` (int): Optimistic concurrency token.
   - `createdByUserId`, `createdAt`, `updatedAt`, `completedAt`, `outcome`.
   - `metadata` (JSON): Instance-specific data (e.g., form title, external reference ID).

4. **WorkflowEvent** (NPoco table: `prismWorkflowEvents`)
   - `eventId` (int, auto-increment): Unique event ID.
   - `instanceId` (string): Foreign key to workflow instance.
   - `tenantId` (string): Tenant isolation.
   - `eventType` (string): `StateChanged`, `FieldGroupSubmitted`, `ActionTaken`, `TaskCreated`, `TaskClaimed`, `TaskCompleted`.
   - `fromState`, `toState` (nullable strings): State transition.
   - `actorUserId` (string): Who performed the action.
   - `timestamp` (datetime): When it happened.
   - `payload` (JSON): Event-specific data (e.g., submitted field values, decision rationale).

5. **WorkflowTask** (NPoco table: `prismWorkflowTasks`)
   - `taskId` (int, auto-increment): Unique task ID.
   - `instanceId` (string): Foreign key to workflow instance.
   - `tenantId` (string): Tenant isolation.
   - `requiredRole` (string): Umbraco backoffice group alias (e.g., `backoffice-reviewers`).
   - `assignedToUserId` (nullable string): Reserved for v2; always NULL in v1.
   - `status` (string): `Pending`, `Claimed`, `Completed`, `Cancelled`.
   - `claimedByUserId` (nullable string): Who claimed this task.
   - `claimedAt` (nullable datetime): When claimed.
   - `completedAt` (nullable datetime): When completed.
   - `createdAt` (datetime): When task was created.

### 5.2 Schema Files

See Section 4.3 for `PrismWorkflowInstanceSchema`. Additional schema classes:

- `PrismWorkflowEventSchema.cs`: Append-only event table.
- `PrismWorkflowTaskSchema.cs`: Task queue table.

---

## 6. Runtime Contracts

All workflow runtime endpoints are under `/umbraco/prism/workflows/*`. These are **production-intended contracts** (not emulator-only).

### 6.1 Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/umbraco/prism/workflows/instances` | Create new workflow instance |
| `GET` | `/umbraco/prism/workflows/instances/{instanceId}/render` | Get current render payload |
| `POST` | `/umbraco/prism/workflows/instances/{instanceId}/submit/{fieldGroupKey}` | Submit field group data |
| `POST` | `/umbraco/prism/workflows/instances/{instanceId}/actions/{actionKey}` | Execute workflow action/transition |
| `GET` | `/umbraco/prism/workflows/instances/{instanceId}/timeline` | Get audit timeline |
| `GET` | `/umbraco/prism/workflows/tasks` | Get tasks for current user |
| `POST` | `/umbraco/prism/workflows/tasks/{taskId}/claim` | Claim a task |
| `POST` | `/umbraco/prism/workflows/tasks/{taskId}/complete` | Complete a claimed task |

### 6.2 Response Envelope (Consistent Across All Endpoints)

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
    "currentState": "Draft",
    "fieldGroups": [
      {
        "fieldGroupKey": "personal-details",
        "version": 1,
        "title": "Personal Details",
        "required": true,
        "schema": { /* JSON Schema */ },
        "submittedData": { /* previously submitted values, if any */ }
      }
    ],
    "availableActions": [
      {
        "actionKey": "save-draft",
        "label": "Save Draft",
        "confirmationMessage": null
      },
      {
        "actionKey": "submit",
        "label": "Submit for Review",
        "confirmationMessage": "Are you sure you want to submit?"
      }
    ]
  },
  "problems": []
}
```

### 6.3 Response States

| State | Meaning | HTTP Status | Client Behavior |
|-------|---------|-------------|-----------------|
| `ask_now` | Backend has one or more items/questions to render immediately | `200 OK` | Render payload and await user action |
| `wait` | Instance is valid but temporarily not ready (async guard, queue, reviewer decision pending) | `202 Accepted` | Show waiting state; poll after `pollAfterMs` |
| `complete` | Workflow reached terminal outcome | `200 OK` | Show completion payload; stop polling |
| `error` | Non-happy-path result with typed failures in `problems` | Varies (see Section 6.4) | Branch by problem type |

### 6.4 HTTP Status Mapping

| Scenario | HTTP Status | `responseState` |
|----------|-------------|-----------------|
| More UI items/questions to ask now | `200 OK` | `ask_now` |
| Backend not ready yet, ask later | `202 Accepted` | `wait` |
| Complete | `200 OK` | `complete` |
| Validation failure | `422 Unprocessable Entity` | `error` |
| Authentication missing/invalid | `401 Unauthorized` | `error` |
| Authorization denied | `403 Forbidden` | `error` |
| Optimistic concurrency or state mismatch | `409 Conflict` | `error` |
| Instance not found/hidden | `404 Not Found` | `error` |
| Transient infrastructure fault | `503 Service Unavailable` | `error` |
| Unhandled server failure | `500 Internal Server Error` | `error` |

---

## 7. Interaction Archetypes

Archetypes are renderer primitives that map to workflow state intent, not specific business domains.

### 7.1 Archetype Catalog

1. **Collect**
   - **Purpose:** Gather user input.
   - **Components:** Form sections, validation summary, save-draft button, submit button.
   - **Example State:** `Draft`, `NeedsChanges` (after request-changes action).

2. **Review**
   - **Purpose:** Read-only confirmation before a transition.
   - **Components:** Grouped answers, change links, submit action.
   - **Example State:** `ReadyToSubmit` (after all required field groups collected).

3. **TaskQueue**
   - **Purpose:** Present pending workflow tasks for operators.
   - **Components:** Sortable table, filters (tenant, state, SLA), assignment status, claim button.
   - **Example State:** Not tied to specific instance state; this is a cross-instance view for reviewers.

4. **Decision**
   - **Purpose:** Approve/reject/request-changes with reason capture.
   - **Components:** Decision buttons (large, radio-style), decision rationale text area, policy hints.
   - **Example State:** `UnderReview` (claimed by reviewer).

5. **RequestChanges**
   - **Purpose:** Route instance back with targeted remediation.
   - **Components:** Required correction items (checklist), due date, notes.
   - **Example State:** `NeedsChanges` (after reviewer requests changes).

6. **StatusTimeline**
   - **Purpose:** Visualize instance progress and audit events.
   - **Components:** Vertical timeline, actor history, timestamps, event descriptions.
   - **Example State:** Any state; timeline is a supplementary view.

7. **Completion**
   - **Purpose:** Final outcome with next-step guidance.
   - **Components:** Success/failure banner, summary, downloadable receipt, follow-up actions.
   - **Example State:** `Approved`, `Rejected` (terminal states).

### 7.2 Renderer Mapping Example (Information Request Workflow)

| State | Archetype | Available Actions |
|-------|-----------|-------------------|
| `Draft` | `Collect` | `save-draft`, `submit` |
| `Submitted` | `StatusTimeline` | (none; waiting for reviewer) |
| `UnderReview` | `Decision` (for reviewer) | `approve`, `reject`, `request-changes` |
| `UnderReview` | `StatusTimeline` (for submitter) | (none; read-only view) |
| `NeedsChanges` | `RequestChanges` + `Collect` | `resubmit` (after corrections) |
| `Approved` | `Completion` | `download-summary` |
| `Rejected` | `Completion` | `appeal` (optional) |

---

## 8. Field-Group Model and Versioning

### 8.1 Field-Group Structure

Field groups are reusable, versioned schema blocks that can be mounted in multiple workflows.

**Properties:**
- `fieldGroupKey` (string): Canonical identifier (e.g., `personal-details`, `financial-info`).
- `version` (integer): Schema version (increment on breaking changes).
- `schema` (JSON Schema): Field types, constraints, conditional visibility rules.
- `validation` (array): Server-side validation rules (format, range, custom).
- `metadata` (JSON): Title, description, layout hints (density, order).

**Example JSON:**
```json
{
  "fieldGroupKey": "personal-details",
  "version": 1,
  "metadata": {
    "title": "Personal Details",
    "description": "Your name and contact information"
  },
  "schema": {
    "type": "object",
    "properties": {
      "fullName": {
        "type": "string",
        "minLength": 1,
        "maxLength": 200
      },
      "email": {
        "type": "string",
        "format": "email"
      },
      "phone": {
        "type": "string",
        "pattern": "^\\+?[1-9]\\d{1,14}$"
      }
    },
    "required": ["fullName", "email"]
  },
  "validation": [
    {
      "type": "unique-email",
      "message": "This email is already registered"
    }
  ]
}
```

### 8.2 Versioning Strategy

1. **Immutable published versions:** Once published, a definition version never changes. Edits create new drafts.

2. **Workflow-version pinning:** A workflow version pins exact field-group versions. Running instances continue on pinned versions even if new versions are published.

3. **Compatibility policy:**
   - **Patch version** (e.g., 1.0.0 → 1.0.1): Non-breaking metadata/label changes only.
   - **Minor version** (e.g., 1.0.0 → 1.1.0): Additive fields with safe defaults.
   - **Major version** (e.g., 1.0.0 → 2.0.0): Breaking schema or rule changes requiring migration.

4. **Migration path (v2):** Optional migration scripts map old submissions to new version schema. Migration is explicit and auditable, never implicit.

### 8.3 Storage Split (v1)

- **Definition:** JSON fixtures in `src/UmbracoPrism.MockBackOffice/Fixtures/field-groups/` (seeded on startup).
- **Binding:** Workflow JSON references `"fieldGroups": [{"key": "personal-details", "version": 1, "requiredInStates": ["Draft"]}]`.
- **Submission:** Instance-level values stored in `prismWorkflowEvents` with `eventType: "FieldGroupSubmitted"` and `payload: { submitted data }`.

---

## 9. Client-Server Dialog Protocol

### 9.1 Dialog Loop Contract

All renderer clients use the same loop for create/render/submit/action:

1. Call runtime endpoint (e.g., `POST /instances`, `GET /render`).
2. Read HTTP status and parse envelope.
3. Branch by `responseState`:
   - `ask_now`: Render payload and await user action.
   - `wait`: Show pending UI and schedule poll using `pollAfterMs`.
   - `complete`: Show terminal view and stop polling.
   - `error`: Route to typed error handling (see Section 9.3).
4. Include `stateVersion` on mutating requests to enforce optimistic concurrency.
5. Attach and log `correlationId` end-to-end for support diagnostics.

### 9.2 Example Dialog Sequence (Including Wait/Poll)

```
1. Client: POST /instances {"workflowKey": "information-request"}
   → 200 OK + ask_now + Collect(personal-details)

2. User fills personal-details and clicks Submit.

3. Client: POST /submit/personal-details {"stateVersion": 1, "data": {...}}
   → 202 Accepted + wait + pollAfterMs: 2000
   (Async verification running: email uniqueness check)

4. Client shows "Checking your information..." and polls after 2s.

5. Client: GET /render
   → 202 Accepted + wait + pollAfterMs: 3000
   (Still verifying)

6. Client polls again after 3s.

7. Client: GET /render
   → 200 OK + ask_now + Review(all-sections)

8. User confirms and clicks Submit for Review.

9. Client: POST /actions/submit {"stateVersion": 2}
   → 200 OK + complete + Completion(submitted-confirmation)

10. Client renders Completion and exits loop.
```

### 9.3 Error Handling

All errors return `responseState: "error"` with typed problems:

```json
{
  "instanceId": "wf_123",
  "responseState": "error",
  "stateVersion": 5,
  "correlationId": "...",
  "problems": [
    {
      "type": "validation",
      "fieldGroupKey": "personal-details",
      "field": "email",
      "message": "This email is already registered"
    },
    {
      "type": "concurrency",
      "message": "Another user has updated this workflow. Please refresh.",
      "expectedVersion": 5,
      "actualVersion": 7
    }
  ]
}
```

**Problem Types:**
- `validation`: Field-level or group-level validation failure.
- `concurrency`: `stateVersion` mismatch (409 Conflict).
- `authorization`: User not allowed to perform action (403 Forbidden).
- `not_found`: Instance not found or hidden (404 Not Found).
- `system`: Infrastructure failure (500/503).

---

## 10. Helper Utilities

### 10.1 Server Utilities (Core)

1. **WorkflowResponseFactory**
   - Creates canonical envelope for `ask_now`, `wait`, `complete`, and `error`.
   - Ensures consistent headers (`Retry-After` where relevant) and `correlationId` propagation.
   - Maps domain outcome → HTTP status + `responseState`.

2. **WorkflowProblemFactory**
   - Builds typed problems for validation/auth/conflict/system categories.
   - Keeps error payloads stable across endpoints.

3. **WorkflowConcurrencyGuard**
   - Validates submitted `stateVersion` and produces conflict outcomes.
   - Single-line usage: `guard.ValidateOrThrow(submitted, current)`.

4. **WorkflowStateMachine**
   - Evaluates transitions and guards.
   - Returns: `CanTransition`, `NextState`, `RequiredGuards`.

5. **FieldGroupValidator**
   - Validates submitted data against JSON Schema + custom validation rules.
   - Returns typed validation problems.

### 10.2 Client Utilities (Client)

1. **workflowApiClient**
   - Typed methods for create/render/submit/action/poll that always return parsed envelope.
   - Handles authorization headers, correlation ID propagation, retry policy.

2. **workflowDialogOrchestrator**
   - Single state machine for `idle → asking → waiting → complete → error`.
   - Handles timers, retry policy, and cancel/dispose behavior.
   - Exposes observables/events for UI binding.

3. **workflowErrorMapper**
   - Converts HTTP/problem payloads into user-safe messages and telemetry events.
   - Maps problem type → user-facing message + suggested action.

4. **workflowTraceContext**
   - Propagates `correlationId` through logs and UI diagnostics panel.
   - Exposes "Copy correlation ID" button for support tickets.

**Design Rule:** Channel components should not interpret raw HTTP responses directly; they consume orchestrator state only.

---

## 11. Phase 0 Deliverable: Example Workflow Definition

The Information Request workflow is the canonical demo example. It models a generic request → review → outcome flow relatable to any back-office system (retirement quote, permit application, etc.).

### 11.1 Information Request Workflow (JSON Fixture)

**File:** `src/UmbracoPrism.MockBackOffice/Fixtures/workflows/information-request.json`

```json
{
  "workflowKey": "information-request",
  "version": 1,
  "status": "Published",
  "metadata": {
    "title": "Information Request",
    "description": "Generic request submission and approval workflow",
    "owner": "Prism Demo Team"
  },
  "states": [
    {
      "name": "Draft",
      "archetype": "Collect",
      "fieldGroups": [
        {
          "key": "personal-details",
          "version": 1,
          "required": true,
          "editable": true
        },
        {
          "key": "request-details",
          "version": 1,
          "required": true,
          "editable": true
        }
      ],
      "isInitial": true,
      "isTerminal": false
    },
    {
      "name": "Submitted",
      "archetype": "StatusTimeline",
      "fieldGroups": [],
      "isInitial": false,
      "isTerminal": false
    },
    {
      "name": "UnderReview",
      "archetype": "Decision",
      "fieldGroups": [
        {
          "key": "personal-details",
          "version": 1,
          "required": false,
          "editable": false
        },
        {
          "key": "request-details",
          "version": 1,
          "required": false,
          "editable": false
        }
      ],
      "isInitial": false,
      "isTerminal": false
    },
    {
      "name": "NeedsChanges",
      "archetype": "RequestChanges",
      "fieldGroups": [
        {
          "key": "personal-details",
          "version": 1,
          "required": true,
          "editable": true
        },
        {
          "key": "request-details",
          "version": 1,
          "required": true,
          "editable": true
        }
      ],
      "isInitial": false,
      "isTerminal": false
    },
    {
      "name": "Approved",
      "archetype": "Completion",
      "fieldGroups": [],
      "isInitial": false,
      "isTerminal": true,
      "outcome": "success"
    },
    {
      "name": "Rejected",
      "archetype": "Completion",
      "fieldGroups": [],
      "isInitial": false,
      "isTerminal": true,
      "outcome": "failure"
    }
  ],
  "transitions": [
    {
      "from": "Draft",
      "to": "Submitted",
      "actionKey": "submit",
      "label": "Submit for Review",
      "guards": ["all-required-field-groups-submitted"],
      "confirmationMessage": "Are you sure you want to submit?"
    },
    {
      "from": "Submitted",
      "to": "UnderReview",
      "actionKey": "assign-to-reviewer",
      "label": "Assign to Reviewer",
      "guards": [],
      "automated": true,
      "createsTask": {
        "requiredRole": "backoffice-reviewers"
      }
    },
    {
      "from": "UnderReview",
      "to": "Approved",
      "actionKey": "approve",
      "label": "Approve",
      "guards": ["user-in-role:backoffice-reviewers"],
      "confirmationMessage": "Are you sure you want to approve this request?"
    },
    {
      "from": "UnderReview",
      "to": "Rejected",
      "actionKey": "reject",
      "label": "Reject",
      "guards": ["user-in-role:backoffice-reviewers"],
      "confirmationMessage": "Are you sure you want to reject this request?",
      "requiresRationale": true
    },
    {
      "from": "UnderReview",
      "to": "NeedsChanges",
      "actionKey": "request-changes",
      "label": "Request Changes",
      "guards": ["user-in-role:backoffice-reviewers"],
      "requiresRationale": true
    },
    {
      "from": "NeedsChanges",
      "to": "Submitted",
      "actionKey": "resubmit",
      "label": "Resubmit",
      "guards": ["all-required-field-groups-submitted"],
      "confirmationMessage": "Are you sure you want to resubmit?"
    }
  ]
}
```

### 11.2 Field Group Definitions

**File:** `src/UmbracoPrism.MockBackOffice/Fixtures/field-groups/personal-details.json`

```json
{
  "fieldGroupKey": "personal-details",
  "version": 1,
  "metadata": {
    "title": "Personal Details",
    "description": "Your name and contact information"
  },
  "schema": {
    "type": "object",
    "properties": {
      "fullName": {
        "type": "string",
        "minLength": 1,
        "maxLength": 200,
        "title": "Full Name"
      },
      "email": {
        "type": "string",
        "format": "email",
        "title": "Email Address"
      },
      "phone": {
        "type": "string",
        "pattern": "^\\+?[1-9]\\d{1,14}$",
        "title": "Phone Number"
      }
    },
    "required": ["fullName", "email"]
  },
  "validation": []
}
```

**File:** `src/UmbracoPrism.MockBackOffice/Fixtures/field-groups/request-details.json`

```json
{
  "fieldGroupKey": "request-details",
  "version": 1,
  "metadata": {
    "title": "Request Details",
    "description": "What information do you need?"
  },
  "schema": {
    "type": "object",
    "properties": {
      "requestType": {
        "type": "string",
        "enum": ["general-inquiry", "document-request", "account-update", "other"],
        "title": "Request Type"
      },
      "description": {
        "type": "string",
        "minLength": 10,
        "maxLength": 2000,
        "title": "Description"
      },
      "urgency": {
        "type": "string",
        "enum": ["low", "medium", "high"],
        "title": "Urgency",
        "default": "medium"
      }
    },
    "required": ["requestType", "description"]
  },
  "validation": []
}
```

---

## 12. Implementation Phases

### Phase 1: Core Runtime Skeleton

**Goal:** Implement minimal state machine execution, audit event append, and runtime endpoints.

**Tasks:**
- Add workflow definition/instance models in `UmbracoPrism.Core.Models`.
- Create NPoco schema classes (`PrismWorkflowInstanceSchema`, `PrismWorkflowEventSchema`, `PrismWorkflowTaskSchema`).
- Write migrations (`CreatePrismWorkflowInstancesTable`, etc.).
- Implement `WorkflowStateMachine`, `WorkflowResponseFactory`, `WorkflowConcurrencyGuard`.
- Add controllers: `WorkflowInstanceController`, `WorkflowTaskController`.
- Wire up `IPrismContext` for tenant/user resolution.

**Deliverables:**
- Passing unit tests in `UmbracoPrism.Core.Tests` for transitions, concurrency, and validation.
- Minimal `.http` file for manual endpoint testing.

**Companion Design Docs Required:**
- **Blathers:** Backend contracts spec (`docs/design/workflow-forms-backend.md`) detailing all endpoint contracts, request/response shapes, validation rules, and error codes.

---

### Phase 2: Field-Group Engine and Versioning

**Goal:** Implement field-group definition storage, binding to states, submission validation, and version pinning.

**Tasks:**
- Add `FieldGroupDefinition` model and JSON fixture loader.
- Implement `FieldGroupValidator` with JSON Schema validation.
- Add `FieldGroupBindingResolver` to map state → required/optional groups.
- Store submissions in `prismWorkflowEvents` with `eventType: "FieldGroupSubmitted"`.
- Add version pinning logic: workflow instance locks field-group versions on creation.

**Deliverables:**
- Tests for version pinning, additive changes, and validation.
- Fixture-driven test: load `personal-details.json`, validate good/bad submissions.

---

### Phase 3: MockBackOffice Emulator

**Goal:** Add workflow authoring and queue simulation endpoints to `UmbracoPrism.MockBackOffice`.

**Tasks:**
- Add `/api/backoffice/workflows/definitions` endpoints: list, import, export, publish.
- Add `/api/backoffice/workflows/tasks` endpoint: list tasks for current role, claim task, complete task.
- Add deterministic seeded workflows in `MockBackOfficeComposer` (load from JSON fixtures).
- Add route-through mode toggle: `RuntimeMode = Core` routes to actual Core endpoints.

**Deliverables:**
- Emulator scriptable demo flow using `.http` requests.
- Seeded `information-request` workflow on TestSite startup.

**Companion Design Docs Required:**
- **Brewster:** MockBackOffice/TestSite integration spec (`docs/design/workflow-forms-testsite.md`) detailing seeding strategy, emulator endpoints, demo personas, and scriptable test scenarios.

---

### Phase 4: Client Archetype Renderer

**Goal:** Implement reusable archetype components in `UmbracoPrism.Client`.

**Tasks:**
- Add web components: `<prism-workflow-collect>`, `<prism-workflow-review>`, `<prism-workflow-decision>`, `<prism-workflow-completion>`, `<prism-workflow-timeline>`.
- Implement `workflowApiClient`, `workflowDialogOrchestrator`, `workflowErrorMapper`.
- Add Storybook stories using render payload fixtures (no backend required).
- Wire submit/action calls to runtime endpoints.

**Deliverables:**
- Demo journey running in browser with clear state transitions.
- Storybook stories for all 7 archetypes.

**Companion Design Docs Required:**
- **Isabelle:** Client archetypes spec (`docs/design/workflow-forms-client.md`) detailing component API, props, events, accessibility patterns, and Storybook fixture strategy.

---

### Phase 5: TestSite Demo Scenario

**Goal:** Seed end-to-end demo workflow into `UmbracoPrism.TestSite`.

**Tasks:**
- Add `information-request` workflow seeding in TestSite startup.
- Add demo user personas (submitter, reviewer) with pre-configured roles.
- Add walkthrough docs and screenshots in `docs/demo-walkthrough.md`.
- Add smoke tests (API + UI happy path) in Playwright.

**Deliverables:**
- Repeatable repo demo script for contributors.
- CI smoke test: create instance → submit → approve → verify completion.

---

### Phase 6: Security Hardening

**Goal:** Tenant isolation validation, authorization enforcement, and security acceptance criteria.

**Tasks:**
- Add tenant isolation tests: user A in tenant X cannot see instances from tenant Y.
- Add authorization tests: user without `backoffice-reviewers` role cannot claim tasks.
- Add concurrency tests: concurrent submissions produce 409 Conflict.
- Add CSRF protection on mutating endpoints (Umbraco anti-forgery token).
- Add rate limiting on workflow creation (prevent DoS).

**Deliverables:**
- Security test suite green in CI.
- Sign-off from Copper (security review).

**Companion Design Docs Required:**
- **Copper:** Security review spec (`docs/design/workflow-forms-security.md`) detailing tenant isolation enforcement, authorization checks, CSRF/XSS/injection risks, rate limiting, and pentest checklist.

---

## 13. Non-Goals (v1)

Explicitly out of scope for the demo framework:

1. **Production-grade low-code designer.** No drag-and-drop workflow builder in v1.
2. **Executable scripts in workflow definitions.** No custom C#/JS code execution; guards are pre-defined.
3. **Cross-tenant shared workflow execution.** All workflows are tenant-isolated.
4. **External integration connectors.** No email/SMS/webhook actions in v1.
5. **Advanced SLA/escalation rules.** No automatic reassignment or deadline enforcement.
6. **Multi-language support.** English only in v1; i18n deferred to v2.

---

## 14. Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Contract drift between emulator and Core runtime | Medium | Shared DTOs and contract tests run against both modes |
| Over-scoping into a full BPM product | High | Strict non-goals and demo-first state machine scope |
| Version migration complexity | Medium | Pin-by-default strategy; explicit migration tooling only when needed |
| Security bypass in emulator pathways | High | Enforce authorization/guard checks in Core runtime regardless of caller |
| Renderer coupling to workflow internals | Medium | Renderer consumes only render payload contract, never raw transition graph |
| Accessibility regressions | Medium | Playwright axe-core tests + manual keyboard/screen reader testing |
| Performance bottleneck on event append | Low | Single transaction for state + audit; profile before optimizing |

---

## 15. Success Criteria

### Demo Sign-Off Criteria

1. **Functional completeness:**
   - Information Request workflow runs end-to-end: Draft → Submitted → UnderReview → Approved.
   - All 7 archetypes render correctly in Storybook and TestSite.
   - Optimistic concurrency enforced (409 Conflict on stale `stateVersion`).

2. **Tenant isolation:**
   - User in tenant A cannot see/modify instances in tenant B.
   - All queries filter by `TenantId` from `IPrismContext`.

3. **Accessibility:**
   - All archetypes pass WCAG 2.1 AA automated checks (axe-core).
   - Keyboard-only navigation works for all interactive elements.
   - Screen reader spot-check confirms semantic structure.

4. **Documentation:**
   - Companion design docs complete: backend, client, TestSite, security.
   - Demo walkthrough doc with screenshots.
   - API reference doc generated from XML comments.

5. **Testing:**
   - Unit tests green: transitions, concurrency, validation, tenant isolation.
   - Integration tests green: full workflow lifecycle.
   - Smoke test green: CI runs end-to-end demo scenario.

---

## 16. Related Design Documents

This architecture design references and is referenced by the following companion documents:

1. **Backend Contracts** (`docs/design/workflow-forms-backend.md`)
   - Owner: Blathers
   - Covers: Endpoint contracts, request/response schemas, validation rules, error codes.

2. **Client Archetypes** (`docs/design/workflow-forms-client.md`)
   - Owner: Isabelle
   - Covers: Component API, props, events, accessibility patterns, Storybook fixtures.

3. **TestSite Integration** (`docs/design/workflow-forms-testsite.md`)
   - Owner: Brewster
   - Covers: Seeding strategy, emulator endpoints, demo personas, scriptable scenarios.

4. **Security Review** (`docs/design/workflow-forms-security.md`)
   - Owner: Copper
   - Covers: Tenant isolation enforcement, authorization checks, CSRF/XSS risks, rate limiting.

---

## 17. Changelog

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2026-04-08 | 1.0 | Tom Nook | Initial authoritative design; all open questions resolved |

---

## Appendix A: Decision Rationale Summary

**Decision 1 (Storage):** Hybrid NPoco + JSON fixtures balances transactional integrity for instances with easy seeding/versioning for definitions.

**Decision 2 (Actor Model):** Role-based only is the minimal viable demo model; user assignment adds complexity without demo value.

**Decision 3 (Concurrency):** Optimistic concurrency is cheap to add now, expensive to retrofit; prevents realistic race conditions.

**Decision 4 (Audit):** Strictly transactional audit keeps consistency guarantees simple; eventual consistency is premature optimization.

**Decision 5 (Accessibility):** WCAG 2.1 AA is the baseline for modern web applications; GDS design system aesthetic reinforces this.

---

## Appendix B: Glossary

- **Archetype:** A reusable UI pattern (Collect, Review, Decision, etc.) that renders workflow state.
- **Field Group:** A versioned, reusable schema block representing a logical section of a form.
- **Workflow Definition:** Immutable, versioned configuration describing states, transitions, and field-group bindings.
- **Workflow Instance:** A single execution of a workflow definition, scoped to a tenant and user.
- **State Version:** Optimistic concurrency token incremented on every state change.
- **Render Payload:** The contract returned by the runtime that tells the client what to display and which actions are available.
- **Response State:** The high-level outcome of a workflow dialog request (`ask_now`, `wait`, `complete`, `error`).

---

**End of Architecture Design Document**
