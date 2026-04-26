# Prism Workflow Forms Engine — Backend Contracts & Schema Design

> **⚠️ v2.0 Schema Update:** This document predates v2.0 implementation. References to `fieldType` and `fields[]` are legacy. See [walkthroughs/](../../walkthroughs/) for v2.0 polymorphic component examples.

**Author:** Blathers (Backend Dev)  
**Date:** 2026-04-08  
**Status:** Design specification  
**References:** [Workflow Forms Engine Demo Proposal](workflow-forms-engine-demo.md)

---

## 1. C# Data Models

### 1.1 WorkflowDefinition

Versioned workflow schema with states, transitions, guards, and field-group bindings.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a versioned workflow definition with states, transitions, and field-group bindings.
/// Definitions are immutable once published.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Gets or sets the database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the workflow key (stable identifier across versions).
    /// </summary>
    public string WorkflowKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic version (e.g., "1.0.0", "2.1.0").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow title for display purposes.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the workflow status: Draft, Published, Retired.
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Gets or sets the states collection as JSON.
    /// Stored as JSON array to avoid complex normalized graph schema for demo.
    /// </summary>
    public string StatesJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the transitions collection as JSON.
    /// </summary>
    public string TransitionsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was published. Null if never published.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID who created this definition.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    // Deserialized runtime properties (not stored directly)
    /// <summary>
    /// Gets the deserialized states collection from StatesJson.
    /// </summary>
    [NPoco.Ignore]
    public List<WorkflowState> States { get; set; } = new();

    /// <summary>
    /// Gets the deserialized transitions collection from TransitionsJson.
    /// </summary>
    [NPoco.Ignore]
    public List<WorkflowTransition> Transitions { get; set; } = new();
}
```

### 1.2 WorkflowState

Individual state node with archetype mapping.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a single state node in a workflow definition.
/// </summary>
public class WorkflowState
{
    /// <summary>
    /// Gets or sets the unique state key within the workflow.
    /// </summary>
    public string StateKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the state.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the archetype for UI rendering.
    /// Valid values: Collect, Review, TaskQueue, Decision, RequestChanges, StatusTimeline, Completion.
    /// </summary>
    public string Archetype { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this state is a terminal state.
    /// </summary>
    public bool IsTerminal { get; set; }

    /// <summary>
    /// Gets or sets the field group keys visible in this state.
    /// </summary>
    public List<string> FieldGroupKeys { get; set; } = new();

    /// <summary>
    /// Gets or sets additional metadata for the state as JSON (UI hints, help text, etc.).
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### 1.3 WorkflowTransition

From/to states, guard references, action key.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a transition between two states in a workflow.
/// </summary>
public class WorkflowTransition
{
    /// <summary>
    /// Gets or sets the source state key.
    /// </summary>
    public string FromStateKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the destination state key.
    /// </summary>
    public string ToStateKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action key that triggers this transition (e.g., "submit", "approve", "reject").
    /// </summary>
    public string ActionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label for the action.
    /// </summary>
    public string ActionLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of guard keys that must pass for this transition to be available.
    /// Guards are evaluated server-side (role checks, validation rules, etc.).
    /// </summary>
    public List<string> Guards { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this transition requires confirmation from the user.
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the transition as JSON.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### 1.4 FieldGroupDefinition

Versioned schema block with field list.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a versioned field group definition with schema and validation rules.
/// Field groups are reusable across workflows.
/// </summary>
public class FieldGroupDefinition
{
    /// <summary>
    /// Gets or sets the database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the field group key (stable identifier across versions).
    /// </summary>
    public string FieldGroupKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic version (e.g., "1.0.0", "2.1.0").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display title for the field group.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the status: Draft, Published, Retired.
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Gets or sets the fields collection as JSON.
    /// </summary>
    public string FieldsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was published. Null if never published.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID who created this definition.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    // Deserialized runtime properties
    /// <summary>
    /// Gets the deserialized fields collection from FieldsJson.
    /// </summary>
    [NPoco.Ignore]
    public List<FieldGroupField> Fields { get; set; } = new();
}
```

### 1.5 FieldGroupField

Individual field with key, type, validation rules, conditional visibility.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a single field within a field group definition.
/// </summary>
public class FieldGroupField
{
    /// <summary>
    /// Gets or sets the unique field key within the field group.
    /// </summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label for the field.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field type.
    /// Valid values: text, email, number, date, select, multiselect, textarea, checkbox, radio, file.
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// Gets or sets whether the field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the validation rules as JSON (e.g., min/max length, pattern, custom validators).
    /// </summary>
    public Dictionary<string, object> ValidationRules { get; set; } = new();

    /// <summary>
    /// Gets or sets the conditional visibility expression (JSON logic or simple key-value).
    /// If null, field is always visible.
    /// </summary>
    public Dictionary<string, object>? ConditionalVisibility { get; set; }

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets options for select/radio fields.
    /// </summary>
    public List<FieldOption>? Options { get; set; }

    /// <summary>
    /// Gets or sets the help text for the field.
    /// </summary>
    public string? HelpText { get; set; }

    /// <summary>
    /// Gets or sets the placeholder text.
    /// </summary>
    public string? Placeholder { get; set; }
}

