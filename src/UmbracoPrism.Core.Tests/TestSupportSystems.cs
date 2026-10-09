using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayfinder.Engine.Extensions;
using UmbracoPrism.TestSite.Services.ServiceDesign;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Registers TestSite's support systems exactly once per test process, from the same
/// <c>appsettings.json</c> and <c>appsettings.Development.json</c> TestSite boots with, through the
/// same <c>AddConfiguredSupportSystems</c> path, so a test of a blueprint that calls one is also a
/// test of its configuration. The files are read from TestSite's own folder, never from the test
/// output: several referenced projects ship an <c>appsettings.json</c>, and which one a clean build
/// leaves in the output is not under this project's control. <c>SupportSystemRegistry</c> freezes on first read, so every test
/// class that needs it touches <see cref="EnsureRegistered"/> from its static constructor: the
/// first one to run registers all of them before anything can read the registry.
/// </summary>
internal static class TestSupportSystems
{
    static TestSupportSystems()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(FindTestSiteDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JUGGLING_LICENCE_SIGNING_KEY"] = "test-signing-key",
                ["JUGGLING_LICENCE_CALLBACK_SECRET"] = "test-callback-secret",
                ["BUTTERFLY_IDENTIFICATION_SIGNING_KEY"] = "test-signing-key",
            })
            .Build();

        new ServiceCollection().AddConfiguredSupportSystems(configuration);

        // Registered in code by TestSiteComposer, not from configuration, so it is added by hand here.
        MockBusinessAppProfile.Register();
    }

    internal static void EnsureRegistered()
    {
        // Touching this type runs the static constructor above, once.
    }

    internal static string FindTestSiteDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "UmbracoPrism.TestSite");
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find UmbracoPrism.TestSite's appsettings.json walking up from {AppContext.BaseDirectory}.");
    }
}
