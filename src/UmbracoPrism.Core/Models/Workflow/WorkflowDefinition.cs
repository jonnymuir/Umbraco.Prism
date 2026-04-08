namespace UmbracoPrism.Core.Models.Workflow;

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
    /// Gets or sets the tenant identifier for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow key (stable identifier across versions).
    /// </summary>
    public string DefinitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow title for display purposes.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic version (e.g., "1.0.0", "2.1.0").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of states in this workflow.
    /// </summary>
    public List<WorkflowState> States { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of transitions between states.
    /// </summary>
    public List<WorkflowTransition> Transitions { get; set; } = new();

    /// <summary>
    /// Gets or sets the initial state key where new instances begin.
    /// </summary>
    public string InitialState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field group keys referenced by this workflow.
    /// </summary>
    public List<string> FieldGroupKeys { get; set; } = new();

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this definition was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

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
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the archetype for UI rendering.
    /// Valid values: Collect, Review, TaskQueue, Decision, RequestChanges, StatusTimeline, Completion.
    /// </summary>
    public string Archetype { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actions allowed in this state.
    /// </summary>
    public List<string> AllowedActions { get; set; } = new();
}

/// <summary>
/// Represents a transition between two states in a workflow.
/// </summary>
public class WorkflowTransition
{
    /// <summary>
    /// Gets or sets the source state key.
    /// </summary>
    public string FromState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the destination state key.
    /// </summary>
    public string ToState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action key that triggers this transition (e.g., "submit", "approve", "reject").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role required to perform this transition. Null if no role requirement.
    /// </summary>
    public string? RequiresRole { get; set; }
}
