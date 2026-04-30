namespace UmbracoPrism.Shared.Services.Sanitization;

/// <summary>Sanitizes HTML authored in a workflow definition before it reaches Razor.</summary>
/// <remarks>
/// Implementations must be thread-safe; register as singleton.
/// The engine is the sole producer of <see cref="UmbracoPrism.Shared.Models.Workflow.WorkflowResponseEnvelope"/>
/// payloads; all Content/Heading fields MUST be routed through this sanitizer before the payload is built.
/// </remarks>
public interface IWorkflowContentSanitizer
{
    /// <summary>Sanitize HTML authored in a workflow definition before it reaches Razor.</summary>
    /// <returns>Sanitized HTML safe to render via @Html.Raw. Never null — returns empty string for null input.</returns>
    string Sanitize(string? html);
}
