using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Services.Sanitization;

/// <summary>
/// Identity sanitizer that returns input unchanged. Retained as a test fixture only —
/// use it in tests that need a predictable, side-effect-free implementation of
/// <see cref="IWorkflowContentSanitizer"/> without exercising the real security policy.
/// Production DI registration uses <see cref="WorkflowContentSanitizer"/> (Ganss.Xss-backed GDS allowlist).
/// </summary>
internal sealed class NoOpWorkflowContentSanitizer : IWorkflowContentSanitizer
{
    /// <inheritdoc />
    public string Sanitize(string? html) => html ?? string.Empty;
}