/// <summary>
/// Represents an option for select/radio fields.
/// </summary>
public class FieldOption
{
    /// <summary>
    /// Gets or sets the option value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the option label.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
```

### 1.6 WorkflowInstance

Runtime instance with instanceId, workflowKey, workflowVersion, currentState, TenantId, UserId, timestamps.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a runtime workflow instance tracking current state and execution metadata.
/// </summary>
public class WorkflowInstance
{
    /// <summary>
    /// Gets or sets the unique instance identifier (GUID).
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow key this instance is running.
    /// </summary>
    public string WorkflowKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pinned workflow version.
    /// </summary>
    public string WorkflowVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who initiated the workflow instance.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state key.
    /// </summary>
    public string CurrentStateKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state version counter for optimistic concurrency control.
    /// Incremented on every state transition.
    /// </summary>
    public int StateVersion { get; set; }

    /// <summary>
    /// Gets or sets the instance status: Active, Waiting, Completed, Cancelled, Faulted.
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the instance reached a terminal state. Null if not complete.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the final outcome key for completed instances (e.g., "approved", "rejected").
    /// </summary>
    public string? OutcomeKey { get; set; }

    /// <summary>
    /// Gets or sets additional instance metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }
}
```

### 1.7 WorkflowTask

Operator task with taskId, instanceId, assigneeRole, dueAt, status.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a queueable work item for reviewer/approver/ops roles.
/// </summary>
public class WorkflowTask
{
    /// <summary>
    /// Gets or sets the unique task identifier (GUID).
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this task belongs to.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task type/key (e.g., "review", "approve", "assign").
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role or user ID assigned to this task.
    /// Role-based: "Approver", "Reviewer"; User-based: specific user GUID.
    /// </summary>
    public string AssignedTo { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the assignment is role-based (true) or user-based (false).
    /// </summary>
    public bool IsRoleAssignment { get; set; } = true;

    /// <summary>
    /// Gets or sets the task status: Pending, InProgress, Completed, Cancelled.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task is due. Null if no deadline.
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was started. Null if not started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was completed. Null if not complete.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user ID who completed the task.
    /// </summary>
    public string? CompletedBy { get; set; }

    /// <summary>
    /// Gets or sets the task outcome (e.g., "approved", "rejected", "changes-requested").
    /// </summary>
    public string? OutcomeKey { get; set; }

    /// <summary>
    /// Gets or sets additional task metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }
}
```

### 1.8 WorkflowEvent

Append-only audit event with eventId, instanceId, eventType, actorId, stateFrom, stateTo, payload, timestampUtc.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents an append-only audit event in the workflow timeline.
/// Events are never updated or deleted.
/// </summary>
public class WorkflowEvent
{
    /// <summary>
    /// Gets or sets the unique event identifier (GUID).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this event belongs to.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event type.
    /// Valid values: InstanceCreated, StateTransition, FieldGroupSubmitted, TaskCreated, TaskCompleted, ActionTriggered, Error.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who triggered the event.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source state key (for transitions). Null if not applicable.
    /// </summary>
    public string? StateFrom { get; set; }

    /// <summary>
    /// Gets or sets the destination state key (for transitions). Null if not applicable.
    /// </summary>
    public string? StateTo { get; set; }

    /// <summary>
    /// Gets or sets the event payload as JSON (submitted values, action metadata, error details).
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the UTC timestamp when the event occurred.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}
```

### 1.9 FieldGroupSubmission

Instance-level submitted values with submissionId, instanceId, fieldGroupKey, fieldGroupVersion, values as JSON, submittedBy, submittedAt.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a submitted field group for a workflow instance.
/// Stores validated user input with version pinning.
/// </summary>
public class FieldGroupSubmission
{
    /// <summary>
    /// Gets or sets the unique submission identifier (GUID).
    /// </summary>
    public string SubmissionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance this submission belongs to.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field group key.
    /// </summary>
    public string FieldGroupKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pinned field group version used for validation.
    /// </summary>
    public string FieldGroupVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the submitted values as JSON (key-value pairs matching field keys).
    /// </summary>
    public string ValuesJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the user ID who submitted the field group.
    /// </summary>
    public string SubmittedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the submission was created.
    /// </summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this submission is the current version or superseded.
    /// </summary>
    public bool IsCurrent { get; set; } = true;
}
```

---

## 2. NPoco Database Schema

### 2.1 Schema Classes

Following the Prism pattern, each table requires an NPoco schema class with `[TableName]`, `[PrimaryKey]`, and `[ExplicitColumns]` attributes.

#### 2.1.1 WorkflowDefinitionSchema

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowDefinitions table.
/// </summary>
[TableName("prismWorkflowDefinitions")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class WorkflowDefinitionSchema
{
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("WorkflowKey")]
    [Length(255)]
    public string WorkflowKey { get; set; } = string.Empty;

    [Column("Version")]
    [Length(50)]
    public string Version { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("Title")]
    [Length(500)]
    public string Title { get; set; } = string.Empty;

    [Column("Description")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string? Description { get; set; }

    [Column("Status")]
    [Length(50)]
    [Constraint(Default = "'Draft'")]
    public string Status { get; set; } = "Draft";

    [Column("StatesJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string StatesJson { get; set; } = "[]";

    [Column("TransitionsJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string TransitionsJson { get; set; } = "[]";

    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    [Column("PublishedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? PublishedAt { get; set; }

    [Column("CreatedBy")]
    [Length(450)]
    public string CreatedBy { get; set; } = string.Empty;
}
```

#### 2.1.2 FieldGroupDefinitionSchema

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismFieldGroupDefinitions table.
/// </summary>
[TableName("prismFieldGroupDefinitions")]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
public class FieldGroupDefinitionSchema
{
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("FieldGroupKey")]
    [Length(255)]
    public string FieldGroupKey { get; set; } = string.Empty;

    [Column("Version")]
    [Length(50)]
    public string Version { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("Title")]
    [Length(500)]
    public string Title { get; set; } = string.Empty;

