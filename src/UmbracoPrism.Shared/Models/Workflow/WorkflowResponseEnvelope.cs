using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Standard API response envelope for workflow operations.
/// </summary>
public record WorkflowResponseEnvelope
{
    /// <summary>
    /// Gets the workflow instance identifier.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the response state indicating what the client should do next.
    /// Valid values: render, defer, complete, error.
    /// </summary>
    public required string ResponseState { get; init; }

    /// <summary>
    /// Gets the current state version for optimistic concurrency control.
    /// </summary>
    public required int StateVersion { get; init; }

    /// <summary>
    /// Gets the correlation identifier for tracking related workflow instances.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the server UTC timestamp.
    /// </summary>
    public required DateTimeOffset ServerTimeUtc { get; init; }

    /// <summary>
    /// Gets the recommended polling interval in milliseconds (nullable).
    /// Only present when ResponseState is "defer".
    /// </summary>
    public int? PollAfterMs { get; init; }

    /// <summary>
    /// Gets the render payload for UI presentation (nullable).
    /// Only present when ResponseState is "render".
    /// </summary>
    public StepContent? Render { get; init; }

    /// <summary>
    /// Gets the instance policy from the workflow definition.
    /// Valid values: "single", "multiple", "prompt".
    /// </summary>
    public string? InstancePolicy { get; init; }

    /// <summary>
    /// Gets the list of validation or error problems.
    /// </summary>
    public IReadOnlyList<WorkflowProblem> Problems { get; init; } = Array.Empty<WorkflowProblem>();
}

/// <summary>
/// Render payload for UI presentation.
/// </summary>
public record StepContent
{
    /// <summary>
    /// Gets the step type for UI rendering (question, check-answers, confirmation, status-timeline, task-list).
    /// </summary>
    public required string StepType { get; init; }

    /// <summary>
    /// Gets the state display name.
    /// </summary>
    public required string StateDisplayName { get; init; }

    /// <summary>
    /// Gets the GDS components to render at this step.
    /// </summary>
    public required IReadOnlyList<PrismComponentRenderPayload> Components { get; init; }

    /// <summary>
    /// Gets the available actions the user can take.
    /// </summary>
    public required IReadOnlyList<WorkflowAction> AvailableActions { get; init; }
}

/// <summary>
/// Runtime representation of a GDS component, with field values pre-populated from the workflow instance.
/// Sent from the engine to the Core controller, which passes it to the view.
/// </summary>
public record PrismComponentRenderPayload
{
    /// <summary>The GDS component type (e.g. "fieldset", "summary-list", "panel", "body", "heading").</summary>
    public string Type { get; init; } = "fieldset";

    // Fieldset
    /// <summary>The fieldset legend text (overrides the field group DisplayName).</summary>
    public string? Legend { get; init; }
    /// <summary>Legend size: "xl" | "l" | "m" | "s".</summary>
    public string? LegendSize { get; init; }
    /// <summary>Fields to render within this component (used by fieldset and summary-list).</summary>
    public IReadOnlyList<FieldRenderPayload> Fields { get; init; } = Array.Empty<FieldRenderPayload>();

    // Summary-list
    /// <summary>Heading above the summary list (overrides the field group DisplayName).</summary>
    public string? Title { get; init; }
    /// <summary>The state key the "Change" links navigate to (summary-list only).</summary>
    public string? SourceStateKey { get; init; }

    // Content types
    /// <summary>Body text or expanded content (panel, inset-text, warning-text, details, body, notification-banner).</summary>
    public string? Content { get; init; }
    /// <summary>Panel title, notification banner heading, or details summary text.</summary>
    public string? Heading { get; init; }
    /// <summary>Banner type for notification-banner: "info" | "success" | "warning".</summary>
    public string? BannerType { get; init; }
    /// <summary>Heading level 1-6 for "heading" type components.</summary>
    public int? Level { get; init; }

    // Task list
    /// <summary>Task sections for task-list components.</summary>
    public IReadOnlyList<PrismTaskSection>? TaskSections { get; init; }

    // Accordion
    /// <summary>Accordion sections for accordion components.</summary>
    public IReadOnlyList<PrismAccordionSectionPayload>? AccordionSections { get; init; }

    // Waiting
    /// <summary>Expected wait time in seconds for "waiting" components.</summary>
    public int? ExpectedWaitSeconds { get; init; }
    /// <summary>Polling interval in milliseconds for "waiting" components.</summary>
    public int? PollIntervalMs { get; init; }
    /// <summary>Allow deferral of waiting for "waiting" components.</summary>
    public bool? AllowDefer { get; init; }
    /// <summary>Message to show if the user defers the wait (for "waiting" components).</summary>
    public string? DeferMessage { get; init; }

