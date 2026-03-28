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

    [Fact]
    public void GetBrandingTabsWithOverrides_DoesNotLeakOverridesAcrossTenants_WhenTabsAreRuntimeCached()
    {
        var root = CreateTempDirectory();
        try
        {
            var cssPath = Path.Combine(root, "branding", "prism-branding.css");
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(cssPath, ":root { --prism-primary: #4f46e5; --prism-accent: #22c55e; }");

            var service = CreateServiceWithRuntimeCache(root);

            var tenantA = service.GetBrandingTabsWithOverrides(new Dictionary<string, string>
            {
                ["--prism-primary"] = "#111111"
            });

            var tenantB = service.GetBrandingTabsWithOverrides(new Dictionary<string, string>
            {
                ["--prism-accent"] = "#222222"
            });

            var tenantAVariables = tenantA.Single(tab => tab.Label == "prism-branding.css").Variables;
            tenantAVariables.Single(v => v.Name == "--prism-primary").OverrideValue.Should().Be("#111111");
            tenantAVariables.Single(v => v.Name == "--prism-accent").OverrideValue.Should().BeNull();

            var tenantBVariables = tenantB.Single(tab => tab.Label == "prism-branding.css").Variables;
            tenantBVariables.Single(v => v.Name == "--prism-accent").OverrideValue.Should().Be("#222222");
            tenantBVariables.Single(v => v.Name == "--prism-primary").OverrideValue.Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetBrandingTabsWithOverrides_ReflectsLatestTenantUpdate_OnSubsequentCalls()
    {
        var root = CreateTempDirectory();
        try
        {
            var cssPath = Path.Combine(root, "branding", "prism-branding.css");
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(cssPath, ":root { --prism-primary: #4f46e5; }");

            var service = CreateServiceWithRuntimeCache(root);

            var initial = service.GetBrandingTabsWithOverrides(new Dictionary<string, string>
            {
                ["--prism-primary"] = "#101010"
            });

            var updated = service.GetBrandingTabsWithOverrides(new Dictionary<string, string>
            {
                ["--prism-primary"] = "#202020"
            });

            initial.Single().Variables.Single(v => v.Name == "--prism-primary").OverrideValue.Should().Be("#101010");
            updated.Single().Variables.Single(v => v.Name == "--prism-primary").OverrideValue.Should().Be("#202020");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task GetBrandingTabsWithOverrides_DoesNotLeakOverridesAcrossConcurrentCalls()
    {
        var root = CreateTempDirectory();
        try
        {
            var cssPath = Path.Combine(root, "branding", "prism-branding.css");
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(cssPath, ":root { --prism-primary: #4f46e5; --prism-accent: #22c55e; }");

            var service = CreateServiceWithRuntimeCache(root);
            var expectedColors = Enumerable.Range(1, 24)
                .Select(i => $"#{i:X6}")
                .ToArray();

            var observedColors = await Task.WhenAll(expectedColors.Select(color => Task.Run(() =>
            {
                var tabs = service.GetBrandingTabsWithOverrides(new Dictionary<string, string>
                {
                    ["--prism-primary"] = color
                });

                return tabs
                    .Single(tab => tab.Label == "prism-branding.css")
                    .Variables
                    .Single(variable => variable.Name == "--prism-primary")
                    .OverrideValue;
            })));

            observedColors.Should().Equal(expectedColors);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task GetBrandingTabsWithOverrides_PreservesCachedDefaults_DuringConcurrentOverrideUpdates()
    {
        var root = CreateTempDirectory();
        try
        {
            var cssPath = Path.Combine(root, "branding", "prism-branding.css");
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(cssPath, ":root { --prism-primary: #4f46e5; --prism-accent: #22c55e; }");

            var service = CreateServiceWithRuntimeCache(root);

            var baseline = service.GetBrandingTabs();
            baseline.Single(tab => tab.Label == "prism-branding.css")
                .Variables.Single(variable => variable.Name == "--prism-primary")
                .DefaultValue.Should().Be("#4f46e5");

            var expectedColors = Enumerable.Range(1, 20)
                .Select(index => $"#{index:X6}")
                .ToArray();

            var observed = await Task.WhenAll(expectedColors.Select(color => Task.Run(() =>
            {
                var tabs = service.GetBrandingTabsWithOverrides(new Dictionary<string, string>
                {
                    ["--prism-primary"] = color,
                    ["--custom-radius"] = $"{color.Length}px"
                });

                var mainTab = tabs.Single(tab => tab.Label == "prism-branding.css");
                var otherTab = tabs.Single(tab => tab.Label == "Other Styles");

                return new
                {
                    Primary = mainTab.Variables.Single(variable => variable.Name == "--prism-primary").OverrideValue,
                    Accent = mainTab.Variables.Single(variable => variable.Name == "--prism-accent").OverrideValue,
                    OtherCount = otherTab.Variables.Count
                };
            })));

            observed.Should().OnlyContain(result => result.Accent == null && result.OtherCount == 1);
            observed.Select(result => result.Primary).Should().Equal(expectedColors);

            var refreshedBaseline = service.GetBrandingTabs();
            var refreshedPrimary = refreshedBaseline.Single(tab => tab.Label == "prism-branding.css")
                .Variables.Single(variable => variable.Name == "--prism-primary");

            refreshedPrimary.DefaultValue.Should().Be("#4f46e5");
            refreshedPrimary.OverrideValue.Should().BeNull();
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

    private static BrandingService CreateServiceWithRuntimeCache(string contentRoot)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(contentRoot);

        var runtimeCache = new ObjectCacheAppCache();
        var requestCache = new Mock<IRequestCache>();
        var isolatedCaches = new IsolatedCaches(_ => new ObjectCacheAppCache());
        var appCaches = new AppCaches(runtimeCache, requestCache.Object, isolatedCaches);

        return new BrandingService(env.Object, appCaches);
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "UmbracoPrismTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
