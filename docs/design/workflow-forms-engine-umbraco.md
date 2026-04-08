# Prism Workflow Forms Engine — Umbraco Integration Design

**Author:** Brewster (Umbraco Platform Specialist)  
**Requested by:** Jonny Muir  
**Status:** Design specification (ready for implementation)  
**Date:** 2026-04-08

**Related Documents:**
- [Workflow Forms Engine Demo Proposal](./workflow-forms-engine-demo.md) — Core runtime contracts and architecture

---

## 1) Executive Summary

This document specifies how the Prism Workflow Forms Engine integrates into the Umbraco ecosystem through:

1. **MockBackOffice extension** — workflow emulator surface for deterministic demo scenarios
2. **TestSite integration** — code-first document types and route-hijacked demo pages
3. **Demo workflow seed packs** — JSON fixtures for repeatable scenarios
4. **HTTP scripts** — `.http` files for scripted API testing

The design respects the governance boundaries established in the core proposal: MockBackOffice owns emulator-only conveniences; Core runtime owns authoritative workflow semantics; TestSite owns CMS-native rendering patterns.

---

## 2) MockBackOffice Extension Design

### 2.1 Goal

Extend `UmbracoPrism.MockBackOffice` with a workflow emulator surface that demonstrates the full lifecycle of workflow-driven forms, including:
- Definition authoring and version management
- Instance creation and state transitions
- Operator queue simulation with decision-making personas

### 2.2 RuntimeMode Toggle

The emulator supports two execution modes:

1. **`RuntimeMode = Emulator`** (default for demos)
   - MockBackOffice handles all workflow state in-memory or in its own ephemeral data structures
   - No dependency on Core runtime services
   - Deterministic, repeatable, fast for local demos
   - State resets on restart (by design — demo-only)

2. **`RuntimeMode = Core`** (fidelity testing mode)
   - All workflow API calls proxied to Core runtime endpoints at `/umbraco/prism/workflows/*`
   - Used to validate that emulator contracts match Core implementation
   - Requires Core runtime services to be registered and running
   - State persisted per Core's storage strategy

**Implementation note:** The toggle affects DI registration — Emulator mode registers `IWorkflowRuntimeService` as `WorkflowEmulatorService`; Core mode registers it as `WorkflowProxyService` (HTTP client wrapper).

### 2.3 Configuration Shape

```json
{
  "PrismMockBackOffice": {
    "Tenants": [ /* existing tenant config */ ],
    "Members": [ /* existing member config */ ],
    "WorkflowEmulator": {
      "RuntimeMode": "Emulator",
      "SeedPacksPath": "workflow-seeds/",
      "OperatorPersonas": [
        {
          "PersonaId": "alice-reviewer",
          "Name": "Alice",
          "Role": "Reviewer",
          "AutoAssignDelay": null
        },
        {
          "PersonaId": "bob-approver",
          "Name": "Bob",
          "Role": "Approver",
          "AutoAssignDelay": "00:00:03"
        }
      ],
      "CoreRuntimeBaseUrl": "http://localhost:5000",
      "EnableAutoOperatorPolling": false
    }
  }
}
```

**Configuration properties:**

- `RuntimeMode` — `"Emulator"` or `"Core"`
- `SeedPacksPath` — relative path to JSON seed files (default: `"workflow-seeds/"`)
- `OperatorPersonas` — array of simulated operator identities for queue/decision demo
  - `PersonaId` — stable identifier for routing decisions (e.g., `"alice-reviewer"`)
  - `Name` — display name for demo UI
  - `Role` — semantic label (e.g., `"Reviewer"`, `"Approver"`)
  - `AutoAssignDelay` — optional TimeSpan; if set, emulator auto-assigns tasks after delay (demo convenience)
- `CoreRuntimeBaseUrl` — base URL when `RuntimeMode = Core`
- `EnableAutoOperatorPolling` — if true, emulator simulates operator decisions after `AutoAssignDelay` (advanced demo mode)

### 2.4 API Endpoint Design

All workflow emulator endpoints live under `/api/backoffice/workflows/`.

#### 2.4.1 Definition Management

**`GET /api/backoffice/workflows/definitions`**
- Purpose: List all workflow definitions (all versions or latest only)
- Query params: `?includeAllVersions=false`, `?status=Published`
- Response: Array of `WorkflowDefinitionSummary`
- Auth: JWT Bearer (Prism tenant validation)

**`GET /api/backoffice/workflows/definitions/{key}`**
- Purpose: Get a specific workflow definition by key (latest or specific version)
- Query params: `?version=2`
- Response: Full `WorkflowDefinition` including states, transitions, field-group bindings
- Auth: JWT Bearer

**`POST /api/backoffice/workflows/definitions`**
- Purpose: Import/create a new workflow definition (draft state)
- Body: Full `WorkflowDefinition` JSON
- Response: `201 Created` with location header
- Auth: JWT Bearer (requires admin role in emulator config)

**`PUT /api/backoffice/workflows/definitions/{key}/publish`**
- Purpose: Promote draft version to Published (immutable)
- Body: Optional `{ "effectiveDate": "2026-04-08T00:00:00Z" }`
- Response: `200 OK` with updated definition
- Auth: JWT Bearer (requires admin role)

**`PUT /api/backoffice/workflows/definitions/{key}/retire`**
- Purpose: Mark a published version as Retired (no new instances, existing continue)
- Body: Optional `{ "reason": "Replaced by v3" }`
- Response: `200 OK`
- Auth: JWT Bearer (requires admin role)

#### 2.4.2 Queue and Operator Simulation

**`GET /api/backoffice/workflows/queue`**
- Purpose: List pending workflow tasks for operator simulation
- Query params: `?role=Reviewer`, `?assignedTo=alice-reviewer`, `?status=Unassigned|Assigned|Completed`
- Response: Array of `WorkflowTaskSummary` with instance metadata
- Auth: JWT Bearer

**`POST /api/backoffice/workflows/queue/{taskId}/assign`**
- Purpose: Assign a task to a simulated operator persona
- Body: `{ "personaId": "alice-reviewer", "notes": "Claimed for review" }`
- Response: `200 OK` with updated task
- Auth: JWT Bearer

**`POST /api/backoffice/workflows/queue/{taskId}/decide`**
- Purpose: Submit an operator decision (approve/reject/request-changes)
- Body:
  ```json
  {
    "decision": "Approve",
    "personaId": "bob-approver",
    "reason": "All information verified",
    "requestedChanges": []
  }
  ```
- Response: `200 OK` with updated task and new instance state
- Auth: JWT Bearer

**Decision enum values:**
- `Approve` — transition to approved/next state
- `Reject` — transition to rejected/terminal state
- `RequestChanges` — route back to actor with correction list

#### 2.4.3 Instance Lifecycle (Pass-Through or Emulated)

These endpoints mirror the Core runtime contracts from the main proposal. In `RuntimeMode = Emulator`, they execute locally; in `RuntimeMode = Core`, they proxy to Core endpoints.

