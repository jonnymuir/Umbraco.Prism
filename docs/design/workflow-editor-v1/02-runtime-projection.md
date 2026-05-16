# 02 — Runtime Model & Projection

**Date:** 2026-05-16  
**Author:** Blathers (Backend Dev)  
**Status:** Proposed  
**Relates to:** `docs/design/workflow-editor-v1/README.md` (Tom Nook — three-plane architecture)

---

## 1. Purpose & Scope

This document defines the editor's internal **Authored Model** and the deterministic **Projection Pipeline** that compiles it into `WorkflowDefinitionFile` instances the existing Prism runtime consumes without modification. The overall three-plane architecture (authoring plane → projection plane → agent plane) is described in the section README; this document owns the authoring and projection planes only — specifically the typed domain model, compilation stages, validation layering, diff/patch contract, storage layout, and the server-side API surface that the editor UI and agent tooling call. It does not redesign the Prism runtime, does not alter `WorkflowDefinitionFile`, and does not replace the existing `/admin/workflow` inspector.

---

## 2. Authored Model

The Authored Model is the editor's primary internal representation. It is **not** `WorkflowDefinitionFile`. It operates at a higher level of abstraction — stages, roles, views, handoffs, and deadlines — and is compiled down to the runtime target contract by the Projection Pipeline (§3).

### 2.1 Top-level Workflow

```csharp
/// <summary>
/// Editor-native representation of a workflow. This is the source of truth
/// for human and agent authoring. It is never loaded directly by the Prism runtime.
/// </summary>
public record AuthoredWorkflow
{
    /// <summary>
    /// Stable slug that maps to WorkflowDefinitionFile.DefinitionKey on projection.
    /// Once set, must not be changed without a migration step.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>Human-readable name for editor UI and documentation.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Monotonically increasing integer. Incremented on every committed projection.
    /// Maps to WorkflowDefinitionFile.Version.
    /// </summary>
    public int Version { get; init; }

    /// <summary>Authored schema version (separate from workflow business version).</summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>Instance creation policy forwarded verbatim to the runtime.</summary>
    public InstancePolicy InstancePolicy { get; init; } = InstancePolicy.Single;

    /// <summary>
    /// The StateKey of the stage that becomes WorkflowDefinitionFile.InitialState.
    /// Must reference a stage in Stages[].
    /// </summary>
    public required string InitialStageKey { get; init; }

    /// <summary>Ordered list of authored stages. Order is informational; graph edges define execution order.</summary>
    public IReadOnlyList<AuthoredStage> Stages { get; init; } = [];

    /// <summary>Authored transitions (edges). Projected 1:1 to WorkflowTransitionFile unless a stage expands.</summary>
    public IReadOnlyList<AuthoredTransition> Transitions { get; init; } = [];

    /// <summary>Named roles that appear in role gates on stages and transitions.</summary>
    public IReadOnlyList<AuthoredRole> Roles { get; init; } = [];

    /// <summary>Reusable field definitions referenced by stages.</summary>
    public IReadOnlyList<AuthoredField> Fields { get; init; } = [];

    // --- Authored-only concerns (stripped during projection) ---

    /// <summary>Editor comment or rationale for the current revision.</summary>
    public string? AuthorNote { get; init; }

    /// <summary>Graph layout hints for the visual canvas (x/y positions, zoom level).</summary>
    public GraphLayout? Layout { get; init; }

    /// <summary>Provenance tags: which agent or human proposed which stage, when.</summary>
    public IReadOnlyList<ProvenanceTag> ProvenanceTags { get; init; } = [];
}

public enum InstancePolicy { Single, Multiple, Prompt }
```

### 2.2 Stage

