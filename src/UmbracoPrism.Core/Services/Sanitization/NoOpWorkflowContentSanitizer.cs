using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Services.Sanitization;

/// <summary>
/// Placeholder sanitizer that returns input unchanged.
/// TODO: Replaced by Copper in SEC-003 T2 — delivers Ganss.Xss-backed allowlist.
/// </summary>
internal sealed class NoOpWorkflowContentSanitizer : IWorkflowContentSanitizer
{
    /// <inheritdoc />
    public string Sanitize(string? html) => html ?? string.Empty;
}
