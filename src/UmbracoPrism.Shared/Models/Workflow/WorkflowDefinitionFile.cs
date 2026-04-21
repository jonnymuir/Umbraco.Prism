namespace UmbracoPrism.Shared.Models.Workflow;

/// <summary>
/// JSON-deserialized shape of a workflow definition seed file.
/// Contains the states and transitions that define a workflow's structure.
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
}

/// <summary>
/// JSON-deserialized shape of a workflow state within a definition.
/// Describes what to collect/display when the instance reaches this state.
/// </summary>
public record StepDefinition
{
    /// <summary>The unique identifier for this state within the workflow (e.g. "collect-details").</summary>
    public string StateKey { get; init; } = "";
    /// <summary>User-facing display name for this state.</summary>
    public string DisplayName { get; init; } = "";
    /// <summary>
    /// The step type for this state: "question" (render fields), "check-answers" (review), 
    /// "confirmation" (final state), "status-timeline" (read-only status), or "task-list" (task list pattern).
    /// </summary>
    public string StepType { get; init; } = "question";
    /// <summary>Allowed actions from this state (legacy field, not currently used).</summary>
    public IReadOnlyList<string> AllowedActions { get; init; } = Array.Empty<string>();
    /// <summary>Keys of field groups to render when in this state.</summary>
    public IReadOnlyList<string> FieldGroupKeys { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Configuration for "waiting" step types. Only present when <see cref="StepType"/> is <c>"waiting"</c>.
    /// </summary>
    public WaitingConfig? WaitingConfig { get; init; }
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
}

/// <summary>
/// Configuration for a "waiting" step type. Defines the message, expected wait time,
/// polling behaviour, and optional defer option shown when the workflow is paused
/// pending external processing (e.g., payment, review queue, background job).
/// Only present when <see cref="StepDefinition.StepType"/> is <c>"waiting"</c>.
/// </summary>
public record WaitingConfig
{
    /// <summary>
    /// The main message displayed to the user while waiting
    /// (e.g., "We're processing your payment. This usually takes 30 seconds.").
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// Expected wait time in seconds, used for user expectation management
    /// (e.g., 30 → "This usually takes about 30 seconds.").
    /// </summary>
    public int ExpectedWaitSeconds { get; init; }

    /// <summary>
    /// How often the client should poll for a state change, in milliseconds (default: 3000).
    /// </summary>
    public int PollIntervalMs { get; init; } = 3000;

    /// <summary>
    /// Whether to show the "leave and return later" defer option (default: true).
    /// When true, users are shown a link to the workflow hub so they can return later.
    /// </summary>
    public bool AllowDefer { get; init; } = true;

    /// <summary>
    /// Optional custom message for the defer option. If null or empty, a default message is shown.
    /// </summary>
    public string? DeferMessage { get; init; }
}

/// <summary>
/// JSON-deserialized shape of a field group seed file.
/// A reusable collection of fields that can be rendered in one or more workflow states.
/// </summary>
public record FormSectionDefinition
{
    /// <summary>The unique identifier for this field group (e.g. "personal-info").</summary>
    public string GroupKey { get; init; } = "";
    /// <summary>User-facing display name for this field group.</summary>
    public string DisplayName { get; init; } = "";
    /// <summary>Version number of the field group (for tracking schema evolution).</summary>
    public int Version { get; init; }
    /// <summary>The fields contained in this group.</summary>
    public IReadOnlyList<FieldFile> Fields { get; init; } = Array.Empty<FieldFile>();
}

/// <summary>
/// JSON-deserialized shape of a field within a field group.
/// Describes a single form field to collect from the user.
/// </summary>
public record FieldFile
{
    /// <summary>The unique identifier for this field (e.g. "retirement-age").</summary>
    public string FieldKey { get; init; } = "";
    /// <summary>User-facing label displayed next to the field.</summary>
    public string Label { get; init; } = "";
    /// <summary>Optional hint or helper text displayed below the label.</summary>
    public string? Hint { get; init; }
    /// <summary>The input type (e.g. "text", "number", "select", "checkbox").</summary>
    public string FieldType { get; init; } = "text";
    /// <summary>Whether this field must be completed before submission.</summary>
    public bool Required { get; init; }
    /// <summary>For select/checkbox fields, the list of available options.</summary>
    public IReadOnlyList<string>? Options { get; init; }
    /// <summary>Minimum character length for text/textarea fields.</summary>
    public int? MinLength { get; init; }
    /// <summary>Maximum character length for text/textarea fields.</summary>
    public int? MaxLength { get; init; }
    /// <summary>HTML5 pattern (regex) attribute value for text/email fields.</summary>
    public string? Pattern { get; init; }
    /// <summary>Minimum value for number fields.</summary>
    public decimal? Min { get; init; }
    /// <summary>Maximum value for number fields.</summary>
    public decimal? Max { get; init; }
    /// <summary>The field key this field depends on for visibility.</summary>
    public string? ConditionalOn { get; init; }
    /// <summary>The value that makes this field visible when ConditionalOn is set.</summary>
    public string? VisibleWhen { get; init; }
    /// <summary>Currency/unit prefix displayed before the input (e.g., "£").</summary>
    public string? Prefix { get; init; }
    /// <summary>
    /// For radios/checkboxes: sub-fields revealed when the parent option is selected.
    /// Key is the option value; value is the list of fields shown when that option is active.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FieldFile>>? ConditionalFields { get; init; }
    /// <summary>Content to render for non-input content field types (inset-text, warning-text, details, notification-banner).</summary>
    public string? Content { get; init; }
}
