using Wayfinder.Services.Sanitization;

namespace Wayfinder.Umbraco.Services.Sanitization;

/// <summary>
/// Identity sanitizer that returns input unchanged. Retained as a test fixture only —
/// use it in tests that need a predictable, side-effect-free implementation of
/// <see cref="IServiceContentSanitizer"/> without exercising the real security policy.
/// Production DI registration uses <see cref="ServiceContentSanitizer"/> (Ganss.Xss-backed GDS allowlist).
/// </summary>
internal sealed class NoOpServiceContentSanitizer : IServiceContentSanitizer
{
    /// <inheritdoc />
    public string Sanitize(string? html) => html ?? string.Empty;
}
