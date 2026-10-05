using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayfinder.Engine.Extensions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Registers TestSite's support systems exactly once per test process, from the same
/// <c>appsettings.json</c> and <c>appsettings.Development.json</c> TestSite boots with, through the
/// same <c>AddConfiguredSupportSystems</c> path, so a test of a blueprint that calls one is also a
/// test of its configuration. <c>SupportSystemRegistry</c> freezes on first read, so every test
/// class that needs it touches <see cref="EnsureRegistered"/> from its static constructor: the
/// first one to run registers all of them before anything can read the registry.
/// </summary>
internal static class TestSupportSystems
{
    static TestSupportSystems()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
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
    }

    internal static void EnsureRegistered()
    {
        // Touching this type runs the static constructor above, once.
    }
}
