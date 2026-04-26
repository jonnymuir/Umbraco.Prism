namespace UmbracoPrism.Shared.Models.Workflow.Components;

/// <summary>
/// Waiting component: displays a message while the workflow is paused pending external processing.
/// </summary>
public sealed record WaitingComponent : PrismComponent
{
    /// <summary>The main message displayed to the user while waiting.</summary>
    public string Content { get; init; } = "";

    /// <summary>Expected wait time in seconds, used for user expectation management.</summary>
    public int ExpectedWaitSeconds { get; init; }

    /// <summary>How often the client should poll for a state change, in milliseconds (default: 3000).</summary>
    public int PollIntervalMs { get; init; } = 3000;

    /// <summary>Whether to show the "leave and return later" defer option (default: true).</summary>
    public bool AllowDefer { get; init; } = true;

    /// <summary>Optional custom message for the defer option.</summary>
    public string? DeferMessage { get; init; }
}

/// <summary>
/// GDS summary list component: displays a list of field values with optional change links.
/// </summary>
public sealed record SummaryListComponent : PrismComponent
{
    /// <summary>
    /// Field keys to display in the summary list.
    /// The engine resolves labels and values from the workflow definition tree.
    /// </summary>
    public IReadOnlyList<string> FieldRefs { get; init; } = Array.Empty<string>();

    /// <summary>The state key the "Change" links navigate to.</summary>
    public string? ChangeStateKey { get; init; }

    /// <summary>Optional title displayed above the summary list.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// GDS task list component: displays a list of workflow tasks grouped by section.
/// </summary>
public sealed record TaskListComponent : PrismComponent
{
    /// <summary>
    /// Task sections. If null or empty, the engine auto-generates sections from workflow states.
    /// </summary>
    public IReadOnlyList<TaskSection>? Sections { get; init; }
}

/// <summary>
/// A section within a task list component.
/// </summary>
public sealed record TaskSection
{
    /// <summary>The section heading.</summary>
    public string Heading { get; init; } = "";

    /// <summary>The tasks within this section.</summary>
    public IReadOnlyList<TaskItem> Tasks { get; init; } = Array.Empty<TaskItem>();
}

/// <summary>
/// A single task item within a task list section.
/// </summary>
public sealed record TaskItem
{
    /// <summary>The task label shown to the user.</summary>
    public string Label { get; init; } = "";

    /// <summary>Links to a workflow state (engine resolves to URL).</summary>
    public string? StateKey { get; init; }

    /// <summary>Direct URL for this task (alternative to StateKey).</summary>
    public string? Href { get; init; }
}