**`POST /api/backoffice/workflows/instances`**
- Purpose: Create a new workflow instance from a published definition
- Body: `{ "workflowKey": "information-request", "actorTenantCode": "ALPHA-CORP", "metadata": {} }`
- Response: `201 Created` with initial render payload (typically `ask_now` + `Collect`)
- Auth: JWT Bearer

**`GET /api/backoffice/workflows/instances/{id}/render`**
- Purpose: Get current render payload for instance (polling endpoint)
- Response: Workflow render envelope (`ask_now` / `wait` / `complete` / `error`)
- Auth: JWT Bearer

**`POST /api/backoffice/workflows/instances/{id}/submit/{fieldGroupKey}`**
- Purpose: Submit field-group data
- Body: `{ "stateVersion": 3, "values": { "firstName": "Alice", "lastName": "Smith" } }`
- Response: `200 OK` or `202 Accepted` with updated render payload
- Auth: JWT Bearer

**`POST /api/backoffice/workflows/instances/{id}/actions/{actionKey}`**
- Purpose: Execute a non-submission action (e.g., `cancel`, `withdraw`)
- Body: `{ "stateVersion": 5, "reason": "Duplicate request" }`
- Response: `200 OK` with outcome
- Auth: JWT Bearer

**`GET /api/backoffice/workflows/instances/{id}/timeline`**
- Purpose: Get audit timeline (state transitions, submissions, operator decisions)
- Response: Array of `WorkflowEvent` with timestamps and actor metadata
- Auth: JWT Bearer

### 2.5 Controller Architecture

All workflow emulator endpoints are handled by a single controller: `WorkflowEmulatorController`.

**File:** `src/UmbracoPrism.MockBackOffice/Controllers/WorkflowEmulatorController.cs`

**Class signature:**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Workflow.Contracts;

namespace UmbracoPrism.MockBackOffice.Controllers;

