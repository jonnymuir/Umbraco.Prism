# Prism Workflow Forms Engine — Security Design

> **⚠️ v2.0 Schema Update:** Security architecture is unchanged in v2.0. Component model changes do not affect security layer.

**Author:** Copper (Security Engineer)  
**Requested by:** Jonny Muir  
**Status:** Security Architecture & Threat Model  
**Date:** 2026-04-08  
**Related:** [workflow-forms-engine-demo.md](./workflow-forms-engine-demo.md)

---

## Executive Summary

This document defines the security architecture for the Prism Workflow Forms Engine, focusing on tenant isolation, authorization boundaries, and threat mitigation. The workflow engine is a security-critical component that manages multi-tenant workflow instances containing PII and audit trails. Every endpoint MUST enforce tenant isolation, actor authorization, and concurrency controls.

**Key Security Principles:**
1. **Defense in Depth:** Tenant isolation at DB query level, authorization at transition level, concurrency at state level
2. **Fail Secure:** Existence concealment (404 not 403), explicit actor checks, no emulator bypass paths
3. **Audit Integrity:** Append-only WorkflowEvent log, immutable once written, never deletable via API
4. **Least Privilege:** Actor roles limit transition eligibility, operators require role claims, members own instances

---

## 1. Threat Model

### 1.1 Threat Surface

The workflow engine exposes the following attack surfaces:

**API Endpoints (All under `/umbraco/prism/workflows/`):**
- `POST /instances` — Create workflow instance
- `GET /instances/{id}/render` — Get current state and UI payload
- `POST /instances/{id}/submit/{fieldGroupKey}` — Submit field group data
- `POST /instances/{id}/actions/{actionKey}` — Trigger state transition
- `GET /instances/{id}/timeline` — Get audit trail

**Emulator Endpoints (MockBackOffice only):**
- `/api/backoffice/workflows/*` — Definition CRUD, queue simulation, operator decisions
- MUST NOT leak into production; MUST NOT bypass authorization

### 1.2 Actors

| Actor Type | Identity | Privileges | Trust Level |
|------------|----------|------------|-------------|
| **Member** | Authenticated Prism member (OIDC/Entra) | Own their instances, submit field groups, view their timeline | Medium (authenticated, same tenant) |
| **Operator** | Authenticated backoffice user with role claim | Act on assigned tasks, approve/reject/request-changes, view queue | High (staff role) |
| **Unauthenticated User** | No identity | None — should receive 401 on all workflow endpoints | Untrusted |
| **Emulator/Demo** | MockBackOffice simulated operator | Demo-only; simulate decisions but flow through Core runtime | Development-only (zero trust in production) |

### 1.3 Assets to Protect

| Asset | Sensitivity | Protection Requirements |
|-------|-------------|------------------------|
| **WorkflowInstance** | High | Tenant-scoped, owner-scoped, existence concealment across tenants |
| **FieldGroupSubmission** | High (may contain PII) | Tenant-scoped, encrypted at rest (optional), no exposure in timeline |
| **WorkflowEvent** | Critical (audit log) | Append-only, immutable, integrity-critical, never deletable |
| **WorkflowDefinition** | Medium (integrity-critical) | Read-only via member API, CRUD only via operator/admin endpoints |
| **WorkflowTask Queue** | Medium | Operator-only visibility, tenant-scoped, no cross-tenant queue leakage |

### 1.4 Threat Scenarios

| ID | Threat | Impact | Likelihood | Priority |
|----|--------|--------|------------|----------|
| **T1** | Cross-tenant instance access (IDOR on `instanceId`) | High — PII leakage, unauthorized state manipulation | High | **Critical** |
| **T2** | Submitting field group data to another user's instance | High — data poisoning, unauthorized state change | Medium | **High** |
| **T3** | Triggering invalid transition for actor role | Medium — workflow bypass, unauthorized approvals | Medium | **High** |
| **T4** | Emulator endpoints leaking into production | High — complete auth bypass, definition tampering | Low | **Critical** |
| **T5** | Concurrency race: two actors transition simultaneously | Medium — state corruption, lost updates | Medium | **Medium** |
| **T6** | Audit trail tampering (delete/modify WorkflowEvents) | Critical — compliance violation, forensic loss | Low | **Critical** |
| **T7** | Definition tampering via emulator affects live instances | High — workflow logic compromise | Low | **High** |
| **T8** | Information leakage in error responses | Low — existence disclosure to wrong tenant | High | **Medium** |

---

## 2. Tenant Isolation Design

### 2.1 Principles

**Every workflow API endpoint MUST enforce tenant isolation at the database query level.**

- No instance, task, or event should be retrievable without tenant scope.
- Use `IPrismContext.CurrentTenant.Id` in ALL DB lookups (no exceptions).
- Return **404 Not Found** (not 403 Forbidden) when instance exists but belongs to different tenant (existence concealment).
- Log tenant mismatches as security events for monitoring.

### 2.2 Tenant Guard Service

Introduce `IWorkflowTenantGuard` to centralise tenant isolation checks:

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Enforces tenant isolation for workflow instance access.
/// Prevents cross-tenant IDOR attacks by scoping all instance lookups to the current tenant.
/// </summary>
public interface IWorkflowTenantGuard
{
    /// <summary>
    /// Retrieves a workflow instance, enforcing tenant isolation.
    /// Returns null if the instance does not exist OR belongs to a different tenant.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <returns>The workflow instance if found and tenant-scoped; otherwise null.</returns>
    Task<WorkflowInstance?> GetInstanceForCurrentTenantAsync(Guid instanceId);

