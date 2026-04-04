namespace UmbracoPrism.Core.Models.Branding;

/// <summary>
/// Represents metadata for a single CSS variable discovered in branding files.
/// </summary>
public class BrandingVariableMetadata
{
    /// <summary>
    /// Gets or sets the CSS variable name (e.g., "--prism-primary").
    /// </summary>
    public string Variable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable label from @prism annotation.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description text from @prism annotation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the type of the variable (color, image, url, font, length, text).
    /// Resolved from @prism type override or inferred from @property syntax.
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// Gets or sets the CSS @property syntax declaration (e.g., "&lt;color&gt;").
    /// </summary>
    public string? Syntax { get; set; }

    /// <summary>
    /// Gets or sets the current value from the CSS file.
    /// </summary>
    public string? CurrentValue { get; set; }
}
