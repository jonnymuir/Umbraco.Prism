using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.ProcessManager.Services;

/// <summary>
/// Identity sanitizer used only by <see cref="ServiceBlueprintSimulationRunner"/>'s dry-run engine.
/// Not for production rendering — a real host must supply its own <see cref="IServiceContentSanitizer"/>
/// wherever blueprint content actually reaches a browser.
/// </summary>
internal sealed class SimulationContentSanitizer : IServiceContentSanitizer
{
    public string Sanitize(string? html) => html ?? string.Empty;
}
