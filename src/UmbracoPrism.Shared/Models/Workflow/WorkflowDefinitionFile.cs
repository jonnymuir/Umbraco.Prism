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
    /// <summary>GDS components to render within this step. Replaces the old FieldGroupKeys approach.</summary>
    public IReadOnlyList<PrismComponentDefinition> Components { get; init; } = Array.Empty<PrismComponentDefinition>();
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
/// Defines a GDS component to render within a workflow step.
/// Components are the building blocks of step rendering — they replace the old FieldGroupKeys/FormSection approach.
/// </summary>
/// <remarks>
/// Supported types and their relevant properties:
/// <list type="table">
/// <listheader><term>type</term><description>Properties used</description></listheader>
/// <item><term>fieldset</term><description>FieldGroupKey, Legend (overrides group DisplayName), LegendSize</description></item>
/// <item><term>summary-list</term><description>FieldGroupKey, Title (overrides group DisplayName), ChangeStateKey</description></item>
/// <item><term>panel</term><description>Heading (panel title), Content (panel body)</description></item>
/// <item><term>notification-banner</term><description>BannerType ("info"|"success"|"warning"), Heading, Content</description></item>
/// <item><term>inset-text</term><description>Content</description></item>
/// <item><term>warning-text</term><description>Content</description></item>
/// <item><term>details</term><description>Heading (summary text), Content (expanded body)</description></item>
/// <item><term>body</term><description>Content (paragraph text)</description></item>
/// <item><term>heading</term><description>Level (1-6), Content (heading text)</description></item>
/// <item><term>task-list</term><description>TaskSections (if null/empty, engine auto-generates from workflow states)</description></item>
/// <item><term>accordion</term><description>AccordionSections</description></item>
/// </list>
/// </remarks>
public record PrismComponentDefinition
{
    /// <summary>The GDS component type (e.g. "fieldset", "summary-list", "panel", "body", "heading").</summary>
    public string Type { get; init; } = "fieldset";

    // Fieldset + summary-list
    /// <summary>Key of the field group to render (used by fieldset and summary-list components).</summary>
    public string? FieldGroupKey { get; init; }
    /// <summary>Overrides the field group DisplayName as the legend for fieldset components.</summary>
    public string? Legend { get; init; }
    /// <summary>Legend size for fieldset components: "xl" | "l" | "m" | "s".</summary>
    public string? LegendSize { get; init; }

    // Summary-list specific
    /// <summary>The state key the "Change" links navigate to (summary-list only).</summary>
    public string? ChangeStateKey { get; init; }
    /// <summary>Heading above the summary list, overriding the field group DisplayName (summary-list only).</summary>
    public string? Title { get; init; }

    // Content types
    /// <summary>Body text or expanded content (panel, inset-text, warning-text, details, body, notification-banner).</summary>
    public string? Content { get; init; }
    /// <summary>Panel title, notification banner heading, or details summary text.</summary>
    public string? Heading { get; init; }
    /// <summary>Banner type for notification-banner components: "info" | "success" | "warning".</summary>
    public string? BannerType { get; init; }
    /// <summary>Heading level 1-6 for "heading" type components.</summary>
    public int? Level { get; init; }

    // Compound
    /// <summary>Accordion sections for "accordion" type components.</summary>
    public IReadOnlyList<PrismAccordionSectionDefinition>? AccordionSections { get; init; }
    /// <summary>Task sections for "task-list" type. If null or empty, the engine auto-generates from workflow states.</summary>
    public IReadOnlyList<PrismTaskSectionDefinition>? TaskSections { get; init; }
}

/// <summary>A section within an accordion component definition.</summary>
public record PrismAccordionSectionDefinition
{
    /// <summary>The accordion section heading.</summary>
    public string Heading { get; init; } = "";
    /// <summary>Optional summary text shown beneath the heading when collapsed.</summary>
    public string? Summary { get; init; }
    /// <summary>Static content for this accordion section.</summary>
    public string? Content { get; init; }
    /// <summary>Key of the field group to render within this accordion section.</summary>
    public string? FieldGroupKey { get; init; }
}

/// <summary>A section within a task-list component definition.</summary>
public record PrismTaskSectionDefinition
{
    /// <summary>The task section heading.</summary>
    public string Heading { get; init; } = "";
    /// <summary>The tasks within this section.</summary>
    public IReadOnlyList<PrismTaskItemDefinition> Tasks { get; init; } = Array.Empty<PrismTaskItemDefinition>();
}

/// <summary>A single task item within a task-list section definition.</summary>
public record PrismTaskItemDefinition
{
    /// <summary>The task label shown to the user.</summary>
    public string Label { get; init; } = "";
    /// <summary>Links to a workflow state (engine resolves to URL).</summary>
    public string? StateKey { get; init; }
    /// <summary>Direct URL for this task (alternative to StateKey).</summary>
    public string? Href { get; init; }
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
