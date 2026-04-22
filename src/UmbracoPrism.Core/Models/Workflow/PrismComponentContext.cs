using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.Core.Models.Workflow;

/// <summary>
/// Pre-computed view model passed to PrismComponent partials.
/// Contains the component payload and the form context needed for interactive components (e.g., summary-list change links).
/// </summary>
public record PrismComponentContext
{
    public required PrismComponentRenderPayload Component { get; init; }

    // Field rendering context (passed through to <prism-field> within fieldset components)
    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    // Form context for components that post (e.g., summary-list "Change" links)
    public string ReturnUrl { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
    public int StateVersion { get; init; }
    public string WorkflowKey { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
}