/// <summary>
/// Workflow emulator controller — simulates workflow runtime and operator actions.
/// Execution mode determined by RuntimeMode config (Emulator or Core proxy).
/// Development-only for demo purposes.
/// </summary>
[ApiController]
[Route("api/backoffice/workflows")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class WorkflowEmulatorController : ControllerBase
{
    private readonly IWorkflowRuntimeService _runtimeService;
    private readonly IWorkflowQueueService _queueService;
    private readonly WorkflowEmulatorOptions _options;
    private readonly ILogger<WorkflowEmulatorController> _logger;

    public WorkflowEmulatorController(
        IWorkflowRuntimeService runtimeService,
        IWorkflowQueueService queueService,
        IOptions<WorkflowEmulatorOptions> options,
        ILogger<WorkflowEmulatorController> logger)
    {
        _runtimeService = runtimeService;
        _queueService = queueService;
        _options = options.Value;
        _logger = logger;
    }

    // Definition management actions (§2.4.1)
    [HttpGet("definitions")]
    public async Task<IActionResult> ListDefinitions([FromQuery] bool includeAllVersions = false, [FromQuery] string? status = null) { /* ... */ }

    [HttpGet("definitions/{key}")]
    public async Task<IActionResult> GetDefinition(string key, [FromQuery] int? version = null) { /* ... */ }

    [HttpPost("definitions")]
    public async Task<IActionResult> CreateDefinition([FromBody] WorkflowDefinitionDto definition) { /* ... */ }

    [HttpPut("definitions/{key}/publish")]
    public async Task<IActionResult> PublishDefinition(string key, [FromBody] PublishWorkflowRequest request) { /* ... */ }

    [HttpPut("definitions/{key}/retire")]
    public async Task<IActionResult> RetireDefinition(string key, [FromBody] RetireWorkflowRequest request) { /* ... */ }

    // Queue and operator simulation actions (§2.4.2)
    [HttpGet("queue")]
    public async Task<IActionResult> ListQueuedTasks([FromQuery] string? role = null, [FromQuery] string? assignedTo = null, [FromQuery] string? status = null) { /* ... */ }

    [HttpPost("queue/{taskId}/assign")]
    public async Task<IActionResult> AssignTask(string taskId, [FromBody] AssignTaskRequest request) { /* ... */ }

    [HttpPost("queue/{taskId}/decide")]
    public async Task<IActionResult> DecideTask(string taskId, [FromBody] OperatorDecisionRequest request) { /* ... */ }

    // Instance lifecycle actions (§2.4.3)
    [HttpPost("instances")]
    public async Task<IActionResult> CreateInstance([FromBody] CreateWorkflowInstanceRequest request) { /* ... */ }

    [HttpGet("instances/{id}/render")]
    public async Task<IActionResult> GetRender(string id) { /* ... */ }

    [HttpPost("instances/{id}/submit/{fieldGroupKey}")]
    public async Task<IActionResult> SubmitFieldGroup(string id, string fieldGroupKey, [FromBody] FieldGroupSubmissionRequest request) { /* ... */ }

    [HttpPost("instances/{id}/actions/{actionKey}")]
    public async Task<IActionResult> ExecuteAction(string id, string actionKey, [FromBody] WorkflowActionRequest request) { /* ... */ }

    [HttpGet("instances/{id}/timeline")]
    public async Task<IActionResult> GetTimeline(string id) { /* ... */ }
}
```

**Design notes:**
- All actions are async (workflow state may involve I/O in Core mode)
- Controller delegates to `IWorkflowRuntimeService` and `IWorkflowQueueService` abstractions
- Implementation injected based on `RuntimeMode` config
- Security validation happens in middleware (Prism JWT Bearer token authentication)
- Controller does NOT interpret workflow semantics — it calls the service layer only

### 2.6 Service Layer Design

**`IWorkflowRuntimeService`** — core workflow execution contract (shared with Core)

Implementations:
1. `WorkflowEmulatorService` — in-memory state machine for `RuntimeMode = Emulator`
2. `WorkflowProxyService` — HTTP client wrapper for `RuntimeMode = Core`

**`IWorkflowQueueService`** — operator task queue contract (emulator-only extension)

Implementation: `WorkflowEmulatorQueueService` — manages task queue and operator persona state.

**DI registration logic (in `Program.cs`):**

```csharp
var workflowConfig = builder.Configuration
    .GetSection("PrismMockBackOffice:WorkflowEmulator")
    .Get<WorkflowEmulatorOptions>() ?? new();

if (workflowConfig.RuntimeMode == "Core")
{
    builder.Services.AddHttpClient<IWorkflowRuntimeService, WorkflowProxyService>(client =>
    {
        client.BaseAddress = new Uri(workflowConfig.CoreRuntimeBaseUrl);
    });
    builder.Services.AddSingleton<IWorkflowQueueService, WorkflowEmulatorQueueService>(); // Queue still local
}
else
{
    builder.Services.AddSingleton<IWorkflowRuntimeService, WorkflowEmulatorService>();
    builder.Services.AddSingleton<IWorkflowQueueService, WorkflowEmulatorQueueService>();
}

builder.Services.AddSingleton<IWorkflowSeedLoader, WorkflowSeedLoader>();
builder.Services.Configure<WorkflowEmulatorOptions>(builder.Configuration.GetSection("PrismMockBackOffice:WorkflowEmulator"));
```

### 2.7 Governance Boundaries

**MockBackOffice MAY:**
- Implement emulator-only convenience endpoints (e.g., operator personas, auto-assignment)
- Load seed packs for deterministic demo scenarios
- Namespace emulator-specific DTOs under `UmbracoPrism.MockBackOffice.Workflow.*`
- Add UI hints and shortcuts for demo clarity (e.g., "Fast-forward to approval")

**MockBackOffice MUST:**
- Always delegate authoritative workflow semantics to Core runtime in production scenarios
- Never leak emulator-only contracts into Core runtime DTOs
- Respect the same auth/tenant validation rules as Core runtime
- Execute security guards in Core runtime, even if initiated from emulator UI

**MockBackOffice MUST NOT:**
- Override Core runtime transition rules or guard logic
- Persist emulator state beyond the demo session lifetime (ephemeral only)
- Allow bypassing auth/tenant checks in emulator mode (always validate JWT claims)

---

## 3) Seeded Demo Workflow Pack

### 3.1 Seed Pack Structure

Seed packs are JSON files stored in the path specified by `WorkflowEmulator.SeedPacksPath` (default: `workflow-seeds/`).

**File:** `workflow-seeds/information-request-v1.json`

This is the canonical demo workflow — a generic "Information Request" form demonstrating the full lifecycle:

```json
{
  "workflowKey": "information-request",
  "version": 1,
  "status": "Published",
  "displayName": "Information Request Workflow",
  "description": "Generic information request with review and approval lifecycle",
  "effectiveDate": "2026-04-01T00:00:00Z",
  "states": [
    {
      "stateKey": "draft",
      "displayName": "Draft",
      "archetype": "Collect",
      "isInitialState": true,
      "isTerminalState": false
    },
    {
      "stateKey": "submitted",
      "displayName": "Submitted",
      "archetype": "StatusTimeline",
      "isInitialState": false,
      "isTerminalState": false
    },
    {
      "stateKey": "under-review",
      "displayName": "Under Review",
      "archetype": "Decision",
      "isInitialState": false,
      "isTerminalState": false
    },
    {
      "stateKey": "needs-changes",
      "displayName": "Changes Required",
      "archetype": "RequestChanges",
      "isInitialState": false,
      "isTerminalState": false
    },
    {
      "stateKey": "approved",
      "displayName": "Approved",
      "archetype": "Completion",
      "isInitialState": false,
      "isTerminalState": true,
      "outcomeType": "Success"
    },
    {
      "stateKey": "rejected",
      "displayName": "Rejected",
      "archetype": "Completion",
      "isInitialState": false,
      "isTerminalState": true,
      "outcomeType": "Failure"
    }
  ],
  "transitions": [
    {
      "transitionKey": "submit-for-review",
      "fromState": "draft",
      "toState": "submitted",
      "displayName": "Submit for Review",
      "guardKey": "requires-complete-application",
      "autoAdvance": false
    },
    {
      "transitionKey": "assign-to-review",
      "fromState": "submitted",
      "toState": "under-review",
      "displayName": "Assign to Reviewer",
      "guardKey": null,
      "autoAdvance": true,
      "autoAdvanceDelayMs": 2000
    },
    {
      "transitionKey": "approve",
      "fromState": "under-review",
      "toState": "approved",
      "displayName": "Approve",
      "guardKey": "requires-approval-authority",
      "autoAdvance": false
    },
    {
      "transitionKey": "reject",
      "fromState": "under-review",
      "toState": "rejected",
      "displayName": "Reject",
      "guardKey": "requires-approval-authority",
      "autoAdvance": false
    },
    {
      "transitionKey": "request-changes",
      "fromState": "under-review",
      "toState": "needs-changes",
      "displayName": "Request Changes",
      "guardKey": "requires-review-authority",
      "autoAdvance": false
    },
    {
      "transitionKey": "resubmit",
      "fromState": "needs-changes",
      "toState": "submitted",
      "displayName": "Resubmit",
      "guardKey": "requires-changes-addressed",
      "autoAdvance": false
    }
  ],
  "guards": [
    {
      "guardKey": "requires-complete-application",
      "displayName": "Complete Application Required",
      "description": "Applicant details and request details must be fully submitted",
      "evaluationType": "FieldGroupCompletion",
      "parameters": {
        "requiredFieldGroups": ["applicant-details", "request-details"]
      }
    },
    {
      "guardKey": "requires-approval-authority",
      "displayName": "Approval Authority Required",
      "description": "Actor must have Approver role",
      "evaluationType": "RoleClaim",
      "parameters": {
        "requiredRole": "Approver"
      }
    },
    {
      "guardKey": "requires-review-authority",
      "displayName": "Review Authority Required",
      "description": "Actor must have Reviewer or Approver role",
      "evaluationType": "RoleClaim",
      "parameters": {
        "requiredRoles": ["Reviewer", "Approver"]
      }
    },
    {
      "guardKey": "requires-changes-addressed",
      "displayName": "Changes Must Be Addressed",
      "description": "All requested changes must be marked complete",
      "evaluationType": "ChangeRequestCompletion",
      "parameters": {}
    }
  ],
  "fieldGroups": [
    {
      "fieldGroupKey": "applicant-details",
      "version": 1,
      "displayName": "Applicant Details",
      "description": "Personal information about the applicant",
      "fields": [
        {
          "fieldKey": "first-name",
          "displayName": "First Name",
          "fieldType": "Text",
          "required": true,
          "validation": {
            "maxLength": 100
          }
        },
        {
          "fieldKey": "last-name",
          "displayName": "Last Name",
          "fieldType": "Text",
          "required": true,
          "validation": {
            "maxLength": 100
          }
        },
        {
          "fieldKey": "email",
          "displayName": "Email Address",
          "fieldType": "Email",
          "required": true,
          "validation": {
            "pattern": "^[^@]+@[^@]+\\.[^@]+$"
          }
        },
        {
          "fieldKey": "phone",
          "displayName": "Phone Number",
          "fieldType": "Tel",
          "required": false,
          "validation": {
            "pattern": "^\\+?[0-9\\s\\-()]+$"
          }
        },
        {
          "fieldKey": "date-of-birth",
          "displayName": "Date of Birth",
          "fieldType": "Date",
          "required": true,
          "validation": {
            "minAge": 18
          }
        }
      ],
      "stateBindings": [
        {
          "stateKey": "draft",
          "visibility": "Required",
          "editability": "Editable"
        },
        {
          "stateKey": "needs-changes",
          "visibility": "Visible",
          "editability": "Editable"
        },
        {
          "stateKey": "submitted",
          "visibility": "Visible",
          "editability": "ReadOnly"
        },
        {
          "stateKey": "under-review",
          "visibility": "Visible",
          "editability": "ReadOnly"
        },
        {
          "stateKey": "approved",
          "visibility": "Visible",
          "editability": "ReadOnly"
        },
        {
          "stateKey": "rejected",
          "visibility": "Visible",
          "editability": "ReadOnly"
        }
      ]
    },
    {
      "fieldGroupKey": "request-details",
      "version": 1,
      "displayName": "Request Details",
      "description": "Details about the information being requested",
      "fields": [
        {
          "fieldKey": "category",
          "displayName": "Request Category",
          "fieldType": "Select",
          "required": true,
          "options": [
            { "value": "personal", "label": "Personal Records" },
            { "value": "financial", "label": "Financial Information" },
            { "value": "medical", "label": "Medical Records" },
            { "value": "legal", "label": "Legal Documentation" },
            { "value": "other", "label": "Other" }
          ]
        },
        {
          "fieldKey": "description",
          "displayName": "Request Description",
          "fieldType": "Textarea",
          "required": true,
          "validation": {
            "minLength": 20,
            "maxLength": 2000
          }
        },
        {
          "fieldKey": "supporting-info",
          "displayName": "Supporting Information",
          "fieldType": "Textarea",
          "required": false,
          "validation": {
            "maxLength": 5000
          }
        },
        {
          "fieldKey": "urgency",
          "displayName": "Urgency Level",
          "fieldType": "Select",
          "required": true,
          "options": [
            { "value": "low", "label": "Low (30 days)" },
            { "value": "normal", "label": "Normal (14 days)" },
            { "value": "high", "label": "High (7 days)" },
            { "value": "urgent", "label": "Urgent (3 days)" }
          ],
          "defaultValue": "normal"
        }
      ],
      "stateBindings": [
        {
          "stateKey": "draft",
          "visibility": "Required",
          "editability": "Editable"
        },
        {
          "stateKey": "needs-changes",
          "visibility": "Visible",
          "editability": "Editable"
        },
        {
          "stateKey": "submitted",
          "visibility": "Visible",
          "editability": "ReadOnly"
        },
        {
          "stateKey": "under-review",
          "visibility": "Visible",
          "editability": "ReadOnly"
        },
        {
          "stateKey": "approved",
          "visibility": "Visible",
          "editability": "ReadOnly"
        },
        {
          "stateKey": "rejected",
          "visibility": "Visible",
          "editability": "ReadOnly"
        }
      ]
    }
  ],
  "metadata": {
    "tags": ["demo", "generic", "canonical"],
    "author": "Prism Team",
    "createdDate": "2026-04-01T00:00:00Z"
  }
}
```

**File:** `workflow-seeds/operator-personas.json`

```json
{
  "personas": [
    {
      "personaId": "alice-reviewer",
      "name": "Alice Thompson",
      "role": "Reviewer",
      "email": "alice@example.com",
      "avatarUrl": null,
      "autoAssignDelay": null,
      "claimableWorkflowStates": ["under-review"],
      "metadata": {
        "department": "Operations",
        "seniority": "Senior"
      }
    },
    {
      "personaId": "bob-approver",
      "name": "Bob Johnson",
      "role": "Approver",
      "email": "bob@example.com",
      "avatarUrl": null,
      "autoAssignDelay": "00:00:05",
      "claimableWorkflowStates": ["under-review"],
      "metadata": {
        "department": "Management",
        "seniority": "Director"
      }
    },
    {
      "personaId": "charlie-coordinator",
      "name": "Charlie Rodriguez",
      "role": "Coordinator",
      "email": "charlie@example.com",
      "avatarUrl": null,
      "autoAssignDelay": null,
      "claimableWorkflowStates": ["submitted"],
      "metadata": {
        "department": "Administration",
        "seniority": "Junior"
      }
    }
  ]
}
```

### 3.2 Seed Loader Design

**Service interface:**

```csharp
namespace UmbracoPrism.MockBackOffice.Workflow;

/// <summary>
/// Loads workflow seed packs from JSON files on startup.
/// </summary>
public interface IWorkflowSeedLoader
{
    /// <summary>
    /// Load all seed packs from the configured path.
    /// </summary>
    Task<IReadOnlyList<WorkflowDefinitionDto>> LoadDefinitionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Load operator personas from seed files.
    /// </summary>
    Task<IReadOnlyList<OperatorPersona>> LoadPersonasAsync(CancellationToken ct = default);
}
```

**Implementation:**

```csharp
public class WorkflowSeedLoader : IWorkflowSeedLoader
{
    private readonly WorkflowEmulatorOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WorkflowSeedLoader> _logger;

    public WorkflowSeedLoader(
        IOptions<WorkflowEmulatorOptions> options,
        IWebHostEnvironment env,
        ILogger<WorkflowSeedLoader> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> LoadDefinitionsAsync(CancellationToken ct = default)
    {
        var basePath = Path.Combine(_env.ContentRootPath, _options.SeedPacksPath);
        if (!Directory.Exists(basePath))
        {
            _logger.LogWarning("WORKFLOW SEED: Directory not found: {Path}", basePath);
            return Array.Empty<WorkflowDefinitionDto>();
        }

        var results = new List<WorkflowDefinitionDto>();
        var files = Directory.GetFiles(basePath, "*-v*.json", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            if (Path.GetFileName(file) == "operator-personas.json") continue;

            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var definition = JsonSerializer.Deserialize<WorkflowDefinitionDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (definition != null)
                {
                    results.Add(definition);
                    _logger.LogInformation("WORKFLOW SEED: Loaded {Key} v{Version} from {File}",
                        definition.WorkflowKey, definition.Version, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WORKFLOW SEED: Failed to load {File}", file);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<OperatorPersona>> LoadPersonasAsync(CancellationToken ct = default)
    {
        var filePath = Path.Combine(_env.ContentRootPath, _options.SeedPacksPath, "operator-personas.json");
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("WORKFLOW SEED: Personas file not found: {Path}", filePath);
            return Array.Empty<OperatorPersona>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var wrapper = JsonSerializer.Deserialize<PersonasWrapper>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("WORKFLOW SEED: Loaded {Count} operator personas", wrapper?.Personas?.Count ?? 0);
            return wrapper?.Personas ?? Array.Empty<OperatorPersona>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WORKFLOW SEED: Failed to load operator personas");
            return Array.Empty<OperatorPersona>();
        }
    }

    private class PersonasWrapper
    {
        public List<OperatorPersona> Personas { get; set; } = new();
    }
}
```

**Startup registration:**

The seed loader runs on application startup and populates the emulator service:

```csharp
// In Program.cs, after DI registration:
var app = builder.Build();

// Load workflow seeds on startup (Emulator mode only)
if (workflowConfig.RuntimeMode == "Emulator")
{
    using var scope = app.Services.CreateScope();
    var seedLoader = scope.ServiceProvider.GetRequiredService<IWorkflowSeedLoader>();
    var emulatorService = scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeService>() as WorkflowEmulatorService;
    var queueService = scope.ServiceProvider.GetRequiredService<IWorkflowQueueService>() as WorkflowEmulatorQueueService;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var definitions = await seedLoader.LoadDefinitionsAsync();
    var personas = await seedLoader.LoadPersonasAsync();

    foreach (var def in definitions)
    {
        emulatorService?.RegisterDefinition(def);
    }

    foreach (var persona in personas)
    {
        queueService?.RegisterPersona(persona);
    }

    logger.LogInformation("WORKFLOW EMULATOR: Loaded {DefCount} definitions, {PersonaCount} personas",
        definitions.Count, personas.Count);
}

app.Run();
```

---

## 4) .http Demo Script

**File:** `demo/workflow-demo.http`

This script demonstrates the complete workflow lifecycle using Visual Studio Code REST Client or JetBrains HTTP Client.

```http
### === Prism Workflow Forms Engine Demo ===
### Prerequisite: MockBackOffice running on localhost:5163
### Prerequisite: Valid Prism Bearer token (get from browser session after login)

@baseUrl = http://localhost:5163/api/backoffice/workflows
@bearerToken = eyJhbGciOiJSUzI1NiIsImtpZCI...  # Replace with your token

### ===========================
### 1. LIST WORKFLOW DEFINITIONS
### ===========================
GET {{baseUrl}}/definitions
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK with information-request workflow listed

### ===========================
### 2. GET SPECIFIC DEFINITION
### ===========================
GET {{baseUrl}}/definitions/information-request
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK with full definition JSON (states, transitions, field groups)

### ===========================
### 3. CREATE WORKFLOW INSTANCE
### ===========================
POST {{baseUrl}}/instances
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "workflowKey": "information-request",
  "actorTenantCode": "ALPHA-CORP",
  "metadata": {
    "source": "demo-script",
    "correlationId": "demo-run-001"
  }
}

### Expected: 201 Created
### Response includes instanceId and initial render payload:
###   responseState: "ask_now"
###   render.archetype: "Collect"
###   render.fieldGroups: ["applicant-details", "request-details"]
###   stateVersion: 1

### Save the instanceId from response for next steps
@instanceId = <replace-with-instance-id>

### ===========================
### 4. GET CURRENT RENDER STATE
### ===========================
GET {{baseUrl}}/instances/{{instanceId}}/render
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK with same render payload as step 3 (idempotent polling)

### ===========================
### 5. SUBMIT APPLICANT DETAILS
### ===========================
POST {{baseUrl}}/instances/{{instanceId}}/submit/applicant-details
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "stateVersion": 1,
  "values": {
    "first-name": "Alice",
    "last-name": "Johnson",
    "email": "alice.johnson@example.com",
    "phone": "+1-555-0123",
    "date-of-birth": "1990-05-15"
  }
}

### Expected: 200 OK or 202 Accepted
### Response updates render payload:
###   responseState: "ask_now" (still collecting)
###   render.archetype: "Collect"
###   render.fieldGroups: ["request-details"] (applicant-details marked complete)
###   stateVersion: 2

### ===========================
### 6. POLL RENDER AFTER FIRST SUBMISSION
### ===========================
GET {{baseUrl}}/instances/{{instanceId}}/render
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK, responseState "ask_now", archetype "Collect"
### Only request-details field group is now required

### ===========================
### 7. SUBMIT REQUEST DETAILS
### ===========================
POST {{baseUrl}}/instances/{{instanceId}}/submit/request-details
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "stateVersion": 2,
  "values": {
    "category": "financial",
    "description": "I am requesting copies of my account statements for the past 12 months for tax purposes.",
    "supporting-info": "Account number: 123456789. Required date range: January 2025 - December 2025.",
    "urgency": "normal"
  }
}

### Expected: 202 Accepted (transition to "submitted" state triggers auto-advance to "under-review")
### Response:
###   responseState: "wait"
###   pollAfterMs: 2000
###   stateVersion: 3

### ===========================
### 8. POLL RENDER AFTER SUBMISSION (WAIT STATE)
### ===========================
GET {{baseUrl}}/instances/{{instanceId}}/render
Authorization: Bearer {{bearerToken}}

### Expected: 202 Accepted, responseState "wait"
### Message: "Your request is being assigned to a reviewer..."

### === Wait 2-3 seconds, then poll again ===

### ===========================
### 9. POLL RENDER AFTER AUTO-ADVANCE
### ===========================
GET {{baseUrl}}/instances/{{instanceId}}/render
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK
### Response:
###   responseState: "ask_now"
###   render.archetype: "StatusTimeline"
###   currentState: "under-review"
###   stateVersion: 4
###   Submitted field groups visible as read-only

### ===========================
### 10. LIST PENDING TASKS (OPERATOR VIEW)
### ===========================
GET {{baseUrl}}/queue?status=Unassigned
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK with 1 task for our instance (state: under-review)

### Save taskId from response
@taskId = <replace-with-task-id>

### ===========================
### 11. ASSIGN TASK TO OPERATOR
### ===========================
POST {{baseUrl}}/queue/{{taskId}}/assign
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "personaId": "bob-approver",
  "notes": "Claimed for review by Bob"
}

### Expected: 200 OK with updated task (assignedTo: "bob-approver")

### ===========================
### 12. OPERATOR DECISION: APPROVE
### ===========================
POST {{baseUrl}}/queue/{{taskId}}/decide
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "decision": "Approve",
  "personaId": "bob-approver",
  "reason": "All information verified. Request approved for processing.",
  "requestedChanges": []
}

