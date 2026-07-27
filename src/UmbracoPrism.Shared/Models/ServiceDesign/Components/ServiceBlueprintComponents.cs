namespace UmbracoPrism.Shared.Models.ServiceDesign.Components;

/// <summary>
/// Waiting component: displays a message while the blueprint is paused pending external processing.
/// </summary>
public sealed record WaitingComponent : PrismComponent
{
    /// <summary>The main message displayed to the user while waiting.</summary>
    public string Content { get; init; } = "";

    /// <summary>Expected wait time in seconds, used for user expectation management.</summary>
    public int ExpectedWaitSeconds { get; init; }

    /// <summary>How often the client should poll for a stage change, in milliseconds (default: 3000).</summary>
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
    /// Inline polymorphic input definitions to summarise. The summary-list carries its
    /// own field schemas (label, type, options, conditional reveals) so the engine can
    /// render payloads directly without resolving keys against another stage.
    /// </summary>
    public IReadOnlyList<PrismComponent> Children { get; init; } = Array.Empty<PrismComponent>();

    /// <summary>The stage key the "Change" links navigate to.</summary>
    public string? ChangeStateKey { get; init; }

    /// <summary>Optional title displayed above the summary list.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// GDS task list component: displays a list of blueprint tasks grouped by section.
/// </summary>
public sealed record TaskListComponent : PrismComponent
{
    /// <summary>
    /// Task sections. If null or empty, the engine auto-generates sections from blueprint stages.
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

    /// <summary>Links to a blueprint stage (engine resolves to URL).</summary>
    public string? StageKey { get; init; }

    /// <summary>Direct URL for this task (alternative to StageKey).</summary>
    public string? Href { get; init; }
}

/// <summary>
/// Guidance checklist component: a set of linked guidance articles, each with its own
/// acknowledgement checkbox. Posts as a single field — a comma-joined list of acknowledged
/// item keys, the same wire shape as <see cref="CheckboxesComponent"/> — but unlike a plain
/// checkbox list, <c>Required</c> here means every configured item must be acknowledged, not
/// merely that at least one is checked.
/// </summary>
public sealed record GuidanceChecklistComponent : InputComponent
{
    /// <summary>The guidance items to acknowledge.</summary>
    public IReadOnlyList<GuidanceChecklistItem> Items { get; init; } = Array.Empty<GuidanceChecklistItem>();
}

/// <summary>
/// A single guidance article link and its acknowledgement key within a
/// <see cref="GuidanceChecklistComponent"/>.
/// </summary>
public sealed record GuidanceChecklistItem
{
    /// <summary>Stable identifier posted when this item is acknowledged.</summary>
    public string Key { get; init; } = "";

    /// <summary>The guidance item's label.</summary>
    public string Label { get; init; } = "";

    /// <summary>URL of the CMS-managed article to open (typically in a new tab).</summary>
    public string Href { get; init; } = "";
}