    /// <summary>
    /// Computed display name for this component — returns the most semantically appropriate heading property
    /// based on component type. Used by views that iterate over components and render section headings.
    /// </summary>
    public string DisplayName => Type switch
    {
        "fieldset" => Legend ?? "",
        "summary-list" => Title ?? "",
        _ => Heading ?? ""
    };
}

/// <summary>A section within a rendered task-list component.</summary>
public record PrismTaskSection
{
    /// <summary>The task section heading.</summary>
    public string Heading { get; init; } = "";
    /// <summary>The tasks within this section.</summary>
    public IReadOnlyList<PrismTaskItem> Tasks { get; init; } = Array.Empty<PrismTaskItem>();
}

/// <summary>A single rendered task item within a task-list section.</summary>
public record PrismTaskItem
{
    /// <summary>The task label shown to the user.</summary>
    public string Label { get; init; } = "";
    /// <summary>The resolved URL for this task.</summary>
    public string? Href { get; init; }
    /// <summary>Task status: "not-started" | "in-progress" | "completed" | "cannot-start".</summary>
    public string Status { get; init; } = "not-started";
}

/// <summary>A rendered accordion section with populated fields.</summary>
public record PrismAccordionSectionPayload
{
    /// <summary>The accordion section heading.</summary>
    public string Heading { get; init; } = "";
    /// <summary>Optional summary text shown beneath the heading when collapsed.</summary>
    public string? Summary { get; init; }
    /// <summary>Static content for this accordion section.</summary>
    public string? Content { get; init; }
    /// <summary>Fields rendered within this accordion section.</summary>
    public IReadOnlyList<FieldRenderPayload> Fields { get; init; } = Array.Empty<FieldRenderPayload>();
}

/// <summary>
/// Individual field render payload.
/// </summary>
public record FieldRenderPayload
{
    /// <summary>
    /// Gets the field key.
    /// </summary>
    public required string FieldKey { get; init; }

    /// <summary>
    /// Gets the field label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the hint text.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Gets the field type (text, email, number, select, etc.).
    /// </summary>
    public required string FieldType { get; init; }

    /// <summary>
    /// Gets whether the field is required.
    /// </summary>
    public required bool Required { get; init; }

    /// <summary>
    /// Gets the current field value (nullable).
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Gets the default value to pre-populate the field (nullable).
    /// Takes precedence over user-submitted values.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets whether the field is read-only and cannot be edited by the user.
    /// Read-only fields are rendered as disabled inputs or plain text.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Gets the options for select/radio fields (nullable).
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>
    /// Gets the currency/unit prefix displayed before the input (e.g., "£").
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// For radios/checkboxes: sub-fields revealed when the parent option is selected.
    /// Key is the option value; value is the list of fields shown when that option is active.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FieldRenderPayload>>? ConditionalFields { get; init; }

    /// <summary>
    /// Gets the minimum character length for text/textarea fields (nullable).
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    /// Gets the maximum character length for text/textarea fields (nullable).
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Gets the HTML5 pattern (regex) attribute value for text/email fields (nullable).
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Gets the minimum value for number fields (nullable).
    /// </summary>
    public decimal? Min { get; init; }

    /// <summary>
    /// Gets the maximum value for number fields (nullable).
    /// </summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// The field key this field depends on for visibility. When set, this field is only
    /// shown when the dependency field's value matches <see cref="VisibleWhen"/>.
    /// </summary>
    public string? ConditionalOn { get; init; }

    /// <summary>
    /// The value that makes this field visible when <see cref="ConditionalOn"/> is set.
    /// </summary>
    public string? VisibleWhen { get; init; }

    /// <summary>
    /// Gets the content to render for non-input content field types
    /// (inset-text, warning-text, details, notification-banner).
    /// </summary>
    public string? Content { get; init; }
}

/// <summary>
/// Workflow action available to the user.
/// </summary>
public record WorkflowAction
{
    /// <summary>
    /// Gets the action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the action label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the action style.
    /// Valid values: primary, secondary, destructive.
    /// </summary>
    public required string Style { get; init; }
}

/// <summary>
/// Validation or error problem.
/// </summary>
public record WorkflowProblem
{
    /// <summary>
    /// Gets the field key this problem relates to.
    /// </summary>
    public required string FieldKey { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public required string Code { get; init; }
}