```csharp
/// <summary>
/// A stage in the authored workflow. Roughly maps to one runtime state, though some
/// stage kinds (e.g. task-list with sub-tasks, or multi-view capture) may expand to
/// multiple runtime states at projection time.
/// </summary>
public record AuthoredStage
{
    /// <summary>
    /// Stable unique key within this workflow.
    /// Maps to StepDefinition.StateKey on projection (or becomes a prefix for expanded states).
    /// </summary>
    public required string StageKey { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Intent hint used by the projector to select the component shell.
    /// The projector uses this as a tie-breaker; the actual shell is always inferred
    /// from the emitted components (preserving existing Prism inference rules).
    /// </summary>
    public StageKind Kind { get; init; } = StageKind.Capture;

    /// <summary>Audience-specific views. Each view may project to a separate runtime state.</summary>
    public IReadOnlyList<AuthoredView> Views { get; init; } = [];

    /// <summary>
    /// Which roles may enter this stage. Empty = any authenticated principal.
    /// Enforced at the business-app transition layer; Prism does not gate entry natively.
    /// </summary>
    public IReadOnlyList<string> RoleGates { get; init; } = [];

    /// <summary>Exits from this stage. Each exit becomes an AuthoredTransition on projection.</summary>
    public IReadOnlyList<AuthoredExit> Exits { get; init; } = [];

    /// <summary>Only present for StageKind.Waiting. Forwarded to WaitingComponent on projection.</summary>
    public WaitingMetadata? Waiting { get; init; }

    /// <summary>Authored-only: comments about this stage visible only in the editor.</summary>
    public string? EditorComment { get; init; }

    /// <summary>Authored-only: canvas position for graph layout.</summary>
    public CanvasPosition? Position { get; init; }
}

public enum StageKind
{
    Capture,      // question shell — has interactive fields
    Review,       // check-answers shell — shows a SummaryList of collected answers
    Decision,     // question shell with role gate; reviewer approves/rejects
    TaskList,     // task-list shell — expands to sub-task states
    Waiting,      // status-timeline shell — polls until condition is met
    Confirmation, // confirmation shell — panel with no inputs
    Backstage,    // operator/caseworker view; no public browser-facing fields
    Complete      // terminal state
}

public record AuthoredView
{
    public required string ViewKey { get; init; }
    public required ViewAudience Audience { get; init; }

    /// <summary>Ordered list of field keys (or inline field definitions) for this view.</summary>
    public IReadOnlyList<AuthoredFieldRef> Fields { get; init; } = [];

    /// <summary>Content components: inset-text, details, warning-text, body, heading.</summary>
    public IReadOnlyList<AuthoredContentBlock> ContentBlocks { get; init; } = [];
}

public enum ViewAudience { Public, Member, BusinessApp, Operator }

public record AuthoredExit
{
    public required string Action { get; init; }       // e.g. "submit", "approve", "reject"
    public required string ToStageKey { get; init; }
    public string? Condition { get; init; }            // optional CEL/simple expression
    public string? RequiresRole { get; init; }         // role key reference
}

public record WaitingMetadata
{
    public string? Content { get; init; }
    public int? ExpectedWaitSeconds { get; init; }
    public int? PollIntervalMs { get; init; }
    public bool AllowDefer { get; init; }
    public string? DeferMessage { get; init; }
}
```

### 2.3 Transition

```csharp
/// <summary>
/// An explicit authored transition. The projector also derives transitions from
/// AuthoredExit entries on stages; explicit AuthoredTransition entries override.
/// Maps 1:1 to WorkflowTransitionFile on projection.
/// </summary>
public record AuthoredTransition
{
    public required string FromStageKey { get; init; }
    public required string ToStageKey { get; init; }
    public required string Action { get; init; }
    public string? RequiresRole { get; init; }
    public string? Condition { get; init; }
    public string? EditorComment { get; init; }
}
```

### 2.4 Field & Validation

```csharp
/// <summary>
/// A reusable field definition. Stored in AuthoredWorkflow.Fields[].
/// Stages reference fields by key via AuthoredFieldRef.
/// </summary>
public record AuthoredField
{
    public required string FieldKey { get; init; }
    public required string Label { get; init; }
    public required FieldKind Kind { get; init; }
    public bool Required { get; init; }
    public string? HintText { get; init; }
    public IReadOnlyList<AuthoredValidation> Validations { get; init; } = [];
    public IReadOnlyList<string> Options { get; init; } = [];  // for Radios, Checkboxes, Select
    public string? EditorComment { get; init; }
}

public enum FieldKind
{
    TextInput, Textarea, Radios, Checkboxes, Select,
    DateInput, FileUpload, Hidden
}

public record AuthoredValidation
{
    public required ValidationType Type { get; init; }
    public string? Pattern { get; init; }       // for Regex
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum ValidationType { Required, MinLength, MaxLength, Regex, Custom }

/// <summary>A reference from a stage view to a shared field definition.</summary>
public record AuthoredFieldRef
{
    public required string FieldKey { get; init; }

    /// <summary>Inline override for label/hint. If null, inherits from AuthoredField.</summary>
    public string? LabelOverride { get; init; }
    public bool? RequiredOverride { get; init; }
}
```

### 2.5 Role & Action

```csharp
public record AuthoredRole
{
    public required string RoleKey { get; init; }     // e.g. "reviewer", "caseworker"
    public required string DisplayName { get; init; }

    /// <summary>How Prism claims map to this role. Informational for V1; not emitted to runtime.</summary>
    public string? ClaimMapping { get; init; }
}
```