    [Column("Description")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string? Description { get; set; }

    [Column("Status")]
    [Length(50)]
    [Constraint(Default = "'Draft'")]
    public string Status { get; set; } = "Draft";

    [Column("FieldsJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string FieldsJson { get; set; } = "[]";

    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    [Column("PublishedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? PublishedAt { get; set; }

    [Column("CreatedBy")]
    [Length(450)]
    public string CreatedBy { get; set; } = string.Empty;
}
```

#### 2.1.3 WorkflowInstanceSchema

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowInstances table.
/// </summary>
[TableName("prismWorkflowInstances")]
[PrimaryKey("InstanceId", AutoIncrement = false)]
[ExplicitColumns]
public class WorkflowInstanceSchema
{
    [Column("InstanceId")]
    [Length(64)]
    public string InstanceId { get; set; } = string.Empty;

    [Column("WorkflowKey")]
    [Length(255)]
    public string WorkflowKey { get; set; } = string.Empty;

    [Column("WorkflowVersion")]
    [Length(50)]
    public string WorkflowVersion { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("UserId")]
    [Length(450)]
    public string UserId { get; set; } = string.Empty;

    [Column("CurrentStateKey")]
    [Length(255)]
    public string CurrentStateKey { get; set; } = string.Empty;

    [Column("StateVersion")]
    [Constraint(Default = "0")]
    public int StateVersion { get; set; }

    [Column("Status")]
    [Length(50)]
    [Constraint(Default = "'Active'")]
    public string Status { get; set; } = "Active";

    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime UpdatedAt { get; set; }

    [Column("CompletedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? CompletedAt { get; set; }

    [Column("OutcomeKey")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OutcomeKey { get; set; }

    [Column("MetadataJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? MetadataJson { get; set; }
}
```

#### 2.1.4 WorkflowTaskSchema

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowTasks table.
/// </summary>
[TableName("prismWorkflowTasks")]
[PrimaryKey("TaskId", AutoIncrement = false)]
[ExplicitColumns]
public class WorkflowTaskSchema
{
    [Column("TaskId")]
    [Length(64)]
    public string TaskId { get; set; } = string.Empty;

    [Column("InstanceId")]
    [Length(64)]
    public string InstanceId { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("TaskType")]
    [Length(255)]
    public string TaskType { get; set; } = string.Empty;

    [Column("AssignedTo")]
    [Length(450)]
    public string AssignedTo { get; set; } = string.Empty;

    [Column("IsRoleAssignment")]
    [Constraint(Default = "1")]
    public bool IsRoleAssignment { get; set; } = true;

    [Column("Status")]
    [Length(50)]
    [Constraint(Default = "'Pending'")]
    public string Status { get; set; } = "Pending";

    [Column("CreatedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime CreatedAt { get; set; }

    [Column("DueAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? DueAt { get; set; }

    [Column("StartedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? StartedAt { get; set; }

    [Column("CompletedAt")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? CompletedAt { get; set; }

    [Column("CompletedBy")]
    [Length(450)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? CompletedBy { get; set; }

    [Column("OutcomeKey")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? OutcomeKey { get; set; }

    [Column("MetadataJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? MetadataJson { get; set; }
}
```

#### 2.1.5 WorkflowEventSchema

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismWorkflowEvents table.
/// Append-only audit log; no updates allowed.
/// </summary>
[TableName("prismWorkflowEvents")]
[PrimaryKey("EventId", AutoIncrement = false)]
[ExplicitColumns]
public class WorkflowEventSchema
{
    [Column("EventId")]
    [Length(64)]
    public string EventId { get; set; } = string.Empty;

    [Column("InstanceId")]
    [Length(64)]
    public string InstanceId { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("EventType")]
    [Length(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("ActorId")]
    [Length(450)]
    public string ActorId { get; set; } = string.Empty;

    [Column("StateFrom")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? StateFrom { get; set; }

    [Column("StateTo")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? StateTo { get; set; }

    [Column("PayloadJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string PayloadJson { get; set; } = "{}";

    [Column("TimestampUtc")]
    [Constraint(Default = "getutcdate()")]
    public DateTime TimestampUtc { get; set; }

    [Column("CorrelationId")]
    [Length(64)]
    public string CorrelationId { get; set; } = string.Empty;
}
```

#### 2.1.6 FieldGroupSubmissionSchema

```csharp
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Database schema for the prismFieldGroupSubmissions table.
/// </summary>
[TableName("prismFieldGroupSubmissions")]
[PrimaryKey("SubmissionId", AutoIncrement = false)]
[ExplicitColumns]
public class FieldGroupSubmissionSchema
{
    [Column("SubmissionId")]
    [Length(64)]
    public string SubmissionId { get; set; } = string.Empty;

    [Column("InstanceId")]
    [Length(64)]
    public string InstanceId { get; set; } = string.Empty;

    [Column("TenantId")]
    [Length(450)]
    public string TenantId { get; set; } = string.Empty;

    [Column("FieldGroupKey")]
    [Length(255)]
    public string FieldGroupKey { get; set; } = string.Empty;

    [Column("FieldGroupVersion")]
    [Length(50)]
    public string FieldGroupVersion { get; set; } = string.Empty;

    [Column("ValuesJson")]
    [SpecialDbType(SpecialDbTypes.NTEXT)]
    public string ValuesJson { get; set; } = "{}";

    [Column("SubmittedBy")]
    [Length(450)]
    public string SubmittedBy { get; set; } = string.Empty;

    [Column("SubmittedAt")]
    [Constraint(Default = "getutcdate()")]
    public DateTime SubmittedAt { get; set; }

    [Column("IsCurrent")]
    [Constraint(Default = "1")]
    public bool IsCurrent { get; set; } = true;
}
```

### 2.2 Migration Classes

Following the existing pattern from `CreatePrismDeviceCredentialsTable`, each table creation requires an `AsyncMigrationBase` migration class.

#### 2.2.1 CreatePrismWorkflowTables

```csharp
using Umbraco.Cms.Infrastructure.Migrations;

namespace UmbracoPrism.Core.Persistence;

/// <summary>
/// Migration that creates all workflow engine tables.
/// </summary>
public class CreatePrismWorkflowTables(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        // Create workflow definitions table
        if (!TableExists("prismWorkflowDefinitions"))
        {
            Create.Table<WorkflowDefinitionSchema>().Do();

            // Unique index: one definition per (TenantId, WorkflowKey, Version)
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismWorkflowDefinitions_TenantId_WorkflowKey_Version
                ON prismWorkflowDefinitions (TenantId, WorkflowKey, Version);");

            // Published workflow lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowDefinitions_TenantId_Status
                ON prismWorkflowDefinitions (TenantId, Status);");
        }

        // Create field group definitions table
        if (!TableExists("prismFieldGroupDefinitions"))
        {
            Create.Table<FieldGroupDefinitionSchema>().Do();

            // Unique index: one definition per (TenantId, FieldGroupKey, Version)
            Database.Execute(@"
                CREATE UNIQUE INDEX IX_prismFieldGroupDefinitions_TenantId_FieldGroupKey_Version
                ON prismFieldGroupDefinitions (TenantId, FieldGroupKey, Version);");

            // Published field group lookup
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupDefinitions_TenantId_Status
                ON prismFieldGroupDefinitions (TenantId, Status);");
        }

        // Create workflow instances table
        if (!TableExists("prismWorkflowInstances"))
        {
            Create.Table<WorkflowInstanceSchema>().Do();

            // Tenant isolation and user instances lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowInstances_TenantId_UserId
                ON prismWorkflowInstances (TenantId, UserId);");

            // Active instances lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowInstances_TenantId_Status
                ON prismWorkflowInstances (TenantId, Status);");

            // State version concurrency control
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowInstances_InstanceId_StateVersion
                ON prismWorkflowInstances (InstanceId, StateVersion);");
        }

        // Create workflow tasks table
        if (!TableExists("prismWorkflowTasks"))
        {
            Create.Table<WorkflowTaskSchema>().Do();

            // Instance tasks lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowTasks_InstanceId
                ON prismWorkflowTasks (InstanceId);");

            // Role/user assignment queue lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowTasks_TenantId_AssignedTo_Status
                ON prismWorkflowTasks (TenantId, AssignedTo, Status);");

            // Due date sorting for task queues
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowTasks_DueAt
                ON prismWorkflowTasks (DueAt);");
        }

        // Create workflow events table
        if (!TableExists("prismWorkflowEvents"))
        {
            Create.Table<WorkflowEventSchema>().Do();

            // Instance timeline lookup
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowEvents_InstanceId_TimestampUtc
                ON prismWorkflowEvents (InstanceId, TimestampUtc);");

            // Correlation ID tracing
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowEvents_CorrelationId
                ON prismWorkflowEvents (CorrelationId);");

            // Tenant event audit
            Database.Execute(@"
                CREATE INDEX IX_prismWorkflowEvents_TenantId_EventType
                ON prismWorkflowEvents (TenantId, EventType);");
        }

        // Create field group submissions table
        if (!TableExists("prismFieldGroupSubmissions"))
        {
            Create.Table<FieldGroupSubmissionSchema>().Do();

            // Instance submissions lookup
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupSubmissions_InstanceId
                ON prismFieldGroupSubmissions (InstanceId);");

            // Current submission by field group
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupSubmissions_InstanceId_FieldGroupKey_IsCurrent
                ON prismFieldGroupSubmissions (InstanceId, FieldGroupKey, IsCurrent);");

            // Tenant data isolation
            Database.Execute(@"
                CREATE INDEX IX_prismFieldGroupSubmissions_TenantId
                ON prismFieldGroupSubmissions (TenantId);");
        }

        return Task.CompletedTask;
    }
}
```

#### 2.2.2 Update PrismMigrationPlan

```csharp
protected override void DefinePlan()
{
    To<CreatePrismTables>("initial-state")
    .To<AddIdentityColumns>("add-identity-cols")
    .To<AddBrandingOverridesColumn>("add-branding-overrides")
    .To<AddMobileBrandingOverridesColumn>("add-mobile-branding-overrides")
    .To<AddMobileAppConfigColumn>("add-mobile-app-config")
    .To<CreatePrismDeviceCredentialsTable>("add-device-credentials")
    .To<AddRefreshTokenEncColumn>("add-refresh-token-enc")
    .To<AddAllowBiometricLoginColumn>("add-allow-biometric-login")
    .To<AddPushTokenColumn>("add-push-token")
    .To<CreatePrismNotificationSubscriptionsTable>("add-notification-subscriptions")
    .To<DropThemeColorColumn>("drop-theme-color")
    .To<CreatePrismWorkflowTables>("add-workflow-engine"); // NEW
}
```

---

## 3. Service Interfaces

### 3.1 IWorkflowDefinitionService

CRUD operations for workflow definitions.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for managing workflow definitions (create, read, update, publish, retire).
/// </summary>
public interface IWorkflowDefinitionService
{
    /// <summary>
    /// Creates a new draft workflow definition.
    /// </summary>
    Task<WorkflowDefinition> CreateDraftAsync(string tenantId, string workflowKey, string title, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow definition by key and version.
    /// </summary>
    Task<WorkflowDefinition?> GetByKeyAndVersionAsync(string tenantId, string workflowKey, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest published version of a workflow definition.
    /// </summary>
    Task<WorkflowDefinition?> GetLatestPublishedAsync(string tenantId, string workflowKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a draft workflow definition.
    /// </summary>
    Task<WorkflowDefinition> UpdateDraftAsync(int id, WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a draft workflow definition, making it immutable.
    /// </summary>
    Task<WorkflowDefinition> PublishAsync(int id, string publishedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retires a published workflow definition, preventing new instances.
    /// </summary>
    Task RetireAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all workflow definitions for a tenant.
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
```

### 3.2 IWorkflowInstanceService

Create, get, and advance workflow instances.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for managing workflow instances and state transitions.
/// </summary>
public interface IWorkflowInstanceService
{
    /// <summary>
    /// Creates a new workflow instance from a published definition.
    /// </summary>
    Task<WorkflowInstance> CreateInstanceAsync(string tenantId, string userId, string workflowKey, string? workflowVersion, Dictionary<string, object>? metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow instance by ID.
    /// </summary>
    Task<WorkflowInstance?> GetByIdAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances a workflow instance to a new state via an action.
    /// Returns updated instance with new state version.
    /// </summary>
    Task<WorkflowInstance> AdvanceAsync(string instanceId, string actionKey, string actorId, int expectedStateVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an instance as waiting (async guard pending).
    /// </summary>
    Task SetWaitingAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an instance as completed with outcome.
    /// </summary>
    Task CompleteAsync(string instanceId, string outcomeKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists instances for a user within a tenant.
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> ListByUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
}
```

### 3.3 IWorkflowRenderService

Produces render payloads from instance state.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for generating render payloads from workflow instance state.
/// </summary>
public interface IWorkflowRenderService
{
    /// <summary>
    /// Generates a render payload for the current state of a workflow instance.
    /// Includes archetype, field groups, and available actions.
    /// </summary>
    Task<WorkflowRenderPayload> GetRenderPayloadAsync(string instanceId, string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates which transitions are available from the current state for the actor.
    /// Applies guard checks (role-based, validation, etc.).
    /// </summary>
    Task<List<WorkflowAction>> GetAvailableActionsAsync(string instanceId, string actorId, CancellationToken cancellationToken = default);
}
```

### 3.4 IWorkflowSubmissionService

Validates and stores field group submissions.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for validating and storing field group submissions.
/// </summary>
public interface IWorkflowSubmissionService
{
    /// <summary>
    /// Validates and stores a field group submission for an instance.
    /// Supersedes previous submission for the same field group if IsCurrent.
    /// </summary>
    Task<FieldGroupSubmission> SubmitAsync(string instanceId, string fieldGroupKey, Dictionary<string, object> values, string submittedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current submission for a field group within an instance.
    /// </summary>
    Task<FieldGroupSubmission?> GetCurrentSubmissionAsync(string instanceId, string fieldGroupKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all submissions for an instance (for review/timeline).
    /// </summary>
    Task<IEnumerable<FieldGroupSubmission>> GetAllSubmissionsAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates field values against the pinned field group definition.
    /// Returns list of validation problems.
    /// </summary>
    Task<List<WorkflowProblem>> ValidateAsync(string tenantId, string fieldGroupKey, string fieldGroupVersion, Dictionary<string, object> values, CancellationToken cancellationToken = default);
}
```

### 3.5 IWorkflowEventService

Appends audit events and queries timeline.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for appending audit events and querying workflow timeline.
/// Events are append-only; no updates or deletes.
/// </summary>
public interface IWorkflowEventService
{
    /// <summary>
    /// Appends a new audit event to the workflow instance timeline.
    /// </summary>
    Task AppendEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full timeline of events for a workflow instance.
    /// </summary>
    Task<IEnumerable<WorkflowEvent>> GetTimelineAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events by correlation ID for distributed tracing.
    /// </summary>
    Task<IEnumerable<WorkflowEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}
```

### 3.6 IWorkflowConcurrencyGuard

Validates stateVersion / ETag for optimistic concurrency.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service for validating optimistic concurrency via state version ETags.
/// </summary>
public interface IWorkflowConcurrencyGuard
{
    /// <summary>
    /// Validates that the expected state version matches the current instance state version.
    /// Returns true if valid, false if conflict.
    /// </summary>
    Task<bool> ValidateStateVersionAsync(string instanceId, int expectedStateVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state version for an instance.
    /// </summary>
    Task<int> GetCurrentStateVersionAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the state version and updates the instance UpdatedAt timestamp.
    /// Called internally during state transitions.
    /// </summary>
    Task IncrementStateVersionAsync(string instanceId, CancellationToken cancellationToken = default);
}
```

---

## 4. API Controller Design

### 4.1 WorkflowController

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using UmbracoPrism.Core.Models;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Controllers;

/// <summary>
/// Controller for workflow engine runtime API.
/// </summary>
[Authorize]
[VersionedApiBackOfficeRoute("prism/workflows")]
[ApiExplorerSettings(GroupName = "Prism Workflow")]
[MapToApi("Prism")]
public class WorkflowController(
    IWorkflowInstanceService instanceService,
    IWorkflowRenderService renderService,
    IWorkflowSubmissionService submissionService,
    IWorkflowEventService eventService,
    IWorkflowConcurrencyGuard concurrencyGuard,
    IPrismUserContext userContext) : ManagementApiControllerBase
{
    /// <summary>
    /// Creates a new workflow instance.
    /// </summary>
    /// <returns>200 OK with ask_now response envelope.</returns>
    [HttpPost("instances")]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 200)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 422)]
    public async Task<IActionResult> CreateInstance([FromBody] CreateWorkflowInstanceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = userContext.GetTenantId();
        var userId = userContext.GetUserId();
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            var instance = await instanceService.CreateInstanceAsync(
                tenantId,
                userId,
                request.WorkflowKey,
                request.WorkflowVersion,
                request.Metadata,
                cancellationToken);

            var renderPayload = await renderService.GetRenderPayloadAsync(instance.InstanceId, userId, cancellationToken);

            await eventService.AppendEventAsync(new WorkflowEvent
            {
                EventId = Guid.NewGuid().ToString(),
                InstanceId = instance.InstanceId,
                TenantId = tenantId,
                EventType = "InstanceCreated",
                ActorId = userId,
                StateTo = instance.CurrentStateKey,
                PayloadJson = "{}",
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = correlationId
            }, cancellationToken);

            return Ok(WorkflowResponseFactory.AskNow(renderPayload, instance.InstanceId, instance.StateVersion, correlationId));
        }
        catch (Exception ex)
        {
            var problems = new List<WorkflowProblem>
            {
                new() { Category = "system", Message = ex.Message }
            };
            return UnprocessableEntity(WorkflowResponseFactory.Error(problems, null, correlationId));
        }
    }

    /// <summary>
    /// Gets the render payload for a workflow instance.
    /// </summary>
    /// <returns>200 OK with ask_now or complete response, 202 Accepted with wait response.</returns>
    [HttpGet("instances/{id}/render")]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 200)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 202)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 404)]
    public async Task<IActionResult> GetRender(string id, CancellationToken cancellationToken)
    {
        var userId = userContext.GetUserId();
        var correlationId = Guid.NewGuid().ToString();

        var instance = await instanceService.GetByIdAsync(id, cancellationToken);
        if (instance == null)
        {
            return NotFound(WorkflowResponseFactory.Error(
                new List<WorkflowProblem> { new() { Category = "not-found", Message = "Instance not found" } },
                id,
                correlationId));
        }

        if (instance.Status == "Waiting")
        {
            return StatusCode(202, WorkflowResponseFactory.Wait(3000, instance.InstanceId, instance.StateVersion, correlationId));
        }

        if (instance.Status == "Completed")
        {
            return Ok(WorkflowResponseFactory.Complete(instance.OutcomeKey ?? "unknown", instance.InstanceId, instance.StateVersion, correlationId));
        }

        var renderPayload = await renderService.GetRenderPayloadAsync(instance.InstanceId, userId, cancellationToken);
        return Ok(WorkflowResponseFactory.AskNow(renderPayload, instance.InstanceId, instance.StateVersion, correlationId));
    }

    /// <summary>
    /// Submits a field group for a workflow instance.
    /// </summary>
    /// <returns>200 OK with updated render payload, 422 for validation errors, 409 for state version conflict.</returns>
    [HttpPost("instances/{id}/submit/{fieldGroupKey}")]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 200)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 422)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 409)]
    public async Task<IActionResult> SubmitFieldGroup(string id, string fieldGroupKey, [FromBody] SubmitFieldGroupRequest request, CancellationToken cancellationToken)
    {
        var userId = userContext.GetUserId();
        var correlationId = Guid.NewGuid().ToString();

        var instance = await instanceService.GetByIdAsync(id, cancellationToken);
        if (instance == null)
        {
            return NotFound(WorkflowResponseFactory.Error(
                new List<WorkflowProblem> { new() { Category = "not-found", Message = "Instance not found" } },
                id,
                correlationId));
        }

        // Validate state version for optimistic concurrency
        if (request.StateVersion.HasValue)
        {
            var isValid = await concurrencyGuard.ValidateStateVersionAsync(id, request.StateVersion.Value, cancellationToken);
            if (!isValid)
            {
                var currentVersion = await concurrencyGuard.GetCurrentStateVersionAsync(id, cancellationToken);
                return Conflict(WorkflowResponseFactory.Error(
                    new List<WorkflowProblem>
                    {
                        new() { Category = "conflict", Message = $"State version mismatch. Expected: {request.StateVersion}, Actual: {currentVersion}" }
                    },
                    id,
                    correlationId));
            }
        }

        // Validate and store submission
        var problems = await submissionService.ValidateAsync(instance.TenantId, fieldGroupKey, instance.WorkflowVersion, request.Values, cancellationToken);
        if (problems.Any())
        {
            return UnprocessableEntity(WorkflowResponseFactory.Error(problems, id, correlationId));
        }

        await submissionService.SubmitAsync(id, fieldGroupKey, request.Values, userId, cancellationToken);

        // Append audit event
        await eventService.AppendEventAsync(new WorkflowEvent
        {
            EventId = Guid.NewGuid().ToString(),
            InstanceId = id,
            TenantId = instance.TenantId,
            EventType = "FieldGroupSubmitted",
            ActorId = userId,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { FieldGroupKey = fieldGroupKey }),
            TimestampUtc = DateTime.UtcNow,
            CorrelationId = correlationId
        }, cancellationToken);

        // Return updated render payload
        var renderPayload = await renderService.GetRenderPayloadAsync(id, userId, cancellationToken);
        return Ok(WorkflowResponseFactory.AskNow(renderPayload, instance.InstanceId, instance.StateVersion, correlationId));
    }

    /// <summary>
    /// Triggers a transition action on a workflow instance.
    /// </summary>
    /// <returns>200 OK with updated render payload or complete, 202 for wait, 409 for conflict.</returns>
    [HttpPost("instances/{id}/actions/{actionKey}")]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 200)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 202)]
    [ProducesResponseType(typeof(WorkflowResponseEnvelope), 409)]
    public async Task<IActionResult> TriggerAction(string id, string actionKey, [FromBody] TriggerActionRequest request, CancellationToken cancellationToken)
    {
        var userId = userContext.GetUserId();
        var correlationId = Guid.NewGuid().ToString();

        var instance = await instanceService.GetByIdAsync(id, cancellationToken);
        if (instance == null)
        {
            return NotFound(WorkflowResponseFactory.Error(
                new List<WorkflowProblem> { new() { Category = "not-found", Message = "Instance not found" } },
                id,
                correlationId));
        }

        // Validate state version
        if (request.StateVersion.HasValue)
        {
            var isValid = await concurrencyGuard.ValidateStateVersionAsync(id, request.StateVersion.Value, cancellationToken);
            if (!isValid)
            {
                var currentVersion = await concurrencyGuard.GetCurrentStateVersionAsync(id, cancellationToken);
                return Conflict(WorkflowResponseFactory.Error(
                    new List<WorkflowProblem>
                    {
                        new() { Category = "conflict", Message = $"State version mismatch. Expected: {request.StateVersion}, Actual: {currentVersion}" }
                    },
                    id,
                    correlationId));
            }
        }

        try
        {
            var updatedInstance = await instanceService.AdvanceAsync(id, actionKey, userId, instance.StateVersion, cancellationToken);

            if (updatedInstance.Status == "Completed")
            {
                return Ok(WorkflowResponseFactory.Complete(updatedInstance.OutcomeKey ?? "unknown", updatedInstance.InstanceId, updatedInstance.StateVersion, correlationId));
            }

            if (updatedInstance.Status == "Waiting")
            {
                return StatusCode(202, WorkflowResponseFactory.Wait(3000, updatedInstance.InstanceId, updatedInstance.StateVersion, correlationId));
            }

            var renderPayload = await renderService.GetRenderPayloadAsync(updatedInstance.InstanceId, userId, cancellationToken);
            return Ok(WorkflowResponseFactory.AskNow(renderPayload, updatedInstance.InstanceId, updatedInstance.StateVersion, correlationId));
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(WorkflowResponseFactory.Error(
                new List<WorkflowProblem> { new() { Category = "validation", Message = ex.Message } },
                id,
                correlationId));
        }
    }

    /// <summary>
    /// Gets the audit timeline for a workflow instance.
    /// </summary>
    [HttpGet("instances/{id}/timeline")]
    [ProducesResponseType(typeof(WorkflowTimelineResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTimeline(string id, CancellationToken cancellationToken)
    {
        var instance = await instanceService.GetByIdAsync(id, cancellationToken);
        if (instance == null)
        {
            return NotFound();
        }

        var events = await eventService.GetTimelineAsync(id, cancellationToken);
        return Ok(new WorkflowTimelineResponse
        {
            InstanceId = id,
            Events = events.ToList()
        });
    }
}
```

### 4.2 Request DTOs

```csharp
namespace UmbracoPrism.Core.Controllers.Models;

public class CreateWorkflowInstanceRequest
{
    public string WorkflowKey { get; set; } = string.Empty;
    public string? WorkflowVersion { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class SubmitFieldGroupRequest
{
    public Dictionary<string, object> Values { get; set; } = new();
    public int? StateVersion { get; set; }
}

public class TriggerActionRequest
{
    public int? StateVersion { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
```

---

## 5. WorkflowResponseFactory

Static factory methods for consistent response envelopes.

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Factory for creating consistent workflow response envelopes.
/// </summary>
public static class WorkflowResponseFactory
{
    /// <summary>
    /// Creates an ask_now response with render payload.
    /// </summary>
    public static WorkflowResponseEnvelope AskNow(WorkflowRenderPayload renderPayload, string instanceId, int stateVersion, string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "ask_now",
            StateVersion = stateVersion,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTime.UtcNow,
            PollAfterMs = null,
            Render = renderPayload,
            Problems = new List<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Creates a wait response with poll interval.
    /// </summary>
    public static WorkflowResponseEnvelope Wait(int pollAfterMs, string instanceId, int stateVersion, string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "wait",
            StateVersion = stateVersion,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTime.UtcNow,
            PollAfterMs = pollAfterMs,
            Render = null,
            Problems = new List<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Creates a complete response with outcome.
    /// </summary>
    public static WorkflowResponseEnvelope Complete(string outcomeKey, string instanceId, int stateVersion, string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "complete",
            StateVersion = stateVersion,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTime.UtcNow,
            PollAfterMs = null,
            Render = new WorkflowRenderPayload
            {
                Archetype = "Completion",
                OutcomeKey = outcomeKey,
                FieldGroups = new List<WorkflowFieldGroup>(),
                AvailableActions = new List<WorkflowAction>()
            },
            Problems = new List<WorkflowProblem>()
        };
    }

    /// <summary>
    /// Creates an error response with problems list.
    /// </summary>
    public static WorkflowResponseEnvelope Error(List<WorkflowProblem> problems, string? instanceId, string correlationId)
    {
        return new WorkflowResponseEnvelope
        {
            InstanceId = instanceId,
            ResponseState = "error",
            StateVersion = null,
            CorrelationId = correlationId,
            ServerTimeUtc = DateTime.UtcNow,
            PollAfterMs = null,
            Render = null,
            Problems = problems
        };
    }
}
```

---

## 6. Response Envelope DTOs

### 6.1 WorkflowResponseEnvelope

Standard response wrapper for all workflow endpoints.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Standard response envelope for all workflow dialog endpoints.
/// </summary>
public class WorkflowResponseEnvelope
{
    /// <summary>
    /// Gets or sets the workflow instance ID.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the response state: ask_now, wait, complete, error.
    /// </summary>
    public string ResponseState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state version for optimistic concurrency.
    /// </summary>
    public int? StateVersion { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the server UTC timestamp.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the poll interval in milliseconds (for wait responses).
    /// </summary>
    public int? PollAfterMs { get; set; }

    /// <summary>
    /// Gets or sets the render payload (for ask_now and complete responses).
    /// </summary>
    public WorkflowRenderPayload? Render { get; set; }

    /// <summary>
    /// Gets or sets the problems list (for error responses).
    /// </summary>
    public List<WorkflowProblem> Problems { get; set; } = new();
}
```

### 6.2 WorkflowRenderPayload

Archetype-based render instructions for UI.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Render payload for workflow UI state.
/// </summary>
public class WorkflowRenderPayload
{
    /// <summary>
    /// Gets or sets the archetype for this state.
    /// Valid values: Collect, Review, TaskQueue, Decision, RequestChanges, StatusTimeline, Completion.
    /// </summary>
    public string Archetype { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state key.
    /// </summary>
    public string? StateKey { get; set; }

    /// <summary>
    /// Gets or sets the current state display name.
    /// </summary>
    public string? StateName { get; set; }

    /// <summary>
    /// Gets or sets the field groups visible in this state.
    /// </summary>
    public List<WorkflowFieldGroup> FieldGroups { get; set; } = new();

    /// <summary>
    /// Gets or sets the available actions (transitions) from this state.
    /// </summary>
    public List<WorkflowAction> AvailableActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the outcome key (for Completion archetype).
    /// </summary>
    public string? OutcomeKey { get; set; }

    /// <summary>
    /// Gets or sets additional UI metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a field group in the render payload.
/// </summary>
public class WorkflowFieldGroup
{
    public string FieldGroupKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<FieldGroupField> Fields { get; set; } = new();
    public Dictionary<string, object>? CurrentValues { get; set; }
    public bool IsReadOnly { get; set; }
}

/// <summary>
/// Represents an available action (transition) in the render payload.
/// </summary>
public class WorkflowAction
{
    public string ActionKey { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
```

### 6.3 WorkflowProblem

Typed problem for validation/auth/conflict/system errors.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Represents a typed problem in workflow execution.
/// </summary>
public class WorkflowProblem
{
    /// <summary>
    /// Gets or sets the problem category.
    /// Valid values: validation, auth, conflict, not-found, system.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field key (for validation problems). Null for general problems.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the user-facing error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional problem metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
```

### 6.4 WorkflowTimelineResponse

Timeline endpoint response.

```csharp
namespace UmbracoPrism.Core.Models;

/// <summary>
/// Response for workflow timeline endpoint.
/// </summary>
public class WorkflowTimelineResponse
{
    public string InstanceId { get; set; } = string.Empty;
    public List<WorkflowEvent> Events { get; set; } = new();
}
```

---

## 7. Concurrency Guard Design

### 7.1 ETag Validation Flow

1. **Client sends stateVersion**: Include in request body OR as `If-Match: "{stateVersion}"` header.
2. **Service validates**: Compare expected vs. current instance state version.
3. **On conflict**: Return `409 Conflict` with error envelope containing expected vs. actual versions.
4. **On success**: Increment state version, update instance, return new version in response.

### 7.2 Implementation Pattern

```csharp
namespace UmbracoPrism.Core.Services;

/// <summary>
/// Service implementation for optimistic concurrency control.
/// </summary>
public class WorkflowConcurrencyGuard : IWorkflowConcurrencyGuard
{
    private readonly IUmbracoDatabaseFactory _databaseFactory;

    public WorkflowConcurrencyGuard(IUmbracoDatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public async Task<bool> ValidateStateVersionAsync(string instanceId, int expectedStateVersion, CancellationToken cancellationToken)
    {
        using var db = _databaseFactory.CreateDatabase();
        
        var instance = await db.SingleOrDefaultAsync<WorkflowInstanceSchema>(
            "SELECT StateVersion FROM prismWorkflowInstances WHERE InstanceId = @0",
            instanceId);

        if (instance == null)
        {
            return false;
        }

        return instance.StateVersion == expectedStateVersion;
    }

    public async Task<int> GetCurrentStateVersionAsync(string instanceId, CancellationToken cancellationToken)
    {
        using var db = _databaseFactory.CreateDatabase();
        
        var result = await db.ExecuteScalarAsync<int?>(
            "SELECT StateVersion FROM prismWorkflowInstances WHERE InstanceId = @0",
            instanceId);

        return result ?? 0;
    }

    public async Task IncrementStateVersionAsync(string instanceId, CancellationToken cancellationToken)
    {
        using var db = _databaseFactory.CreateDatabase();
        
        await db.ExecuteAsync(
            "UPDATE prismWorkflowInstances SET StateVersion = StateVersion + 1, UpdatedAt = @0 WHERE InstanceId = @1",
            DateTime.UtcNow,
            instanceId);
    }
}
```

---

## 8. Dependency Injection Registration

### 8.1 IUmbracoBuilder Extension Method

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Extension methods for registering Prism Workflow Engine services.
/// </summary>
public static class PrismWorkflowEngineExtensions
{
    /// <summary>
    /// Registers all workflow engine services with the Umbraco builder.
    /// </summary>
    public static IUmbracoBuilder AddPrismWorkflowEngine(this IUmbracoBuilder builder)
    {
        // Core services
        builder.Services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        builder.Services.AddScoped<IWorkflowInstanceService, WorkflowInstanceService>();
        builder.Services.AddScoped<IWorkflowRenderService, WorkflowRenderService>();
        builder.Services.AddScoped<IWorkflowSubmissionService, WorkflowSubmissionService>();
        builder.Services.AddScoped<IWorkflowEventService, WorkflowEventService>();
        builder.Services.AddScoped<IWorkflowConcurrencyGuard, WorkflowConcurrencyGuard>();

        // Field group service (if separate)
        builder.Services.AddScoped<IFieldGroupDefinitionService, FieldGroupDefinitionService>();

        // Task service (if separate)
        builder.Services.AddScoped<IWorkflowTaskService, WorkflowTaskService>();

        return builder;
    }
}
```

### 8.2 Usage in Composer

```csharp
using Umbraco.Cms.Core.Composing;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core;

/// <summary>
/// Composer for registering Prism Workflow Engine components.
/// </summary>
public class PrismWorkflowEngineComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddPrismWorkflowEngine();
    }
}
```

---

## 9. Implementation Notes

### 9.1 Multi-Tenant Isolation

- **ALL entities** include `TenantId` column for isolation.
- **ALL queries** filter by `TenantId` from `IPrismUserContext`.
- **Index strategy**: Include `TenantId` as first column in composite indexes for query performance.

### 9.2 JSON Storage Strategy

- **Workflow states/transitions** stored as JSON in single row to avoid complex normalized graph for demo.
- **Field group fields** stored as JSON in single row.
- **Submission values** stored as JSON key-value dictionary.
- **Metadata** stored as JSON for extensibility.

### 9.3 Version Pinning

- **Workflow instances** pin `workflowVersion` on creation (immutable).
- **Field group submissions** pin `fieldGroupVersion` (immutable).
- **Migration path**: Explicit migration scripts when breaking changes occur.

### 9.4 Append-Only Audit

- **WorkflowEvent** is append-only: no updates, ever.
- **Use case**: Complete audit trail, distributed tracing, timeline reconstruction.

### 9.5 Concurrency Control

- **StateVersion**: Integer counter incremented on every state transition.
- **Client includes**: `stateVersion` in submit/action requests.
- **Service validates**: Compare expected vs. actual before mutation.
- **On conflict**: Return `409 Conflict` with error envelope.

### 9.6 Response State Semantics

| ResponseState | HTTP Status | Meaning |
|---|---|---|
| `ask_now` | `200 OK` | UI items ready to render now |
| `wait` | `202 Accepted` | Not ready; poll after interval |
| `complete` | `200 OK` | Terminal state reached |
| `error` | `4xx/5xx` | Validation/auth/conflict/system failure |

### 9.7 Guard Evaluation

- **Server-side only**: Guards execute in Core runtime, not in renderer.
- **Role-based**: Check user roles against assignee requirements.
- **Validation**: Check required field groups submitted.
- **Custom**: Extensible guard registry for domain-specific rules.

---

## 10. Next Steps

### 10.1 Phase 1: Core Runtime Implementation

1. Create model classes in `UmbracoPrism.Core/Models/`.
2. Create schema classes in `UmbracoPrism.Core/Persistence/`.
3. Create migration class `CreatePrismWorkflowTables`.
4. Update `PrismMigrationPlan` to include new migration.
5. Implement service interfaces in `UmbracoPrism.Core/Services/`.
6. Create `WorkflowController` in `UmbracoPrism.Core/Controllers/`.
7. Register services via `AddPrismWorkflowEngine()` extension.

### 10.2 Phase 2: Testing

1. Unit tests for state transition logic.
2. Unit tests for field validation.
3. Unit tests for concurrency guard.
4. Integration tests for complete workflow flows.

### 10.3 Phase 3: MockBackOffice Integration

1. Add workflow authoring endpoints.
2. Add seeded demo workflows.
3. Add operator task queue simulation.

---

## Appendix: References

- [Workflow Forms Engine Demo Proposal](workflow-forms-engine-demo.md)
- Existing Prism patterns:
  - `CreatePrismDeviceCredentialsTable.cs` — migration pattern
  - `PrismDeviceCredential.cs` — model pattern
  - `TenantService.cs` — service pattern with caching
  - `TenantManagementController.cs` — controller pattern