### Expected: 200 OK
### Response includes updated task status: "Completed"
### Instance transitions to "approved" state

### ===========================
### 13. FINAL POLL: COMPLETION STATE
### ===========================
GET {{baseUrl}}/instances/{{instanceId}}/render
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK
### Response:
###   responseState: "complete"
###   render.archetype: "Completion"
###   currentState: "approved"
###   outcomeType: "Success"
###   stateVersion: 5

### ===========================
### 14. GET AUDIT TIMELINE
### ===========================
GET {{baseUrl}}/instances/{{instanceId}}/timeline
Authorization: Bearer {{bearerToken}}

### Expected: 200 OK with array of WorkflowEvent:
###   - Instance created (draft)
###   - Applicant details submitted
###   - Request details submitted
###   - Auto-advanced to under-review
###   - Task assigned to bob-approver
###   - Approved by bob-approver
###   - Final state: approved

### ===========================
### END OF DEMO FLOW
### ===========================

### ===========================
### BONUS: REJECTION SCENARIO
### ===========================
### Create a second instance and reject it
POST {{baseUrl}}/instances
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "workflowKey": "information-request",
  "actorTenantCode": "ALPHA-CORP",
  "metadata": {
    "source": "demo-script",
    "scenario": "rejection"
  }
}

### (Follow steps 5-7 to submit both field groups)
### Then at decision step:
POST {{baseUrl}}/queue/{{taskId}}/decide
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "decision": "Reject",
  "personaId": "alice-reviewer",
  "reason": "Request does not meet eligibility criteria.",
  "requestedChanges": []
}