### 2.6 Authored-only Concerns

```csharp
/// <summary>Captures which agent or human proposed a stage or field, and when.</summary>
public record ProvenanceTag
{
    public required string TargetId { get; init; }    // stage key, field key, or transition key
    public required string Author { get; init; }      // "human", "copilot", or agent id
    public required DateTimeOffset ProposedAt { get; init; }
    public string? Rationale { get; init; }
}

public record GraphLayout
{
    public double Zoom { get; init; } = 1.0;
    public double PanX { get; init; }
    public double PanY { get; init; }
}

public record CanvasPosition
{
    public double X { get; init; }
    public double Y { get; init; }
}

public record AuthoredContentBlock
{
    public required ContentBlockKind Kind { get; init; }
    public required string Content { get; init; }
    public string? Summary { get; init; }  // for Details component
}

public enum ContentBlockKind { InsetText, WarningText, Details, Body, Heading, NotificationBanner }
```

---

## 3. Projection Pipeline

The Projection Pipeline is a **deterministic, pure function** from `AuthoredWorkflow` to `WorkflowDefinitionFile + ProjectionManifest`. Given identical input, it must produce byte-identical output every time. This is the central guarantee that enables:

- **Diff/replay**: patch application can be verified by re-projecting and comparing checksums.
- **Test determinism**: seed file round-trip tests (`SeedFileRoundtripTests`) remain stable.
- **Agent trust**: an agent proposing a patch can project locally and assert the result before proposing.

### 3.1 Pipeline Stages

```
AuthoredWorkflow
    │
    ▼
[1. Validate]          — structural + domain rules (see §4.2)
    │  ProjectionValidationResult (errors block; warnings pass)
    ▼
[2. Normalise]         — deterministic ordering + field deduplication
    │  NormalisedWorkflow (internal)
    ▼
[3. Infer Shells]      — component shape → Prism shell type (preserves existing inference)
    │  StageEmissionPlan[]
    ▼
[4. Emit]              — build WorkflowDefinitionFile + sidecar ProjectionManifest
    │
    ▼
[5. Checksum]          — SHA-256 of deterministic JSON serialisation
    │
    ▼
WorkflowDefinitionFile + ProjectionManifest
```

### 3.2 Function Signatures

```csharp
namespace UmbracoPrism.Core.Services.WorkflowEditor;

/// <summary>
/// Entry point for the Projection Pipeline. All methods are pure and stateless;
/// callers supply the full AuthoredWorkflow and receive the result.
/// </summary>
public interface IWorkflowProjector
{
    /// <summary>
    /// Projects the authored model to a WorkflowDefinitionFile and a sidecar manifest.
    /// Throws ProjectionException if validation fails (use Validate() first to surface errors gracefully).
    /// </summary>
    ProjectionResult Project(AuthoredWorkflow authored);

    /// <summary>Validates the authored model without projecting. Fast path for editor feedback.</summary>
    AuthoringValidationResult ValidateAuthored(AuthoredWorkflow authored);

    /// <summary>Validates the projected result against the runtime contract.</summary>
    ProjectionValidationResult ValidateProjection(WorkflowDefinitionFile projected);
}

public record ProjectionResult
{
    public required WorkflowDefinitionFile Definition { get; init; }
    public required ProjectionManifest Manifest { get; init; }

    /// <summary>
    /// SHA-256 of the deterministic JSON serialisation of Definition.
    /// Identical inputs produce identical checksums.
    /// </summary>
    public required string Checksum { get; init; }
}

public record ProjectionManifest
{
    /// <summary>Maps each projected StateKey back to its source AuthoredStage.StageKey.</summary>
    public required IReadOnlyDictionary<string, string> StateToStageMap { get; init; }

    /// <summary>
    /// Inferred shell for each projected state. Preserved for editor preview and diagnostics.
    /// </summary>
    public required IReadOnlyDictionary<string, string> InferredShells { get; init; }

    public required DateTimeOffset ProjectedAt { get; init; }
    public required string AuthoredSchemaVersion { get; init; }
    public required int ProjectedVersion { get; init; }
}
```

### 3.3 Stage 1: Validate (Projection-time)

See §4.2 for the full rule set. Any error halts projection.

### 3.4 Stage 2: Normalise

Deterministic ordering is required for byte-identical output. The normaliser:

1. Sorts `Stages` by `StageKey` (ordinal). Original order is captured in `Layout` hints but not emitted.
2. Sorts `Transitions` by `(FromStageKey, ToStageKey, Action)` (ordinal).
3. Sorts `Fields` by `FieldKey` (ordinal).
4. Deduplicates `Fields`: if a `FieldRef` overrides only label/required, it inherits the base field definition for all other properties.
5. Sorts `Roles` by `RoleKey` (ordinal).
6. Merges explicit `AuthoredTransition` entries with implied exits from `AuthoredExit` on each stage. Explicit entries win on collision (same `from+to+action`).

### 3.5 Stage 3: Infer Shells

The shell inferencer maps each `AuthoredStage` to one or more `StageEmissionPlan` entries. Each plan describes the `PrismComponent[]` array and the expected inferred shell.

**Shell inference rules** — these are identical to the rules in `PrismComponentExtensions.InferStepType()` and `WorkflowRenderShellResolver.ResolveShell()`, which lock the contract. The projector must not diverge from these rules. Contracts are guarded by:

- `WorkflowDefinitionInferenceTests.MissingStepType_IsInferredFromComponentShape`
- `WorkflowDefinitionInferenceTests.WaitingComponentWithoutAuthoredStepType_InfersWaitingMetadata`

| `StageKind`    | Emitted Components                                | Inferred Shell   |
|----------------|---------------------------------------------------|------------------|
| `Capture`      | `FieldsetComponent` + content blocks              | `question`       |
| `Review`       | `SummaryListComponent` (one per view field group) | `check-answers`  |
| `Decision`     | `FieldsetComponent` with Radios + content blocks  | `question`       |
| `TaskList`     | `TaskListComponent`                               | `task-list`      |
| `Waiting`      | `WaitingComponent` (from `WaitingMetadata`)       | `status-timeline`|
| `Confirmation` | `PanelComponent` (no inputs)                      | `confirmation`   |
| `Backstage`    | `BodyComponent` or operator-specific content      | `status-timeline`|
| `Complete`     | `PanelComponent` (no inputs)                      | `confirmation`   |

> **Invariant:** The projector MUST NOT emit a `stepType` or `waitingConfig` property on any `StepDefinition`. These are legacy V1 properties. The inferred shell must be derivable purely from the component tree. This is locked by `WorkflowDefinitionInferenceTests.DemoWorkflowSeeds_DoNotAuthorLegacyStepMetadata`.

Content blocks are emitted before fieldsets, matching the order: `heading`, `inset-text`, `warning-text`, `details`, `notification-banner`, `body`. This ordering is deterministic and alphabetically stable within each type by `Content`.

### 3.6 Stage 4: Emit

```csharp
internal WorkflowDefinitionFile Emit(NormalisedWorkflow normalised, StageEmissionPlan[] plans)
{
    var states = plans.Select(plan => new StepDefinition
    {
        StateKey    = plan.StateKey,
        DisplayName = plan.DisplayName,
        Components  = plan.Components   // ordered per §3.5
    }).ToArray();

    var transitions = normalised.Transitions.Select(t => new WorkflowTransitionFile
    {
        FromState    = t.FromStageKey,
        ToState      = t.ToStageKey,
        Action       = t.Action,
        RequiresRole = t.RequiresRole   // null if no role gate
    }).ToArray();

    return new WorkflowDefinitionFile
    {
        DefinitionKey  = normalised.DefinitionKey,
        DisplayName    = normalised.DisplayName,
        Version        = normalised.Version,
        InitialState   = normalised.InitialStageKey,
        InstancePolicy = normalised.InstancePolicy.ToRuntimeString(),
        States         = states,
        Transitions    = transitions
    };
}
```

### 3.7 Stage 5: Checksum

```csharp
internal string ComputeChecksum(WorkflowDefinitionFile definition)
{
    // Deterministic JSON: sorted property names, no indentation, camelCase.
    var options = new JsonSerializerOptions
    {
        PropertyNamingPolicy       = JsonNamingPolicy.CamelCase,
        WriteIndented              = false,
        DefaultIgnoreCondition     = JsonIgnoreCondition.Never,
        Encoder                    = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Sort all IReadOnlyList<> before serialising. Normalise stage already did this;
    // this step is a belt-and-suspenders guard.
    var json = JsonSerializer.Serialize(definition, options);
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
}
```

### 3.8 Worked Example: single Capture stage through the pipeline

**Authored input:**

