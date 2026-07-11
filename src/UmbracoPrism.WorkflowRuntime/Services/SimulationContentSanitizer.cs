using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.WorkflowRuntime.Services;

/// <summary>
/// Identity sanitizer used only by <see cref="WorkflowSimulationRunner"/>'s dry-run engine.
/// Not for production rendering — a real host must supply its own <see cref="IWorkflowContentSanitizer"/>
/// wherever workflow content actually reaches a browser.
/// </summary>
internal sealed class SimulationContentSanitizer : IWorkflowContentSanitizer
{
    public string Sanitize(string? html) => html ?? string.Empty;
}