### Expected: Instance transitions to "rejected" terminal state

### ===========================
### BONUS: REQUEST CHANGES SCENARIO
### ===========================
### Create a third instance and request changes
POST {{baseUrl}}/instances
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "workflowKey": "information-request",
  "actorTenantCode": "ALPHA-CORP",
  "metadata": {
    "source": "demo-script",
    "scenario": "changes-required"
  }
}

### (Follow steps 5-7 to submit both field groups)
### Then at decision step:
POST {{baseUrl}}/queue/{{taskId}}/decide
Authorization: Bearer {{bearerToken}}
Content-Type: application/json

{
  "decision": "RequestChanges",
  "personaId": "alice-reviewer",
  "reason": "Additional information required.",
  "requestedChanges": [
    {
      "fieldGroupKey": "request-details",
      "fieldKey": "supporting-info",
      "issue": "Please provide account holder identification.",
      "required": true
    }
  ]
}

### Expected: Instance transitions to "needs-changes" state
### Actor can resubmit after addressing requested changes
```

---

## 5) TestSite Demo Integration

### 5.1 Document Type Design

**Alias:** `workflowDemoPage`  
**Display Name:** Workflow Demo Page  
**Icon:** `icon-science`  
**Template:** `WorkflowDemo.cshtml`  
**Allowed At Root:** Yes  
**Allowed Child Content Types:** None (leaf page)

**Property Groups and Properties:**

**Group: Workflow Configuration**
- `workflowKey` (Text String)
  - Label: Workflow Definition Key
  - Description: The key of the workflow definition to render (e.g., "information-request")
  - Data type: Textstring
  - Mandatory: Yes
  - Default value: `information-request`

**Group: Page Content**
- `pageTitle` (Text String)
  - Label: Page Title
  - Description: Heading displayed at the top of the page
  - Data type: Textstring
  - Mandatory: Yes
  - Default value: `Workflow Demo`

- `introductionText` (Textarea)
  - Label: Introduction Text
  - Description: Markdown or plain text introduction shown above the workflow shell
  - Data type: Textarea
  - Mandatory: No

**Group: Completion Behavior**
- `completionRedirectUrl` (Text String)
  - Label: Completion Redirect URL
  - Description: Optional URL to redirect after workflow completion (leave blank to stay on page)
  - Data type: Textstring
  - Mandatory: No

**Implementation file:** `src/UmbracoPrism.TestSite/WorkflowDemoContentType.cs`

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Creates the WorkflowDemoPage document type on application startup.
/// Development-only for demo purposes.
/// </summary>
public class WorkflowDemoContentType : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IWebHostEnvironment _env;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<WorkflowDemoContentType> _logger;

    private static readonly Guid BuiltInTextBoxKey = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid BuiltInTextAreaKey = new("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3");

    public WorkflowDemoContentType(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IWebHostEnvironment env,
        IRuntimeState runtimeState,
        ILogger<WorkflowDemoContentType> logger)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _env = env;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return;
        if (!_env.IsDevelopment()) return;

        await Task.Run(SetupAsync, cancellationToken);
    }

    private async Task SetupAsync()
    {
        try
        {
            if (_contentTypeService.Get("workflowDemoPage") != null)
            {
                _logger.LogDebug("WORKFLOW DEMO: Content type already exists");
                return;
            }

            var textBox = await _dataTypeService.GetAsync(BuiltInTextBoxKey);
            var textArea = await _dataTypeService.GetAsync(BuiltInTextAreaKey);

            if (textBox == null || textArea == null)
            {
                _logger.LogError("WORKFLOW DEMO: Could not resolve built-in data types");
                return;
            }

            var contentType = new ContentType(_contentTypeService, -1)
            {
                Alias = "workflowDemoPage",
                Name = "Workflow Demo Page",
                Icon = "icon-science",
                AllowedAsRoot = true,
                IsElement = false
            };

            // Create template reference (template file must exist)
            var template = new Template(_contentTypeService, "WorkflowDemo")
            {
                Alias = "WorkflowDemo",
                Name = "Workflow Demo"
            };
            contentType.AddTemplate(_contentTypeService, template);
            contentType.SetDefaultTemplate(template);

            // Group: Workflow Configuration
            contentType.AddPropertyGroup("Workflow Configuration");
            contentType.AddPropertyType(new PropertyType(_contentTypeService, textBox, "workflowKey")
            {
                Name = "Workflow Definition Key",
                Description = "The key of the workflow definition to render (e.g., 'information-request')",
                Mandatory = true,
                ValidationRegExp = null
            }, "Workflow Configuration");

            // Group: Page Content
            contentType.AddPropertyGroup("Page Content");
            contentType.AddPropertyType(new PropertyType(_contentTypeService, textBox, "pageTitle")
            {
                Name = "Page Title",
                Description = "Heading displayed at the top of the page",
                Mandatory = true
            }, "Page Content");

            contentType.AddPropertyType(new PropertyType(_contentTypeService, textArea, "introductionText")
            {
                Name = "Introduction Text",
                Description = "Markdown or plain text introduction shown above the workflow shell",
                Mandatory = false
            }, "Page Content");

            // Group: Completion Behavior
            contentType.AddPropertyGroup("Completion Behavior");
            contentType.AddPropertyType(new PropertyType(_contentTypeService, textBox, "completionRedirectUrl")
            {
                Name = "Completion Redirect URL",
                Description = "Optional URL to redirect after workflow completion (leave blank to stay on page)",
                Mandatory = false
            }, "Completion Behavior");

            _contentTypeService.Save(contentType);
            _logger.LogInformation("WORKFLOW DEMO: Content type created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WORKFLOW DEMO: Unexpected error during setup");
        }
    }
}
```

