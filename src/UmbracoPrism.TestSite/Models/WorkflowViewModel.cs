using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.TestSite.Models;

/// <summary>
/// View model for the WorkflowPage route-hijacking controller.
/// Inherits <see cref="PublishedContentWrapped"/> so Umbraco's ContentModelBinder
/// can satisfy its IPublishedContent requirement during route-hijacking.
/// </summary>
public class WorkflowViewModel : PublishedContentWrapped
{
    public WorkflowViewModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : base(content, publishedValueFallback) { }

    /// <summary>The instance identifier used in form hidden fields.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>The state version for optimistic concurrency — must be echoed in the form.</summary>
    public int StateVersion { get; set; }

    /// <summary>The workflow definition key read from the Umbraco page property.</summary>
    public string WorkflowKey { get; set; } = string.Empty;

    /// <summary>The URL of this page — used as the PRG redirect target after POST.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    // --- Render payload fields (from WorkflowRenderPayload) ---

    /// <summary>Archetype driving the partial view selection: Collect, Review, Completion, etc.</summary>
    public string Archetype { get; set; } = string.Empty;

    /// <summary>Human-readable name for the current state.</summary>
    public string StateDisplayName { get; set; } = string.Empty;

    /// <summary>Field groups to render in Collect steps.</summary>
    public IReadOnlyList<FieldGroupRenderPayload> FieldGroups { get; set; } = Array.Empty<FieldGroupRenderPayload>();

    /// <summary>Actions the user can take (e.g. continue, submit, back).</summary>
    public IReadOnlyList<WorkflowAction> AvailableActions { get; set; } = Array.Empty<WorkflowAction>();

    /// <summary>Validation problems from the previous POST (populated via TempData).</summary>
    public IReadOnlyList<WorkflowProblem> Problems { get; set; } = Array.Empty<WorkflowProblem>();

    /// <summary>Tamper-proof nonce binding this form to its server-side field definitions.</summary>
    public string Nonce { get; set; } = string.Empty;

    /// <summary>Human-readable display name for the workflow (e.g. "Get in Touch").</summary>
    public string WorkflowDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// True when instancePolicy = "prompt" and an active instance already exists for this user.
    /// Causes the view to render the instance picker partial instead of the workflow form.
    /// </summary>
    public bool ShowInstancePicker { get; set; }

    /// <summary>True when the workflow engine returned a fatal error (definition not found, etc.).</summary>
    public bool HasError { get; set; }

    /// <summary>Human-readable error message when <see cref="HasError"/> is true.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Pre-filled field values to repopulate the form after a failed validation round-trip.
    /// Used to preserve user input during PRG redirects (WCAG 3.3.1 compliance).
    /// </summary>
    public IReadOnlyDictionary<string, string> FormValues { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Returns a lookup of the first problem message keyed by field key,
    /// for rendering inline field-level errors.
    /// </summary>
    public IReadOnlyDictionary<string, string> FieldErrors =>
        Problems
            .Where(p => !string.IsNullOrEmpty(p.FieldKey))
            .GroupBy(p => p.FieldKey)
            .ToDictionary(g => g.Key, g => g.First().Message);
}