```json
{
  "stageKey": "collect-applicant-details",
  "displayName": "Applicant Details",
  "kind": "Capture",
  "views": [
    {
      "viewKey": "public",
      "audience": "Public",
      "contentBlocks": [
        { "kind": "InsetText", "content": "We will use these details to contact you." }
      ],
      "fields": [
        { "fieldKey": "full-name" },
        { "fieldKey": "email" }
      ]
    }
  ],
  "exits": [
    { "action": "submit", "toStageKey": "check-your-answers" }
  ]
}
```

**After normalise:** fields resolved from `AuthoredWorkflow.Fields[]`; content block sorted before fieldset.

**After infer-shells:** `StageEmissionPlan` → `[InsetTextComponent, FieldsetComponent { Children: [TextInputComponent{FieldKey:"full-name"}, TextInputComponent{FieldKey:"email"}] }]`; inferred shell = `"question"`.

**After emit:**

```json
{
  "stateKey": "collect-applicant-details",
  "displayName": "Applicant Details",
  "components": [
    { "type": "inset-text", "content": "We will use these details to contact you." },
    {
      "type": "fieldset",
      "children": [
        { "type": "text-input", "fieldKey": "full-name", "label": "Full name", "required": true },
        { "type": "text-input", "fieldKey": "email",     "label": "Email address", "required": true }
      ]
    }
  ]
}
```

**Transition emitted separately:**

```json
{ "fromState": "collect-applicant-details", "toState": "check-your-answers", "action": "submit" }
```

**Inferred shell at render-time (WorkflowRenderShellResolver):** `FieldsetComponent` has interactive inputs → not `check-answers`, not `confirmation`, not `status-timeline` → shell = `"question"`. ✓

---

## 4. Validation Layers

Three distinct validation contexts run at different points in the authoring lifecycle.

### 4.1 Authoring-time (fast, synchronous, per-save or per-keystroke)

Runs in-memory on every save. Must complete in < 50 ms. No IO.

| Rule | Check |
|------|-------|
| **Schema** | `DefinitionKey` non-empty, matches `^[a-z0-9-]+$` |
| **Schema** | `InitialStageKey` references a stage in `Stages[]` |
| **Schema** | Every `AuthoredFieldRef.FieldKey` resolves in `AuthoredWorkflow.Fields[]` |
| **Schema** | Every `AuthoredExit.RequiresRole` resolves in `AuthoredWorkflow.Roles[]` |
| **Graph** | No duplicate `StageKey` values |
| **Graph** | No duplicate `FieldKey` values in `Fields[]` |
| **Graph** | Every `AuthoredExit.ToStageKey` and `AuthoredTransition.ToStageKey` references a valid stage |
| **Graph** | At least one stage is reachable from `InitialStageKey` |
| **Graph** | Every non-terminal stage has at least one exit |
| **Role/Action** | `RequiresRole` values reference defined roles |
| **Role/Action** | `Action` strings are non-empty and match `^[a-z0-9-]+$` |
| **Waiting** | `StageKind.Waiting` stages have non-null `WaitingMetadata` |

```csharp
public record AuthoringValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ValidationIssue> Errors   { get; init; } = [];
    public IReadOnlyList<ValidationIssue> Warnings { get; init; } = [];
}

public record ValidationIssue
{
    public required string Code     { get; init; }
    public required string Message  { get; init; }
    public string? TargetId         { get; init; }  // stage key, field key, or transition key
    public Severity Severity        { get; init; }
}

public enum Severity { Error, Warning, Info }
```

### 4.2 Projection-time

Runs before `Emit`. Any error raises `ProjectionException`; warnings are recorded in `ProjectionManifest`.

| Rule | Check |
|------|-------|
| **Shell legality** | `StageKind.TaskList` stages must not have field refs (task-list is structural only) |
| **Shell legality** | `StageKind.Confirmation` / `StageKind.Complete` stages must not have interactive fields |
| **Shell legality** | `StageKind.Waiting` stages must have `WaitingMetadata.PollIntervalMs >= 500` |
| **Completeness** | Every stage reachable from `InitialStageKey` must produce at least one `StepDefinition` |
| **Completeness** | Every emitted `StepDefinition.Components` is non-empty |
| **Completeness** | Every emitted `WorkflowTransitionFile.Action` is non-empty |
| **Contract compat** | No `stepType` or `waitingConfig` properties emitted (legacy V1 guard) |
| **Contract compat** | `DefinitionKey` matches `^[a-z0-9-]+$` (Prism engine requirement) |
| **Contract compat** | `InstancePolicy` is `"single"`, `"multiple"`, or `"prompt"` |
| **Field keys** | All `FieldKey` values within a view path are globally unique across the projected definition |

### 4.3 Runtime-time