### 5.2 Template Design (Razor View)

**File:** `src/UmbracoPrism.TestSite/Views/WorkflowDemo.cshtml`

```cshtml
@using UmbracoPrism.Core.Models
@using Umbraco.Cms.Core.Models.PublishedContent
@using Umbraco.Cms.Web.Common.PublishedModels
@inject IPrismContext PrismContext
@{
    Layout = "Master.cshtml";
    var pageTitle = Model.Value<string>("pageTitle") ?? "Workflow Demo";
    var workflowKey = Model.Value<string>("workflowKey") ?? "information-request";
    var introductionText = Model.Value<string>("introductionText");
    var completionRedirectUrl = Model.Value<string>("completionRedirectUrl");
    var tenant = PrismContext.CurrentTenant;
}

@section head {
    <style>
        .workflow-demo-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 2rem;
        }

        .workflow-demo__header {
            margin-bottom: 2rem;
        }

        .workflow-demo__title {
            font-size: 2.5rem;
            font-weight: 700;
            color: var(--prism-primary-color, #333);
            margin-bottom: 1rem;
        }

        .workflow-demo__intro {
            font-size: 1.1rem;
            line-height: 1.6;
            color: #666;
            margin-bottom: 2rem;
            white-space: pre-wrap;
        }

        .workflow-demo__shell {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
            padding: 2rem;
            min-height: 400px;
        }

        .workflow-demo__tenant-badge {
            display: inline-block;
            background: var(--prism-primary-color, #007bff);
            color: white;
            padding: 0.25rem 0.75rem;
            border-radius: 4px;
            font-size: 0.875rem;
            font-weight: 600;
            margin-bottom: 1rem;
        }
    </style>
}

<div class="workflow-demo-container">
    <div class="workflow-demo__header">
        @if (tenant != null)
        {
            <div class="workflow-demo__tenant-badge">
                @tenant.DisplayName
            </div>
        }
        <h1 class="workflow-demo__title">@pageTitle</h1>
        @if (!string.IsNullOrWhiteSpace(introductionText))
        {
            <div class="workflow-demo__intro">@introductionText</div>
        }
    </div>

    <div class="workflow-demo__shell">
        <prism-workflow-shell
            workflow-key="@workflowKey"
            tenant-code="@(tenant?.Code ?? "UNKNOWN")"
            api-base-url="http://localhost:5163/api/backoffice/workflows"
            completion-redirect-url="@completionRedirectUrl"
            enable-debug="true">
        </prism-workflow-shell>
    </div>
</div>

@section scripts {
    @* Load the Prism workflow shell web component *@
    <script type="module" src="~/prism-workflow-shell/index.js"></script>
}
```

