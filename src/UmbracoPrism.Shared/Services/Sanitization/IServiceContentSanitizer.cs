namespace UmbracoPrism.Shared.Services.Sanitization;

/// <summary>Sanitizes HTML authored in a service blueprint before it reaches Razor.</summary>
/// <remarks>
/// Implementations must be thread-safe; register as singleton.
/// The engine is the sole producer of <see cref="UmbracoPrism.Shared.Models.ServiceDesign.ServiceRequestResponseEnvelope"/>
/// payloads; all Content/Heading fields MUST be routed through this sanitizer before the payload is built.
/// </remarks>
public interface IServiceContentSanitizer
{
    /// <summary>Sanitize HTML authored in a service blueprint before it reaches Razor.</summary>
    /// <returns>Sanitized HTML safe to render via @Html.Raw. Never null — returns empty string for null input.</returns>
    string Sanitize(string? html);
}