    /// <summary>
    /// Verifies that a workflow instance belongs to the current tenant.
    /// Use this before mutation operations to avoid leaking existence via different error codes.
    /// </summary>
    /// <param name="instanceId">The workflow instance ID.</param>
    /// <returns>True if the instance exists and belongs to the current tenant; otherwise false.</returns>
    Task<bool> InstanceBelongsToCurrentTenantAsync(Guid instanceId);

    /// <summary>
    /// Retrieves a workflow task, enforcing tenant isolation.
    /// Returns null if the task does not exist OR belongs to a different tenant's instance.
    /// </summary>
    /// <param name="taskId">The workflow task ID.</param>
    /// <returns>The workflow task if found and tenant-scoped; otherwise null.</returns>
    Task<WorkflowTask?> GetTaskForCurrentTenantAsync(Guid taskId);
}
```

### 2.3 Implementation Pattern

**Reference implementation (following existing Prism patterns):**

```csharp
public class WorkflowTenantGuard : IWorkflowTenantGuard
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;
    private readonly IPrismContext _prismContext;
    private readonly ILogger<WorkflowTenantGuard> _logger;

    public async Task<WorkflowInstance?> GetInstanceForCurrentTenantAsync(Guid instanceId)
    {
        var tenant = _prismContext.CurrentTenant;
        if (tenant == null)
        {
            _logger.LogWarning("Workflow instance access without tenant context");
            return null;
        }

        var tenantId = tenant.Id.ToString();
        using var db = _databaseFactory.CreateDatabase();

        // CRITICAL: Always include tenantId in WHERE clause
        var instance = await db.FirstOrDefaultAsync<WorkflowInstanceSchema>(
            "WHERE InstanceId = @0 AND TenantId = @1", instanceId, tenantId);

        if (instance == null)
        {
            // Do NOT log whether instance exists in other tenant (information leakage)
            _logger.LogDebug("Workflow instance {InstanceId} not found for tenant {TenantId}", 
                instanceId, tenantId);
        }

        return instance != null ? MapToModel(instance) : null;
    }
}
```

### 2.4 Controller Usage Pattern

**All workflow controllers MUST use this pattern:**

```csharp
[HttpGet("instances/{instanceId}/render")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[Authorize(Policy = "PrismTenant")] // Enforces tenant token match
public async Task<IActionResult> Render(Guid instanceId)
{
    // 1. Tenant-scoped retrieval (returns null for wrong tenant OR non-existent)
    var instance = await _tenantGuard.GetInstanceForCurrentTenantAsync(instanceId);

    // 2. Return 404 (NOT 403) — do not reveal existence to wrong tenant
    if (instance == null)
    {
        return NotFound();
    }

    // 3. Proceed with business logic
    var renderPayload = await _workflowService.BuildRenderPayloadAsync(instance);
    return Ok(renderPayload);
}
```

**Key properties:**
- Single source of truth for tenant checks
- Consistent 404 response (existence concealment)
- Logging at service layer (not controller)
- Reusable across all workflow endpoints

---

## 3. Authorization Model

### 3.1 Actor Roles

Workflow transitions are restricted by **WorkflowActor** role:

```csharp
namespace UmbracoPrism.Core.Models;

[Flags]
public enum WorkflowActor
{
    /// <summary>
    /// The member who owns the workflow instance (initiated the request).
    /// </summary>
    Member = 1 << 0,

    /// <summary>
    /// A backoffice operator/reviewer with role-based access.
    /// Requires authenticated backoffice user with operator role claim.
    /// </summary>
    Operator = 1 << 1,

    /// <summary>
    /// System-initiated transition (e.g., timeout, scheduled event).
    /// Not callable via API endpoint.
    /// </summary>
    System = 1 << 2
}
```

### 3.2 Transition Authorization

Each `WorkflowTransition` MUST declare allowed actors:

```csharp
public class WorkflowTransition
{
    public string TransitionKey { get; set; } = string.Empty;
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    
    /// <summary>
    /// Actor roles allowed to trigger this transition.
    /// Checked at runtime before executing transition logic.
    /// </summary>
    public WorkflowActor AllowedActors { get; set; }

    // Guards, field group requirements, etc.
}
```

### 3.3 Actor Authorization Service

Introduce `IWorkflowActorAuthorizationService`:

```csharp
namespace UmbracoPrism.Core.Services;

public interface IWorkflowActorAuthorizationService
{
    /// <summary>
    /// Determines the current authenticated user's workflow actor role.
    /// </summary>
    /// <returns>WorkflowActor flags for the current user.</returns>
    WorkflowActor GetCurrentActorRole();

    /// <summary>
    /// Verifies that the current actor is authorized to trigger a transition.
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <param name="transition">The transition being attempted.</param>
    /// <returns>True if authorized; otherwise false.</returns>
    Task<bool> IsAuthorizedForTransitionAsync(WorkflowInstance instance, WorkflowTransition transition);

    /// <summary>
    /// Verifies that the current user owns the workflow instance (member role).
    /// </summary>
    /// <param name="instance">The workflow instance.</param>
    /// <returns>True if the current user is the instance owner; otherwise false.</returns>
    bool IsInstanceOwner(WorkflowInstance instance);
}
```

### 3.4 Authorization Checks

**Reference implementation:**

```csharp
public class WorkflowActorAuthorizationService : IWorkflowActorAuthorizationService
{
    private readonly IPrismUserContext _userContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WorkflowActor GetCurrentActorRole()
    {
        if (!_userContext.IsAuthenticated)
            return 0; // No role for unauthenticated

        // Check if user has operator role claim
        var isOperator = _httpContextAccessor.HttpContext?.User
            .HasClaim("role", "prism-operator") ?? false;

        return isOperator ? WorkflowActor.Operator : WorkflowActor.Member;
    }

    public async Task<bool> IsAuthorizedForTransitionAsync(
        WorkflowInstance instance, WorkflowTransition transition)
    {
        var currentRole = GetCurrentActorRole();

        // No role = unauthorized
        if (currentRole == 0)
            return false;

        // Check if current role is in allowed actors
        if (!transition.AllowedActors.HasFlag(currentRole))
            return false;

        // Additional check for Member role: must own the instance
        if (currentRole == WorkflowActor.Member && !IsInstanceOwner(instance))
            return false;

        return true;
    }

    public bool IsInstanceOwner(WorkflowInstance instance)
    {
        if (!_userContext.IsAuthenticated)
            return false;

        // Instance owner is determined by MemberId claim match
        var currentMemberId = _userContext.MemberId;
        return currentMemberId != null && currentMemberId == instance.CreatedByMemberId;
    }
}
```

### 3.5 Controller Authorization Enforcement

**All transition endpoints MUST check authorization:**

```csharp
[HttpPost("instances/{instanceId}/actions/{actionKey}")]
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
[Authorize(Policy = "PrismTenant")]
public async Task<IActionResult> ExecuteAction(Guid instanceId, string actionKey)
{
    // 1. Tenant guard
    var instance = await _tenantGuard.GetInstanceForCurrentTenantAsync(instanceId);
    if (instance == null) return NotFound();

    // 2. Get transition definition
    var transition = await _workflowService.GetTransitionAsync(instance, actionKey);
    if (transition == null) return NotFound();

    // 3. Authorization check
    var isAuthorized = await _actorAuthService.IsAuthorizedForTransitionAsync(instance, transition);
    if (!isAuthorized)
    {
        _logger.LogWarning(
            "Unauthorized transition attempt: {ActionKey} on instance {InstanceId} by {UserId}",
            actionKey, instanceId, _userContext.MemberId);
        return Forbid(); // 403 here is appropriate (user knows instance exists, lacks permission)
    }

    // 4. Execute transition
    var result = await _workflowService.ExecuteTransitionAsync(instance, transition);
    return Ok(result);
}
```

**Key distinction:**
- **404** when instance not found or wrong tenant (existence concealment)
- **403** when instance found, tenant matches, but actor role insufficient (user knows it exists)

---

## 4. Emulator Security Boundary

### 4.1 Risk Assessment

**Emulator endpoints are the highest-risk component** because they:
- Simulate backoffice operator decisions
- May have convenient "skip auth" paths for demo purposes
- Could leak into production if not properly gated
- Could bypass Core runtime authorization if calling internal services directly

### 4.2 Hard Requirements

**MUST NOT:**
- Bypass `IWorkflowTenantGuard` or `IWorkflowActorAuthorizationService` checks
- Create workflow instances for tenants other than the configured demo tenant
- Modify `WorkflowDefinition` in a way that affects live Core runtime instances
- Expose endpoints in `IWebHostEnvironment.IsProduction()`

**MUST:**
- Check environment at the start of EVERY emulator action method
- Flow ALL operator decisions through `IWorkflowInstanceService` (not direct DB writes)
- Use `[ApiExplorerSettings(IgnoreApi = true)]` to hide from OpenAPI in production
- Scope to demo tenant only (configurable via `appsettings`)

### 4.3 EmulatorOnly Attribute Filter

**Recommended implementation:**

```csharp
namespace UmbracoPrism.MockBackOffice.Filters;

/// <summary>
/// Authorization filter that restricts endpoint access to Development environment only.
/// Returns 404 Not Found in non-Development environments to prevent emulator endpoint leakage.
/// </summary>
public class EmulatorOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var env = context.HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>();

        if (!env.IsDevelopment())
        {
            // Return 404 (not 403) — do not reveal emulator endpoint existence in production
            context.Result = new NotFoundResult();
        }

        base.OnActionExecuting(context);
    }
}
```

### 4.4 Emulator Controller Pattern

**All MockBackOffice workflow controllers MUST use this pattern:**

```csharp
namespace UmbracoPrism.MockBackOffice.Controllers;

