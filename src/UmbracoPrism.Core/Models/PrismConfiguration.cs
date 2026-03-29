namespace UmbracoPrism.Core.Models;

/// <summary>
/// Core configuration options for Umbraco Prism.
/// Bind from appsettings.json under "Prism".
/// </summary>
public class PrismConfiguration
{
    /// <summary>
    /// Configuration section path used to bind Prism options.
    /// </summary>
    public const string SectionName = "Prism";

    /// <summary>
    /// Opt-in flag to seed starter content (Home and Dashboard pages) on first run.
    /// Only applies if the content tree is empty.
    /// Default: false.
    /// </summary>
    public bool SeedStarterContent { get; set; } = false;
}