The existing Prism engine validates the loaded `WorkflowDefinitionFile` on startup and on first instance creation. We do not alter this validation. The projection's job is to feed the engine valid input so that runtime validation passes without error. The contract is the existing `WorkflowDefinitionFile` record shape and behaviour.

---

## 5. Diff & Patch Model

Patches operate on the **Authored Model**, not on the projected `WorkflowDefinitionFile`. This keeps diffs human-readable and agent-friendly.

### 5.1 Patch Envelope

```csharp
public record WorkflowPatchEnvelope
{
    /// <summary>Target workflow definition key.</summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Expected version of the authored model this patch applies to.
    /// Prevents lost-update conflicts.
    /// </summary>
    public required int BaseVersion { get; init; }

    /// <summary>Ordered list of JSON-Patch-like operations on the Authored Model.</summary>
    public required IReadOnlyList<PatchOperation> Ops { get; init; }

    /// <summary>Human or agent rationale for this patch set.</summary>
    public string? Rationale { get; init; }

    /// <summary>Who or what generated this patch.</summary>
    public required PatchProvenance Provenance { get; init; }

    /// <summary>Populated by the server after ValidatePatch(); not supplied by callers.</summary>
    public AuthoringValidationResult? ValidationResult { get; init; }
}

public record PatchOperation
{
    /// <summary>JSON Patch operation type: add, remove, replace, move, copy, test.</summary>
    public required string Op { get; init; }

    /// <summary>
    /// JSON Pointer path into the Authored Model.
    /// E.g. "/stages/-" (append), "/stages/2/exits/0/toStageKey", "/fields/email/label".
    /// </summary>
    public required string Path { get; init; }

    public JsonElement? Value { get; init; }
    public string? From { get; init; }

    /// <summary>Optional: the stage/field/transition key that is the semantic insertion point.</summary>
    public string? TargetId { get; init; }
}

public record PatchProvenance
{
    public required string Author { get; init; }         // "human", "copilot", or agent id
    public required DateTimeOffset GeneratedAt { get; init; }
    public string? NaturalLanguageRequest { get; init; } // original user prompt, if agent-sourced
}
```

### 5.2 Natural-language → Patch Envelope (Example)

**User request:** *"Insert an external ID&V stage before the final review."*

**Agent produces:**

```json
{
  "definitionKey": "planning-application",
  "baseVersion": 7,
  "rationale": "Add external identity verification step before reviewer sees the application.",
  "provenance": {
    "author": "copilot",
    "generatedAt": "2026-05-16T13:20:33Z",
    "naturalLanguageRequest": "Insert an external ID&V stage before the final review."
  },
  "ops": [
    {
      "op": "add",
      "path": "/stages/-",
      "targetId": "identity-verification",
      "value": {
        "stageKey": "identity-verification",
        "displayName": "Identity Verification",
        "kind": "Waiting",
        "waiting": {
          "content": "We are verifying your identity with our ID&V provider.",
          "expectedWaitSeconds": 120,
          "pollIntervalMs": 5000,
          "allowDefer": false
        },
        "exits": [
          { "action": "verified",       "toStageKey": "review-application" },
          { "action": "failed",         "toStageKey": "identity-check-failed" }
        ]
      }
    },
    {
      "op": "replace",
      "path": "/transitions/3/toStageKey",
      "targetId": "submit-application→review-application",
      "value": "identity-verification"
    },
    {
      "op": "add",
      "path": "/transitions/-",
      "value": {
        "fromStageKey": "identity-verification",
        "toStageKey":   "review-application",
        "action":       "verified"
      }
    },
    {
      "op": "add",
      "path": "/transitions/-",
      "value": {
        "fromStageKey": "identity-verification",
        "toStageKey":   "identity-check-failed",
        "action":       "failed"
      }
    }
  ]
}
```

The server runs `ValidatePatch(authored, patch)` before applying. If valid, `ApplyPatch` produces a new `AuthoredWorkflow` with `Version` incremented.

---

## 6. Storage Layout

### 6.1 On-disk Format

Authored workflows live in the MockBusinessApp alongside seed files:

```
src/UmbracoPrism.MockBusinessApp/
  workflow-authored/
    planning-application.workflow.json
    community-enquiry.workflow.json
    payment-demo.workflow.json
    information-request.workflow.json
```

Each `*.workflow.json` serialises a single `AuthoredWorkflow` using camelCase JSON, sorted property names within arrays, and UTF-8 without BOM. The schema version field (`schemaVersion`) enables future migration steps.

### 6.2 Relationship to the Projected WorkflowDefinitionFile