[Route("api/backoffice/workflows")]
[ApiExplorerSettings(IgnoreApi = true)] // Hide from OpenAPI/Swagger
[EmulatorOnly] // Block in production
public class WorkflowEmulatorController : Controller
{
    private readonly IWorkflowInstanceService _workflowService;
    private readonly IWorkflowTenantGuard _tenantGuard;
    private readonly IWorkflowActorAuthorizationService _actorAuthService;
    private readonly IPrismContext _prismContext;

    [HttpPost("operator/approve/{instanceId}")]
    public async Task<IActionResult> SimulateApproval(Guid instanceId, 
        [FromBody] OperatorDecisionRequest request)
    {
        // 1. Verify demo tenant context
        var tenant = _prismContext.CurrentTenant;
        if (tenant == null || !tenant.IsDemo)
        {
            return BadRequest(new { error = "Emulator only works with demo tenant." });
        }

        // 2. Use tenant guard (no bypass)
        var instance = await _tenantGuard.GetInstanceForCurrentTenantAsync(instanceId);
        if (instance == null) return NotFound();

        // 3. Simulate operator actor (but flow through authorization service)
        // DO NOT skip authorization checks — simulate legitimate operator role
        var approvalResult = await _workflowService.ExecuteOperatorDecisionAsync(
            instance, "approve", request.Notes);

        return Ok(approvalResult);
    }
}
```

**Critical properties:**
- `[EmulatorOnly]` at class level (all actions protected)
- `[ApiExplorerSettings(IgnoreApi = true)]` (hide from discovery)
- Demo tenant check at method start
- Uses Core services, not direct DB access
- Authorization still flows through `IWorkflowActorAuthorizationService`

### 4.5 Emulator Configuration

**Recommended `appsettings.Development.json` pattern:**

```json
{
  "Prism": {
    "Workflow": {
      "Emulator": {
        "Enabled": true,
        "DemoTenantId": "demo-tenant-123",
        "SimulatedOperators": [
          {
            "OperatorId": "op-001",
            "DisplayName": "Demo Reviewer",
            "RoleClaims": ["prism-operator"]
          }
        ]
      }
    }
  }
}
```

**Production config MUST NOT enable emulator:**

```json
{
  "Prism": {
    "Workflow": {
      "Emulator": {
        "Enabled": false
      }
    }
  }
}
```

### 4.6 Emulator Observability

**All emulator actions MUST log:**
- Simulated operator identity
- Target instance ID and tenant
- Decision type (approve/reject/request-changes)
- Correlation ID for tracing

**Recommended log structure:**

```csharp
_logger.LogInformation(
    "[EMULATOR] Operator {OperatorId} simulated {DecisionType} on instance {InstanceId} " +
    "in tenant {TenantId} (CorrelationId: {CorrelationId})",
    operatorId, decisionType, instanceId, tenantId, correlationId);
