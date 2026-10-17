namespace UmbracoPrism.TestSite;

/// <summary>
/// The reference site's own settings, bound from the <c>TestSite</c> configuration section.
/// </summary>
public class TestSiteOptions
{
    public const string SectionName = "TestSite";

    /// <summary>
    /// Seeds the demo content (Home, Dashboard, Settings, the demo services) on startup.
    /// </summary>
    public bool SeedStarterContent { get; set; }
}