Generated `WorkflowDefinitionFile` JSON is **checked in** alongside the authored source in V1:

```
src/UmbracoPrism.MockBusinessApp/
  workflow-seeds/
    planning-application.json   ← generated; checked in; loaded by runtime
  workflow-authored/
    planning-application.workflow.json  ← source of truth for authors
```

**Rationale for checking in the generated file:**

- The Prism runtime loads seeds from disk at startup. It cannot project at runtime in V1.
- Checking in the projection makes CI trivial: `dotnet test` loads seeds directly, `SeedFileRoundtripTests` validates them.
- A CI step (`workflow-editor project --verify`) re-projects all authored files and fails if the checksum differs from the checked-in seed. This detects manual edits to the projected file.

### 6.3 Versioning (V1)

V1 uses git as the version store. `AuthoredWorkflow.Version` is a monotonically increasing integer written by the server on every `ApplyPatch` call. It is used for optimistic concurrency (patch envelopes carry `baseVersion`). There is no separate semver or branching model in V1.

The `schemaVersion` field (e.g. `"1.0"`) tracks the authored schema shape. Migration steps run on load if `schemaVersion` is older than the current engine version.

---

## 7. API Surface (Server-side)

All endpoints require admin authentication. Align with existing Prism admin auth (Umbraco backoffice cookie / bearer token). No new auth model is introduced.

### 7.1 Endpoints

```
GET    /umbraco/api/workflow-editor/{definitionKey}
       → AuthoredWorkflow (LoadAuthoredWorkflow)

POST   /umbraco/api/workflow-editor/{definitionKey}/patch
       Body: WorkflowPatchEnvelope
       → PatchResult { AuthoredWorkflow, AuthoringValidationResult }

POST   /umbraco/api/workflow-editor/{definitionKey}/validate
       Body: AuthoredWorkflow
       → AuthoringValidationResult

POST   /umbraco/api/workflow-editor/{definitionKey}/validate-patch
       Body: { authored: AuthoredWorkflow, patch: WorkflowPatchEnvelope }
       → AuthoringValidationResult (runs authoring-time rules + projection-time dry-run)

POST   /umbraco/api/workflow-editor/{definitionKey}/project
       Body: AuthoredWorkflow
       → ProjectionResult { WorkflowDefinitionFile, ProjectionManifest, Checksum }

GET    /umbraco/api/workflow-editor/{definitionKey}/preview?persona={public|member|operator}&route={stageKey}
       → PreviewShell (see below)
```

### 7.2 Service Interfaces

```csharp
namespace UmbracoPrism.Core.Services.WorkflowEditor;

public interface IAuthoredWorkflowStore
{
    Task<AuthoredWorkflow?> LoadAsync(string definitionKey, CancellationToken ct = default);
    Task SaveAsync(AuthoredWorkflow workflow, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListDefinitionKeysAsync(CancellationToken ct = default);
}

public interface IWorkflowPatchService
{
    /// <summary>Applies the patch envelope to the authored workflow and persists the result.</summary>
    Task<PatchResult> ApplyPatchAsync(
        string definitionKey,
        WorkflowPatchEnvelope patch,
        CancellationToken ct = default);

    AuthoringValidationResult ValidatePatch(
        AuthoredWorkflow authored,
        WorkflowPatchEnvelope patch);
}

public record PatchResult
{
    public required AuthoredWorkflow Updated       { get; init; }
    public required AuthoringValidationResult Validation { get; init; }
    public bool Applied => Validation.IsValid;
}

public interface IWorkflowPreviewService
{
    /// <summary>
    /// Returns a renderable shell for a single stage, scoped to the given persona.
    /// Used by Isabelle's editor preview pane.
    /// Does not require a projected WorkflowDefinitionFile; operates on the Authored Model.
    /// </summary>
    PreviewShell Preview(
        AuthoredWorkflow authored,
        string stageKey,
        ViewAudience persona);
}

public record PreviewShell
{
    public required string StageKey      { get; init; }
    public required string InferredShell { get; init; }
    public required IReadOnlyList<PrismComponentRenderPayload> Components { get; init; }
    public IReadOnlyList<string> AvailableActions { get; init; } = [];
}
```

### 7.3 Auth Model

All workflow editor API routes are protected by the Umbraco backoffice authentication policy (`UmbracoApiController` or equivalent Minimal API policy). No new auth middleware is needed. Role-based access within the editor (e.g. read-only reviewer vs full author) is out of scope for V1; all authenticated backoffice users have full editor access.

---

## 8. Migration / Coexistence