```

---

## 5. Optimistic Concurrency Control

### 5.1 Security Relevance

The `stateVersion` / ETag mechanism is **not just UX — it's a security and integrity control**:

- Prevents TOCTOU (Time-Of-Check/Time-Of-Use) race conditions
- Ensures state transitions are atomic and based on current state
- Protects against lost-update problems where two actors transition simultaneously
- Guards audit integrity by preventing state changes on stale reads

### 5.2 Version Enforcement

**Every mutating operation MUST include stateVersion:**

```csharp
[HttpPost("instances/{instanceId}/submit/{fieldGroupKey}")]
public async Task<IActionResult> SubmitFieldGroup(
    Guid instanceId, 
    string fieldGroupKey,
    [FromBody] FieldGroupSubmissionRequest request)
{
    // 1. Tenant guard
    var instance = await _tenantGuard.GetInstanceForCurrentTenantAsync(instanceId);
    if (instance == null) return NotFound();

    // 2. Version check (CRITICAL for concurrency safety)
    if (request.StateVersion != instance.StateVersion)
    {
        _logger.LogWarning(
            "Stale state version on submit: expected {Expected}, got {Actual} " +
            "for instance {InstanceId}",
            instance.StateVersion, request.StateVersion, instanceId);

        return Conflict(new
        {
            error = "State version mismatch. The workflow state has changed.",
            expectedVersion = instance.StateVersion,
            providedVersion = request.StateVersion
        });
    }

    // 3. Proceed with submission (atomically increment version in DB)
    var result = await _workflowService.SubmitFieldGroupAsync(instance, fieldGroupKey, request);
    return Ok(result);
}
```

### 5.3 Database-Level Version Increment

**Use atomic UPDATE with WHERE clause to prevent races:**

```sql
-- Safe atomic version increment (prevents double-transition)
UPDATE PrismWorkflowInstances
SET 
    CurrentState = @newState,
    StateVersion = StateVersion + 1,
    UpdatedAt = @now
WHERE 
    InstanceId = @instanceId 
    AND TenantId = @tenantId
    AND StateVersion = @expectedVersion;

-- Check affected rows: if 0, version mismatch occurred
```

**Service layer pattern:**

```csharp
public async Task<WorkflowTransitionResult> ExecuteTransitionAsync(
    WorkflowInstance instance, WorkflowTransition transition)
{
    using var db = _databaseFactory.CreateDatabase();

    // Atomic state transition with version check
    var affectedRows = await db.ExecuteAsync(
        @"UPDATE PrismWorkflowInstances 
          SET CurrentState = @newState, 
              StateVersion = StateVersion + 1, 
              UpdatedAt = @now
          WHERE InstanceId = @instanceId 
            AND TenantId = @tenantId 
            AND StateVersion = @expectedVersion",
        new
        {
            newState = transition.ToState,
            now = DateTime.UtcNow,
            instanceId = instance.InstanceId,
            tenantId = instance.TenantId,
            expectedVersion = instance.StateVersion
        });

    if (affectedRows == 0)
    {
        // Version mismatch or instance deleted
        _logger.LogWarning(
            "Version conflict on transition {TransitionKey} for instance {InstanceId}",
            transition.TransitionKey, instance.InstanceId);

        return WorkflowTransitionResult.Conflict();
    }

    // Append audit event (separate transaction or same)
    await AppendWorkflowEventAsync(instance.InstanceId, transition.TransitionKey);

    return WorkflowTransitionResult.Success(instance.StateVersion + 1);
}
```

### 5.4 Response Format on Conflict

**HTTP 409 Conflict response:**

```json
{
  "instanceId": "wf_123",
  "responseState": "error",
  "stateVersion": null,
  "problems": [
    {
      "type": "StateVersionMismatch",
      "title": "Workflow state has changed",
      "detail": "Your request was based on version 5, but the current version is 7. Please refresh and try again.",
      "expectedVersion": 7,
      "providedVersion": 5
    }
  ]
}
```

---

## 6. PII and Data Sensitivity

### 6.1 Data Classification

**FieldGroupSubmission values may contain PII:**
- Date of birth
- Contact details (email, phone, address)
- Identity documents (passport, driver's license numbers)
- Financial information (income, bank details)

**Regulatory considerations:**
- GDPR (Europe): Right to erasure, encryption requirements, breach notification
- CCPA (California): Consumer data rights, security obligations
- SOC 2: Confidentiality and privacy criteria

### 6.2 Encryption at Rest

**Option A: Encrypt field values using RefreshTokenEncryptionService pattern**

**Recommended approach:**

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// AES-256-GCM authenticated encryption for workflow field group submission values.
/// Wire format: Base64([12-byte nonce][ciphertext][16-byte tag]).
/// Follows the same proven pattern as RefreshTokenEncryptionService.
/// </summary>
public class FieldGroupEncryptionService : IFieldGroupEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32; // 256 bits

    private readonly byte[] _key;

    public FieldGroupEncryptionService(IOptions<PrismWorkflowOptions> options)
    {
        var keyString = options.Value.FieldEncryptionKey;

        if (string.IsNullOrWhiteSpace(keyString))
            throw new InvalidOperationException(
                "Prism: Workflow field encryption key must be configured. " +
                "Set 'Prism:Workflow:FieldEncryptionKey' (base64-encoded 32-byte key).");

        _key = Convert.FromBase64String(keyString);

        if (_key.Length != KeySize)
            throw new InvalidOperationException(
                $"Prism: Workflow field encryption key must be exactly {KeySize} bytes.");
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);

        var data = Convert.FromBase64String(ciphertext);

        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted field data is too short.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(data.Length - TagSize);
        var encrypted = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);

        var plaintext = new byte[encrypted.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
```

