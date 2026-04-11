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
    /// Valid values: ask_now, wait, complete, error.
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
    /// Only present when ResponseState is "wait".
    /// </summary>
    public int? PollAfterMs { get; init; }

    /// <summary>
    /// Gets the render payload for UI presentation (nullable).
    /// Only present when ResponseState is "ask_now".
    /// </summary>
    public WorkflowRenderPayload? Render { get; init; }

    /// <summary>
    /// Gets the list of validation or error problems.
    /// </summary>
    public IReadOnlyList<WorkflowProblem> Problems { get; init; } = Array.Empty<WorkflowProblem>();
}

/// <summary>
/// Render payload for UI presentation.
/// </summary>
public record WorkflowRenderPayload
{
    /// <summary>
    /// Gets the archetype for UI rendering.
    /// </summary>
    public required string Archetype { get; init; }

    /// <summary>
    /// Gets the state display name.
    /// </summary>
    public required string StateDisplayName { get; init; }

    /// <summary>
    /// Gets the field groups to render.
    /// </summary>
    public required IReadOnlyList<FieldGroupRenderPayload> FieldGroups { get; init; }

    /// <summary>
    /// Gets the available actions the user can take.
    /// </summary>
    public required IReadOnlyList<WorkflowAction> AvailableActions { get; init; }
}

/// <summary>
/// Field group render payload.
/// </summary>
public record FieldGroupRenderPayload
{
    /// <summary>
    /// Gets the field group key.
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the fields in this group.
    /// </summary>
    public required IReadOnlyList<FieldRenderPayload> Fields { get; init; }
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
    /// Gets the options for select/radio fields (nullable).
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

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