The existing `/admin/workflow` JSON inspector loads `WorkflowDefinitionFile` seeds directly from `workflow-seeds/*.json`. It continues to work unchanged — it never sees the Authored Model.

The Prism runtime loads `workflow-seeds/*.json` at startup via `BusinessAppWorkflowEngine`. This also continues unchanged. The projection pipeline's output (the seed file) is the integration point.

**Additive, not replacing:** the Authored Model is an additional authoring artefact. Existing seed files that were hand-authored before the editor existed continue to be valid. The CI projection-verify step only runs on seed files that have a corresponding `*.workflow.json` source. Seed files without a `.workflow.json` counterpart are left untouched and are loaded by the runtime as-is.

**Upgrade path:** to bring an existing hand-authored seed file under editor management, an author runs a one-off import command (`workflow-editor import {definitionKey}`) which reverse-engineers an `AuthoredWorkflow` from the existing seed. This is a V2 concern; V1 starts with new workflows only.

---

## 9. Open Questions

1. **Multi-tenant authored stores.** In V1, authored files live under `workflow-authored/` in the MockBusinessApp. In a multi-tenant deployment, each tenant may have their own workflow variant. The store interface (`IAuthoredWorkflowStore`) is designed to be swapped, but the V1 file-backed implementation is single-tenant.

2. **Branching and rollback.** V1 relies entirely on git for rollback. A future iteration may want a named-branch concept within the editor (draft vs published) without requiring a git branch per authored change.

3. **Backstage / operator views.** `StageKind.Backstage` stages project to `status-timeline` shells in V1. Whether operator-facing states should live inside the Prism payload or be served from a separate operator API contract (outside `WorkflowDefinitionFile`) is an open decision from the prior Blathers runtime design (§Open Decisions, item 1).

4. **Condition expressions.** `AuthoredExit.Condition` and `AuthoredTransition.Condition` are modelled as strings in V1 but not evaluated. A future iteration will need a safe expression language and a corresponding validation layer.

5. **Authored schema migration pipeline.** `schemaVersion` is tracked, but V1 ships no migration steps. The first breaking authored schema change will need a concrete migration runner.

---

## 10. Tests Owed

Coordinate with Tangy for test orchestration and CI hooks. The following C# unit/integration tests **must ship with V1**.

| Test Class | What it covers |
|---|---|
| `WorkflowProjectorDeterminismTests` | Same `AuthoredWorkflow` input → identical `Checksum` across 10 runs; different input → different checksum |
| `WorkflowProjectorShellInferenceTests` | Each `StageKind` produces the expected inferred shell; `WaitingComponent` → `status-timeline`; `PanelComponent` → `confirmation`; `SummaryListComponent` → `check-answers`; `TaskListComponent` → `task-list`; default → `question` |
| `WorkflowProjectorLegacyGuardTests` | Projection never emits `stepType` or `waitingConfig` on any `StepDefinition` (aligns with `WorkflowDefinitionInferenceTests.DemoWorkflowSeeds_DoNotAuthorLegacyStepMetadata`) |
| `WorkflowProjectorRoundtripTests` | Project an `AuthoredWorkflow`; deserialise the result as `WorkflowDefinitionFile`; re-project a second time from the re-loaded authored source; checksums match. Parameterised over all `workflow-authored/*.workflow.json` files |
| `WorkflowPatchServiceApplyTests` | Apply a single `add` op, a `replace` op, a `remove` op; assert the resulting `AuthoredWorkflow` fields are correct; assert `Version` is incremented |
| `WorkflowPatchServiceConflictTests` | `ApplyPatch` with `BaseVersion` != current version returns a conflict result without mutating the stored model |
| `AuthoringValidationTests` | Each authoring-time rule (§4.1) has at least one passing and one failing case |
| `ProjectionValidationTests` | Each projection-time rule (§4.2) has at least one failing authored model that produces the expected `ProjectionValidationResult` error |
| `WorkflowPreviewServiceTests` | `Preview` for a `Capture` stage + `Public` persona returns the correct component tree; `Preview` for a `Waiting` stage returns a `WaitingComponent`; inferred shell matches expected |
| `AuthoredWorkflowStoreRoundtripTests` | Load a `*.workflow.json`; serialise back to JSON; re-load; all fields match (no loss through serialisation round-trip) |

> **Regression anchor:** `SeedFileRoundtripTests` and `WorkflowDefinitionInferenceTests` (already present) must continue to pass without modification. These lock the shell inference contract. The new projection tests must not alter the inference behaviour those tests assert.

---

*End of 02-runtime-projection.md*