**Storage schema:**

```csharp
public class FieldGroupSubmissionSchema
{
    public Guid SubmissionId { get; set; }
    public Guid InstanceId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string FieldGroupKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Encrypted JSON of field values. Encrypted at rest using AES-256-GCM.
    /// </summary>
    public string EncryptedValues { get; set; } = string.Empty;
    
    public DateTime SubmittedAt { get; set; }
    public string SubmittedByMemberId { get; set; } = string.Empty;
}
```

**Encryption flow:**

```csharp
public async Task SubmitFieldGroupAsync(
    WorkflowInstance instance, 
    string fieldGroupKey, 
    Dictionary<string, object> fieldValues)
{
    // Serialize field values to JSON
    var valuesJson = JsonSerializer.Serialize(fieldValues);

    // Encrypt before storage
    var encryptedValues = _fieldEncryptionService.Encrypt(valuesJson);

    var submission = new FieldGroupSubmissionSchema
    {
        SubmissionId = Guid.NewGuid(),
        InstanceId = instance.InstanceId,
        TenantId = instance.TenantId,
        FieldGroupKey = fieldGroupKey,
        EncryptedValues = encryptedValues,
        SubmittedAt = DateTime.UtcNow,
        SubmittedByMemberId = _userContext.MemberId
    };

    using var db = _databaseFactory.CreateDatabase();
    await db.InsertAsync(submission);
}
```

**Option B: Document decision NOT to encrypt**

If simplicity is prioritized for the demo, document the explicit decision:

```markdown
### Encryption Decision (v1 Demo)

**Decision:** Field group values are stored in plaintext (JSON) in the database.

**Rationale:**
1. Demo scope — not handling real PII in v1
2. Database-level encryption at rest (TDE) provides baseline protection
3. Tenant isolation + authorization prevents cross-tenant access
4. Simplifies development and debugging for demo phase
5. Can be added in Phase 2 without schema migration (encrypt in place)

**Constraints:**
- v1 deployment MUST NOT process real user PII
- Demo data MUST use synthetic/fake values only
- Production readiness gate requires encryption at rest before real PII

**Upgrade path:**
- Add `IFieldGroupEncryptionService` in Phase 2
- Migrate plaintext values: read → encrypt → write back
- Update submission service to encrypt on write, decrypt on read
```

**Recommendation for Prism:** Use **Option A** (encryption) because:
1. Prism is marketed as multi-tenant security-focused
2. Reuses proven RefreshTokenEncryptionService pattern (low risk)
3. Establishes security posture from day one (no "we'll add it later" technical debt)
4. Minimal performance impact (field groups are small)

### 6.3 Timeline Endpoint Data Exposure

**Timeline endpoint MUST NOT return raw field values:**

```csharp
[HttpGet("instances/{instanceId}/timeline")]
public async Task<IActionResult> GetTimeline(Guid instanceId)
{
    // 1. Tenant guard
    var instance = await _tenantGuard.GetInstanceForCurrentTenantAsync(instanceId);
    if (instance == null) return NotFound();

    // 2. Authorization check (members can view their own timeline, operators all)
    var canView = _actorAuthService.IsInstanceOwner(instance) ||
                  _actorAuthService.GetCurrentActorRole().HasFlag(WorkflowActor.Operator);

    if (!canView) return Forbid();

    // 3. Build timeline with metadata only (NO raw field values)
    var events = await _workflowService.GetTimelineEventsAsync(instance.InstanceId);

    var timeline = events.Select(e => new TimelineEventDto
    {
        EventId = e.EventId,
        EventType = e.EventType,
        OccurredAt = e.OccurredAt,
        ActorType = e.ActorType,
        ActorDisplayName = e.ActorDisplayName,
        
        // Metadata only — which field group was submitted, not the values
        FieldGroupKey = e.FieldGroupKey,
        FieldGroupSubmittedAt = e.FieldGroupSubmittedAt,
        
        // DO NOT INCLUDE:
        // FieldValues = e.DecryptedFieldValues  ❌ NEVER expose PII in timeline
        
        StateTransition = e.StateTransition,
        Notes = e.Notes
    });

    return Ok(new { instanceId, timeline });
}
```

**Key principle:** Timeline is for audit/progress visibility, not PII retrieval.

---

## 7. Security Test Checklist

### 7.1 Pre-Production Security Gate

**The following tests MUST pass before the workflow engine enters any production-like environment:**

#### Tenant Isolation Tests