**Design notes:**
- Uses `@inject IPrismContext` from `_ViewImports.cshtml` (already wired in TestSite)
- Reads document properties using `Model.Value<T>(alias)` pattern
- Renders `<prism-workflow-shell>` web component with attributes from document properties
- Member-protected via `[Authorize]` attribute on the route-hijacking controller (not in view)
- Tenant badge displays current tenant from `IPrismContext.CurrentTenant`
- CSS variables respect Prism tenant branding (e.g., `--prism-primary-color`)

### 5.3 Route Hijacking Controller

**File:** `src/UmbracoPrism.TestSite/Controllers/WorkflowDemoPageController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace UmbracoPrism.TestSite.Controllers;

/// <summary>
/// Route-hijacking controller for workflowDemoPage document type.
/// Enforces PrismMemberCookie authentication (member must be signed in).
/// </summary>
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowDemoPageController : RenderController
{
    public WorkflowDemoPageController(
        ILogger<WorkflowDemoPageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor)
        : base(logger, compositeViewEngine, umbracoContextAccessor)
    {
    }

    public override IActionResult Index()
    {
        // Default Umbraco render behavior — no custom logic needed
        // View: /Views/WorkflowDemo.cshtml
        return CurrentTemplate(CurrentPage);
    }
}
```

**Design notes:**
- Controller naming convention: `{DocumentTypeAlias}Controller` (no attribute needed in v17)
- Inherits `RenderController` (standard Umbraco route hijacking pattern)
- `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` enforces member authentication
- Default `Index()` action renders the associated template (WorkflowDemo.cshtml)
- No template name specified — Umbraco resolves by document type template assignment

### 5.4 Content Seeder

**File:** `src/UmbracoPrism.TestSite/WorkflowDemoSeeder.cs`

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds a Workflow Demo Page into the TestSite content tree.
/// Runs idempotently in Development only — skips if page already exists.
/// </summary>
public class WorkflowDemoSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IWebHostEnvironment _env;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<WorkflowDemoSeeder> _logger;

    public WorkflowDemoSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IWebHostEnvironment env,
        IRuntimeState runtimeState,
        ILogger<WorkflowDemoSeeder> logger)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _env = env;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run) return;
        if (!_env.IsDevelopment()) return;

        await Task.Run(SeedAsync, cancellationToken);
    }

    private void SeedAsync()
    {
        try
        {
            var contentType = _contentTypeService.Get("workflowDemoPage");
            if (contentType == null)
            {
                _logger.LogDebug("WORKFLOW DEMO SEEDER: Content type not found — skipping seed");
                return;
            }

            // Check if page already exists
            var existing = _contentService.GetRootContent()
                .FirstOrDefault(c => c.ContentType.Alias == "workflowDemoPage" && c.Name == "Workflow Demo");

            if (existing != null)
            {
                _logger.LogDebug("WORKFLOW DEMO SEEDER: Page already exists — skipping seed");
                return;
            }

            // Create new page
            var page = _contentService.Create("Workflow Demo", Constants.System.Root, "workflowDemoPage");

            page.SetValue("workflowKey", "information-request");
            page.SetValue("pageTitle", "Workflow Forms Engine Demo");
            page.SetValue("introductionText",
                "Welcome to the Prism Workflow Forms Engine demo!\n\n" +
                "This page demonstrates a complete workflow-driven form lifecycle:\n" +
                "• Collect applicant and request details\n" +
                "• Submit for review\n" +
                "• Operator approval simulation\n" +
                "• Completion with audit trail\n\n" +
                "All workflow logic is defined in the workflow definition — the UI only renders what the runtime instructs.");
            page.SetValue("completionRedirectUrl", "");

            _contentService.SaveAndPublish(page);
            _logger.LogInformation("WORKFLOW DEMO SEEDER: Page created and published");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WORKFLOW DEMO SEEDER: Unexpected error — safe to ignore");
        }
    }
}
```

**Design notes:**
- Follows the same pattern as `DemoMobileNavSeeder.cs` and `VinylVaultSeeder.cs`
- Runs on `UmbracoApplicationStartedNotification` in Development mode only
- Idempotent: checks for existing page before creating
- Uses `_contentService.SaveAndPublish()` to make page immediately visible
- Logs all actions for troubleshooting

### 5.5 Composer Registration

All seeders and content type handlers must be registered in `TestSiteComposer.cs`:

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoPrism.TestSite;

public class TestSiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Existing registrations
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, VinylVaultContentTypes>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, VinylVaultSeeder>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, MobileNavSchemaSetup>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoMobileNavSeeder>();
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>();

        // Add workflow demo registrations
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WorkflowDemoContentType>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WorkflowDemoSeeder>();
    }
}
```

---

## 6) Governance Boundaries and Security Rules

### 6.1 Ownership Boundaries

**MockBackOffice Owns:**
- Workflow emulator runtime (`RuntimeMode = Emulator`)
- Operator persona simulation and task queue UI
- Demo-only convenience endpoints (e.g., auto-assignment, fast-forward)
- JSON seed pack loading and registration
- Emulator-scoped DTOs and contracts under `UmbracoPrism.MockBackOffice.Workflow.*`

**MockBackOffice MUST Delegate to Core Runtime:**
- Authoritative workflow transition rules (guards, validation)
- Field-group schema validation and versioning
- State machine execution semantics
- Audit event persistence strategy
- Production-grade concurrency control (optimistic locking, ETag)

**TestSite Owns:**
- CMS-native document type definitions (code-first)
- Razor views and route-hijacking controllers
- Member authentication enforcement (`[Authorize]` on controllers)
- Content seeding and demo page initialization
- Rendering integration with `<prism-workflow-shell>` web component

**Core Runtime Owns (UmbracoPrism.Core):**
- Workflow definition and instance storage contracts
- Transition evaluation and guard execution
- Field-group validation pipeline
- Canonical render payload generation
- HTTP status and `responseState` mapping
- Security enforcement (auth, tenant, role checks)

### 6.2 Emulator-Only Extensions MUST Be Namespaced

All emulator-specific features must be clearly namespaced and never leak into Core runtime contracts:

**Allowed in Emulator:**
- `OperatorPersona` (emulator-only concept for demo personas)
- `WorkflowTaskQueue` (simplified task queue for operator simulation)
- `AutoAssignmentPolicy` (demo convenience for auto-claiming tasks)
- `FastForwardTransition` (demo shortcut for skipping wait states)

