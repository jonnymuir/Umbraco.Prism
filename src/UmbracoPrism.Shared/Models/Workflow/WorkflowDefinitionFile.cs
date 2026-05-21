using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow.Components;

namespace UmbracoPrism.Shared.Models.Workflow;

/// <summary>
/// JSON-deserialized shape of a workflow definition seed file.
/// Uses polymorphic component hierarchy with type discriminator for all components.
/// </summary>
public record WorkflowDefinitionFile
{
    /// <summary>The unique identifier for this workflow definition (e.g. "retirement-quote").</summary>
    public string DefinitionKey { get; init; } = "";

    /// <summary>User-facing display name for the workflow.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Version number of the definition (for tracking schema evolution).</summary>
    public int Version { get; init; }

    /// <summary>The state key that instances start in when first created.</summary>
    public string InitialState { get; init; } = "";

    /// <summary>Instance creation policy: "single" (reuse existing), "multiple" (always create new), "prompt" (ask user).</summary>
    public string InstancePolicy { get; init; } = "single";

    /// <summary>All states defined in this workflow.</summary>
    public IReadOnlyList<StepDefinition> States { get; init; } = Array.Empty<StepDefinition>();

    /// <summary>All state transitions (edges) defined in this workflow.</summary>
    public IReadOnlyList<WorkflowTransitionFile> Transitions { get; init; } = Array.Empty<WorkflowTransitionFile>();

    /// <summary>
    /// Optional authored-workflow metadata preserved during publish so runtime hosts can inspect
    /// the original authoring intent without changing the core Prism execution contract.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowDefinitionMetadata? Metadata { get; init; }
}

/// <summary>
/// JSON-deserialized shape of a workflow state within a definition.
/// Describes what to collect/display when the instance reaches this state using polymorphic components.
/// </summary>
public record StepDefinition
{
    /// <summary>The unique identifier for this state within the workflow (e.g. "collect-details").</summary>
    public string StateKey { get; init; } = "";

    /// <summary>User-facing display name for this state.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Polymorphic components to render within this step.</summary>
    public IReadOnlyList<PrismComponent> Components { get; init; } = Array.Empty<PrismComponent>();

    /// <summary>
    /// Optional authored-stage metadata preserved during publish for action execution and
    /// compatibility diagnostics.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowStateMetadata? Metadata { get; init; }
}

/// <summary>
/// JSON-deserialized shape of a workflow transition.
/// Defines a valid state change and the action that triggers it.
/// </summary>
public record WorkflowTransitionFile
{
    /// <summary>The state this transition originates from.</summary>
    public string FromState { get; init; } = "";

    /// <summary>The state this transition goes to.</summary>
    public string ToState { get; init; } = "";

    /// <summary>The action name that triggers this transition (e.g. "submit", "approve").</summary>
    public string Action { get; init; } = "";

    /// <summary>Optional role restriction: null for any user, "reviewer" for reviewer-only actions.</summary>
    public string? RequiresRole { get; init; }

    /// <summary>
    /// Optional authored-transition metadata preserved during publish for conditions and runtime
    /// transition handlers.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowTransitionMetadata? Metadata { get; init; }
}

/// <summary>
/// Optional metadata carried alongside the published workflow definition without affecting the
/// existing Prism runtime contract.
/// </summary>
public record WorkflowDefinitionMetadata
{
    public Guid AuthoredWorkflowId { get; init; }

    public string? Description { get; init; }

    public string? SchemaVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowHandoffDefinition>? Handoffs { get; init; }
}

/// <summary>Preserved authored handoff metadata.</summary>
public record WorkflowHandoffDefinition
{
    public string Id { get; init; } = "";

    public string FromState { get; init; } = "";

    public string ToState { get; init; } = "";

    public string Label { get; init; } = "";

    public string? ActorChange { get; init; }
}

/// <summary>Preserved authored-state metadata.</summary>
public record WorkflowStateMetadata
{
    public string? Description { get; init; }

    public string? StageType { get; init; }

    public string? Actor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? RoleGates { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }
}

/// <summary>Preserved authored-transition metadata.</summary>
public record WorkflowTransitionMetadata
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowConditionDefinition>? Conditions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkflowActionDefinition>? Actions { get; init; }
}

/// <summary>Portable action metadata preserved in the published runtime definition.</summary>
public record WorkflowActionDefinition
{
    public string Type { get; init; } = "";

    public string Timing { get; init; } = "";

    public JsonObject Parameters { get; init; } = [];

    public string? ParameterSchemaKey { get; init; }

    public string? Summary { get; init; }
}

/// <summary>Portable transition-condition metadata preserved in the published runtime definition.</summary>
public record WorkflowConditionDefinition
{
    public string Kind { get; init; } = "expression";

    public string Expression { get; init; } = "";

    public string? Description { get; init; }
}