- [ ] **T1.1:** Cross-tenant instance access attempt returns 404 (not 403, not 200)
  - Create instance in tenant A, attempt access from tenant B
  - Expected: 404 Not Found
  - Verify: No instance existence revealed, no data leaked

- [ ] **T1.2:** Cross-tenant instance mutation attempt returns 404
  - Create instance in tenant A, attempt submit/action from tenant B
  - Expected: 404 Not Found
  - Verify: State unchanged, event not logged

- [ ] **T1.3:** Cross-tenant task queue visibility
  - Create task in tenant A, query queue from tenant B
  - Expected: Task not visible in B's queue
  - Verify: No task metadata leaked

#### Authorization Tests

- [ ] **T2.1:** Unauthenticated user accessing instance returns 401
  - Attempt GET /render without authentication
  - Expected: 401 Unauthorized
  - Verify: No instance details in response

- [ ] **T2.2:** Member accessing another member's instance in same tenant returns 404
  - Create instance as member M1, attempt access as member M2 (same tenant)
  - Expected: 404 Not Found (member scope, not just tenant scope)
  - Verify: No cross-member access

- [ ] **T2.3:** Member attempting operator-only transition returns 403
  - Create instance as member, attempt "approve" action
  - Expected: 403 Forbidden (transition exists, member not authorized)
  - Verify: State unchanged, attempt logged

- [ ] **T2.4:** Operator accessing member-only transition returns 403
  - Create instance, operator attempts "submit-personal-details" (member-only)
  - Expected: 403 Forbidden
  - Verify: Operators cannot act as members without ownership

#### Emulator Security Tests

- [ ] **T3.1:** Emulator endpoints return 404 in non-Development environment
  - Deploy to Staging/Production config, call `/api/backoffice/workflows/*`
  - Expected: 404 Not Found
  - Verify: `IWebHostEnvironment.IsDevelopment()` check working

- [ ] **T3.2:** Emulator cannot create instances in non-demo tenant
  - Attempt emulator instance creation with production tenant ID
  - Expected: 400 Bad Request (demo tenant check)
  - Verify: Production data untouched

- [ ] **T3.3:** Emulator decisions flow through authorization service
  - Emulator simulates operator approval, verify auth service was called
  - Expected: Authorization service invoked, logged
  - Verify: No bypass path

#### Concurrency Tests

- [ ] **T4.1:** Concurrent transitions on same stateVersion return 409 for second request
  - Submit two transition requests simultaneously with version=5
  - Expected: First succeeds (200), second gets 409 Conflict
  - Verify: Only one transition executed, version now 6

- [ ] **T4.2:** Stale version submission returns 409 with expected vs actual
  - Submit field group with stateVersion=3 when current is 5
  - Expected: 409 Conflict, response includes expected=5, provided=3
  - Verify: State unchanged

#### Audit Integrity Tests

- [ ] **T5.1:** WorkflowEvent records are append-only
  - Attempt to DELETE or UPDATE WorkflowEvent via any API
  - Expected: No endpoint exists, DB constraint prevents modification
  - Verify: Audit log immutable

- [ ] **T5.2:** Timeline endpoint does not expose raw field values
  - Submit PII in field group, query timeline
  - Expected: Timeline shows metadata only (field group key, timestamp)
  - Verify: No PII in response JSON

#### Information Leakage Tests

- [ ] **T6.1:** Error responses do not leak internal details
  - Trigger various errors (DB failure, null ref, unhandled exception)
  - Expected: Generic error message, correlation ID only
  - Verify: Stack traces, DB queries, file paths NOT in response

- [ ] **T6.2:** Existence concealment: 404 response identical for "wrong tenant" vs "not found"
  - Compare response body/headers for non-existent ID vs wrong-tenant ID
  - Expected: Identical response (timing-safe comparison)
  - Verify: No timing side-channel leakage

#### Definition Integrity Tests

- [ ] **T7.1:** WorkflowDefinition CRUD not accessible to members
  - Authenticated member attempts GET/POST to `/workflows/definitions`
  - Expected: 403 Forbidden or 404 Not Found
  - Verify: Only operators/admin can access

- [ ] **T7.2:** Published workflow definitions are immutable
  - Attempt to modify published WorkflowDefinition version
  - Expected: 400 Bad Request (immutability constraint)
  - Verify: Only draft versions modifiable

### 7.2 Test Implementation Strategy

**Unit tests:** Service layer authorization and tenant scoping
```csharp
[Fact]
public async Task GetInstanceForCurrentTenantAsync_WrongTenant_ReturnsNull()
{
    // Arrange: instance in tenant A, context set to tenant B
    // Act: call GetInstanceForCurrentTenantAsync
    // Assert: returns null, no exception, logs security event
}
```

**Integration tests:** Controller endpoints with tenant/actor scenarios
```csharp
[Fact]
public async Task SubmitFieldGroup_CrossTenantAttempt_Returns404()
{
    // Arrange: create instance in tenant A
    // Act: switch tenant to B, attempt submit
    // Assert: 404 response, state unchanged
}
```

**E2E tests (Playwright):** Full workflow with multi-tenant UI
```typescript
test('cross-tenant access blocked', async ({ page, context }) => {
  // Login as tenant A member, create instance
  const instanceId = await createWorkflowInstance(page);
  
  // Switch to tenant B context (new browser context)
  const tenantBPage = await context.newPage();
  await loginAsTenantB(tenantBPage);
  
  // Attempt to access tenant A's instance
  const response = await tenantBPage.goto(`/workflows/${instanceId}`);
  expect(response?.status()).toBe(404);
});
```

### 7.3 Continuous Security Monitoring

**Recommended telemetry:**

