using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Umbraco.Cms.Core.Cache;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class BrandingServiceTests
{
    [Fact]
    public void GetBrandingTabs_ReadsVariablesFromCssFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            var cssPath = Path.Combine(root, "branding", "prism-branding.css");
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(cssPath, ":root { --prism-primary: #4f46e5; --prism-accent: #22c55e; }");

            var excludedPath = Path.Combine(root, "node_modules", "ignore.css");
            Directory.CreateDirectory(Path.GetDirectoryName(excludedPath)!);
            File.WriteAllText(excludedPath, ":root { --should-not-read: #000; }");

            var service = CreateService(root);

            var tabs = service.GetBrandingTabs();

            tabs.Should().HaveCount(1);
            tabs[0].Label.Should().Be("prism-branding.css");
            tabs[0].Variables.Should().Contain(v => v.Name == "--prism-primary" && v.DefaultValue == "#4f46e5");
            tabs[0].Variables.Should().Contain(v => v.Name == "--prism-accent" && v.DefaultValue == "#22c55e");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetBrandingTabsWithOverrides_IncludesOtherStylesTabForUnknownVariables()
    {
        var root = CreateTempDirectory();
        try
        {
            var cssPath = Path.Combine(root, "branding", "prism-branding.css");
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(cssPath, ":root { --prism-primary: #4f46e5; --prism-accent: #22c55e; }");

            var service = CreateService(root);
            var overrides = new Dictionary<string, string>
            {
                ["--prism-primary"] = "#111111",
                ["--custom-radius"] = "12px"
            };

            var tabs = service.GetBrandingTabsWithOverrides(overrides);

            tabs.Should().ContainSingle(tab => tab.Label == "prism-branding.css");
            tabs.Should().ContainSingle(tab => tab.Label == "Other Styles");

            var mainTab = tabs.Single(tab => tab.Label == "prism-branding.css");
            mainTab.Variables.Should().Contain(v => v.Name == "--prism-primary" && v.OverrideValue == "#111111");

            var otherTab = tabs.Single(tab => tab.Label == "Other Styles");
            otherTab.Variables.Should().ContainSingle(v => v.Name == "--custom-radius" && v.OverrideValue == "12px");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static BrandingService CreateService(string contentRoot)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(contentRoot);
        return new BrandingService(env.Object, AppCaches.NoCache);
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "UmbracoPrismTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