**NOT Allowed in Core Runtime Contracts:**
- Any reference to "persona" (use "actor" in Core)
- Any "auto-assignment" or "fast-forward" semantics
- Any emulator-specific metadata in `WorkflowInstance` or `WorkflowEvent`

**Contract Sharing Strategy:**
- Shared DTOs live in `UmbracoPrism.Core.Workflow.Contracts` namespace
- Emulator-only DTOs live in `UmbracoPrism.MockBackOffice.Workflow.Models` namespace
- MockBackOffice references Core contracts via project reference
- Core NEVER references MockBackOffice (one-way dependency)

### 6.3 Security Guards MUST Always Run in Core

Even when initiated from emulator UI, security-sensitive operations must execute in Core runtime:

**Examples:**
- Guard evaluation (e.g., `requires-approval-authority`) must run in Core
- Role claim validation must use Core's tenant-scoped claim resolution
- Optimistic concurrency checks (`stateVersion` validation) must run in Core
- Field-group validation must use Core's schema engine

**MockBackOffice Enforcement:**
- In `RuntimeMode = Emulator`, the emulator service MUST replicate Core's auth/guard logic exactly
- In `RuntimeMode = Core`, all security checks proxy to Core endpoints (no local validation)
- MockBackOffice MUST validate JWT Bearer token on all workflow endpoints
- MockBackOffice MUST resolve Prism tenant from claims before processing workflow requests

**Testing Strategy:**
- Contract tests run against both Emulator and Core modes
- Security tests validate that guard bypass is impossible in both modes
- Integration tests verify that emulator behavior matches Core fidelity

### 6.4 State Persistence Rules

**Emulator Mode (Development/Demo):**
- State stored in-memory only (ConcurrentDictionary or similar)
- No database persistence required
- State resets on application restart (intentional — demo-only)
- Seed packs reload on every startup

**Core Mode (Production/Fidelity Testing):**
- State persisted per Core runtime's storage strategy (likely SQL/SQLite)
- Optimistic concurrency enforced via ETag or `stateVersion`
- Audit events append-only (immutable history)
- Migrations supported for definition version changes

**Mixed-Mode Consideration:**
- MockBackOffice queue state (operator personas, task assignments) is always local
- Core runtime instance state is always authoritative when `RuntimeMode = Core`
- No attempt to sync emulator queue with Core queue (separate concerns)

---

## 7) Implementation Checklist

### Phase 1: MockBackOffice Extension
- [ ] Add `WorkflowEmulatorOptions` configuration class
- [ ] Implement `IWorkflowRuntimeService` interface and `WorkflowEmulatorService`
- [ ] Implement `IWorkflowQueueService` interface and `WorkflowEmulatorQueueService`
- [ ] Implement `WorkflowEmulatorController` with all endpoints (§2.4)
- [ ] Add DI registration logic in `Program.cs` with `RuntimeMode` toggle
- [ ] Write unit tests for emulator state machine transitions

### Phase 2: Seed Pack Support
- [ ] Create `workflow-seeds/` directory in MockBackOffice project
- [ ] Write `information-request-v1.json` seed pack (§3.1)
- [ ] Write `operator-personas.json` seed pack (§3.1)
- [ ] Implement `IWorkflowSeedLoader` and `WorkflowSeedLoader` (§3.2)
- [ ] Add seed loading logic to `Program.cs` startup
- [ ] Test seed loading and definition registration

### Phase 3: HTTP Demo Script
- [ ] Create `demo/workflow-demo.http` file (§4)
- [ ] Validate all endpoints with manual execution
- [ ] Add inline comments for expected responses
- [ ] Document token acquisition process
- [ ] Add rejection and request-changes scenarios

### Phase 4: TestSite Integration
- [ ] Implement `WorkflowDemoContentType.cs` seeder (§5.1)
- [ ] Create `Views/WorkflowDemo.cshtml` template (§5.2)
- [ ] Implement `WorkflowDemoPageController.cs` (§5.3)
- [ ] Implement `WorkflowDemoSeeder.cs` content seeder (§5.4)
- [ ] Register handlers in `TestSiteComposer.cs` (§5.5)
- [ ] Test end-to-end: create page → authenticate → render workflow shell

### Phase 5: Contract Tests
- [ ] Write contract tests that run against both Emulator and Core modes
- [ ] Validate render payload shape matches specification
- [ ] Validate HTTP status codes and `responseState` values
- [ ] Validate guard enforcement (security)
- [ ] Validate optimistic concurrency (stateVersion)

### Phase 6: Documentation
- [ ] Update `README.md` with workflow demo instructions
- [ ] Add screenshots to `docs/design/workflow-forms-engine-screenshots/`
- [ ] Document seed pack schema
- [ ] Document operator persona configuration
- [ ] Document RuntimeMode toggle behavior

---

## 8) Open Questions and Decisions

### 8.1 Resolved Decisions

1. **RuntimeMode toggle location:** Configuration-based (appsettings.json), not environment variable
   - Rationale: Easier to maintain multiple demo scenarios, clearer intent

2. **Seed pack format:** JSON (not YAML or C# builders)
   - Rationale: Standard, cross-language, easy to version control and share

3. **Operator personas:** Emulator-only concept, not in Core contracts
   - Rationale: Production systems use real actor identities; personas are demo convenience

4. **Queue simulation:** Local to MockBackOffice, not synced with Core
   - Rationale: Separate concerns; Core owns authoritative workflow state only

5. **Member authentication:** `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` on controller, not view
   - Rationale: v17 best practice; security enforcement at route-hijacking layer

### 8.2 Open Questions for Core Implementation

1. **Workflow definition storage:** Should Core use Umbraco content nodes, dedicated SQL tables, or hybrid?
   - Recommendation: Dedicated tables for v1 (cleaner separation, easier versioning)

2. **Field-group validation:** Should Core use FluentValidation, System.ComponentModel.DataAnnotations, or custom?
   - Recommendation: Custom validation engine aligned with workflow schema DSL

3. **Optimistic concurrency:** Should Core use ETag header, stateVersion property, or both?
   - Recommendation: Both — ETag for HTTP cache semantics, stateVersion for workflow semantics

4. **Actor model:** Should Core support role-based and user-assignment, or role-based only for v1?
   - Recommendation: Role-based only for v1 demo; user-assignment in future iteration

5. **Audit timeline storage:** Eventual consistency or transactional with state transitions?
   - Recommendation: Transactional for v1 (simpler, safer); eventual consistency in production scale

---

## 9) References

- [Prism Workflow Forms Engine Demo Proposal](./workflow-forms-engine-demo.md) — Core runtime contracts
- [Umbraco v17 Documentation](https://docs.umbraco.com/) — Route hijacking and document types
- [RenderController Pattern](https://docs.umbraco.com/umbraco-cms/reference/routing/custom-controllers) — Umbraco controller conventions
- [MockBackOffice Implementation](../../src/UmbracoPrism.MockBackOffice/) — Existing patterns
- [TestSite Seeder Examples](../../src/UmbracoPrism.TestSite/) — VinylVaultSeeder, DemoMobileNavSeeder

---

**END OF DOCUMENT**