```csharp
// Log all authorization failures for monitoring
_logger.LogWarning(
    "Workflow authorization failure: User {UserId} in tenant {TenantId} " +
    "attempted {Action} on instance {InstanceId} (result: {Result})",
    userId, tenantId, action, instanceId, result);

// Metrics for security dashboard
_metrics.RecordAuthFailure("workflow", action, result);
```

**Alerting thresholds:**
- More than 10 cross-tenant access attempts per hour from same user → alert
- Emulator endpoint access in production → critical alert
- Multiple stateVersion conflicts for same instance → potential race or attack

---

## 8. Risk Mitigation Summary

### 8.1 Threat T1: Cross-Tenant Instance Access (IDOR)

**Mitigations:**
1. `IWorkflowTenantGuard` service enforces tenant scope on ALL lookups
2. Database queries ALWAYS include `AND TenantId = @tenantId` clause
3. Return 404 (not 403) for existence concealment
4. Security test T1.1, T1.2, T1.3 validate isolation
5. Log cross-tenant attempts as security events

**Residual Risk:** Low (defense in depth, mandatory guard, tested)

---

### 8.2 Threat T2: Unauthorized Field Group Submission

**Mitigations:**
1. `IWorkflowActorAuthorizationService` checks instance ownership for Member role
2. Field group submission requires both tenant match AND owner match
3. State version check prevents submission on stale reads
4. Authorization test T2.2 validates cross-member blocking
5. Audit log records all submission attempts

**Residual Risk:** Low (authorization service, ownership check, tested)

---

### 8.3 Threat T3: Invalid Transition for Actor Role

**Mitigations:**
1. `WorkflowTransition.AllowedActors` declaratively defines role eligibility
2. `IsAuthorizedForTransitionAsync` enforces role check before execution
3. Member role requires instance ownership (not just tenant membership)
4. Operator role requires `role=prism-operator` claim in JWT
5. Authorization test T2.3, T2.4 validate role enforcement
6. Failed attempts logged with actor/role/transition details

**Residual Risk:** Low (declarative model, centralized check, tested)

---

### 8.4 Threat T4: Emulator Endpoints in Production

**Mitigations:**
1. `[EmulatorOnly]` attribute filter returns 404 in non-Development environments
2. `IWebHostEnvironment.IsDevelopment()` check at action start
3. `[ApiExplorerSettings(IgnoreApi = true)]` hides from OpenAPI/Swagger
4. Demo tenant ID required for all emulator actions (config-driven)
5. Security test T3.1 validates production blocking
6. Emulator logging clearly marks simulated actions

**Residual Risk:** Very Low (multiple defense layers, environment-gated, tested)

**Recommendation:** Add CI/CD pipeline check that fails build if emulator endpoints are discoverable in production swagger.json

---

### 8.5 Threat T5: Concurrency Race Conditions

**Mitigations:**
1. `stateVersion` / ETag enforcement on ALL mutating operations
2. Atomic database UPDATE with `WHERE stateVersion = @expected` clause
3. Return 409 Conflict with expected vs actual version on mismatch
4. Client-side retry logic with fresh state fetch
5. Security test T4.1, T4.2 validate version enforcement
6. Version conflicts logged for monitoring

**Residual Risk:** Very Low (database-level atomicity, tested)

**Note:** This is both a UX and security control — protects integrity as well as user experience.

---

### 8.6 Threat T6: Audit Trail Tampering

**Mitigations:**
1. `WorkflowEvent` table is append-only (no UPDATE/DELETE endpoints)
2. Database constraints prevent modification (no ON UPDATE CASCADE)
3. Application services only expose `AppendEventAsync`, no delete methods
4. Security test T5.1 validates immutability
5. Audit events include hash of previous event for chain integrity (optional Phase 2)

**Residual Risk:** Very Low (design-level immutability, no delete path)

**Recommendation:** Consider adding event chain hash in Phase 2 for tamper-evidence at DB level (detect direct DB modifications).

---

### 8.7 Threat T7: Definition Tampering via Emulator

**Mitigations:**
1. Emulator-modified definitions scoped to demo tenant only
2. Core runtime always uses published, immutable definition versions
3. Running instances pin to definition version at creation (not latest)
4. Security test T7.1, T7.2 validate definition access control
5. Emulator actions logged with "EMULATOR" prefix for audit clarity

**Residual Risk:** Low (demo tenant isolation, version pinning, immutability)

**Note:** If emulator and Core share definition storage, ensure demo tenant prefix or separate schema to prevent accidental production impact.

---

### 8.8 Threat T8: Information Leakage in Errors

**Mitigations:**
1. Existence concealment: 404 for wrong-tenant instances (not 403)
2. Generic error messages in API responses (no stack traces, SQL, paths)
3. Detailed errors in structured logs with correlation ID
4. `WorkflowProblemFactory` centralizes error response format
5. Security test T6.1 validates error sanitization
6. Timeline endpoint returns metadata only, no PII

**Residual Risk:** Low (consistent response format, tested)

**Recommendation:** Review all error paths for potential leakage during security review milestone.

---

## 9. Recommended Implementation Order

### Phase 1: Tenant Isolation Foundation

1. Implement `IWorkflowTenantGuard` service
2. Add tenant scoping to all DB queries
3. Write unit tests for tenant isolation (T1.1, T1.2, T1.3)
4. Establish 404 response pattern for wrong-tenant access

**Deliverables:** Tenant isolation tests passing, no cross-tenant leakage possible

---

### Phase 2: Authorization Model

1. Define `WorkflowActor` enum and `WorkflowTransition.AllowedActors`
2. Implement `IWorkflowActorAuthorizationService`
3. Add authorization checks to all transition endpoints
4. Write unit tests for role enforcement (T2.1-T2.4)

**Deliverables:** Authorization tests passing, role-based access working

---

### Phase 3: Emulator Security Boundary

1. Create `[EmulatorOnly]` attribute filter
2. Add `[ApiExplorerSettings(IgnoreApi = true)]` to emulator controllers
3. Implement demo tenant check in emulator actions
4. Write integration tests for production blocking (T3.1, T3.2, T3.3)

**Deliverables:** Emulator cannot leak into production, tests passing

---

### Phase 4: Concurrency Controls

1. Add `stateVersion` column to WorkflowInstance schema
2. Implement atomic version check in transition logic
3. Return 409 Conflict on version mismatch
4. Write concurrency tests (T4.1, T4.2)

**Deliverables:** Concurrency tests passing, race conditions prevented

---

### Phase 5: PII Protection

1. Implement `IFieldGroupEncryptionService` (AES-256-GCM pattern)
2. Encrypt field values on submission, decrypt on retrieval
3. Ensure timeline endpoint returns metadata only (no raw values)
4. Write PII exposure tests (T5.2, T6.2)

**Deliverables:** PII encrypted at rest, timeline safe

---

### Phase 6: Security Test Suite

1. Implement all security tests from checklist (Section 7)
2. Add tests to CI/CD pipeline as mandatory gate
3. Document test failure triage process
4. Set up security monitoring and alerting

**Deliverables:** Full security test suite passing, monitoring in place

---

## 10. Open Questions for Design Review

### Q1: Encryption Key Rotation

**Question:** How should encryption key rotation be handled for field values?

**Options:**
- A) Manual: Operator triggers rotation, decrypt-reencrypt all field submissions
- B) Multi-key: Store key version with each submission, support multiple keys
- C) External KMS: Use Azure Key Vault / AWS KMS for key management

**Recommendation:** Option B for Phase 2 (multi-key support), defer KMS to production readiness.

---

### Q2: Audit Event Chain Integrity

**Question:** Should WorkflowEvent include hash chain for tamper-evidence?

**Options:**
- A) No hash chain: Trust DB constraints and access controls
- B) Simple hash: Each event includes SHA-256 of previous event ID + timestamp
- C) Merkle tree: Build merkle tree over event batches for efficient verification

**Recommendation:** Option A for v1 demo, Option B for Phase 2 if compliance requires.

---

### Q3: Rate Limiting per Tenant

**Question:** Should workflow API endpoints have rate limiting per tenant/user?

**Options:**
- A) No rate limiting: Trust ASP.NET Core built-in middleware
- B) Per-tenant limits: 100 workflow actions/minute per tenant
- C) Per-user limits: 20 workflow actions/minute per user

**Recommendation:** Option B for Phase 2 (prevent tenant-level DoS), use ASP.NET Core RateLimiter middleware.

---

### Q4: WorkflowDefinition Signing

**Question:** Should published WorkflowDefinitions be cryptographically signed to prevent tampering?

**Options:**
- A) No signing: DB immutability constraints sufficient
- B) HMAC signature: Sign definition JSON with shared secret on publish
- C) Digital signature: Sign with asymmetric key pair, verify on load

**Recommendation:** Option A for v1 demo (DB constraints sufficient), Option B for Phase 2 if definition export/import adds tamper risk.

---

## 11. Security Review Checklist

**Before Phase 1 implementation begins:**

- [ ] Security design document reviewed by Copper (this document)
- [ ] Threat model validated with Lead (Tom Nook)
- [ ] Authorization model approved (member vs operator roles)
- [ ] Emulator boundary design signed off
- [ ] PII encryption approach decided (Option A recommended)

**Before v1 demo deployment:**

- [ ] All security tests passing (Section 7 checklist)
- [ ] Emulator endpoints blocked in non-Development environment
- [ ] Tenant isolation validated with cross-tenant test suite
- [ ] Authorization enforcement tested for all transitions
- [ ] Concurrency controls tested with simulated race conditions
- [ ] PII exposure tests passing (timeline, errors)
- [ ] Security logging and monitoring in place
- [ ] Incident response plan defined (what to do if vulnerability found)

**Before production readiness:**

- [ ] Encryption key rotation strategy implemented
- [ ] Rate limiting per tenant enabled
- [ ] Security penetration testing completed
- [ ] GDPR/CCPA compliance review (if handling real PII)
- [ ] SOC 2 audit trail requirements validated
- [ ] Disaster recovery plan includes workflow data
- [ ] Security documentation published for operators/admins

---

## 12. References

### Internal References

- [Workflow Forms Engine Demo Proposal](./workflow-forms-engine-demo.md) — Section 7 risks
- `/src/UmbracoPrism.Core/Auth/PrismTenantHandler.cs` — Tenant isolation pattern
- `/src/UmbracoPrism.Core/Services/RefreshTokenEncryptionService.cs` — Encryption pattern
- `/src/UmbracoPrism.Core/Controllers/DeviceAdminController.cs` — Tenant-scoped controller example

### External References

- [OWASP ASVS 4.0](https://owasp.org/www-project-application-security-verification-standard/) — Security requirements standard
- [OWASP API Security Top 10](https://owasp.org/API-Security/) — API threat landscape
- [NIST SP 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html) — Digital identity guidelines
- [CWE-639: Insecure Direct Object Reference](https://cwe.mitre.org/data/definitions/639.html) — IDOR vulnerability
- [CWE-362: Concurrent Execution using Shared Resource](https://cwe.mitre.org/data/definitions/362.html) — TOCTOU races

---

## Document Version

**Version:** 1.0  
**Author:** Copper (Security Engineer)  
**Review Status:** Pending Lead approval  
**Next Review:** After Phase 1 implementation (tenant isolation milestone)
