using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;

namespace UmbracoPrism.TestSite;

internal static class TestSiteRuntimeLayout
{
    private const string UnattendedUserName = "Prism Admin";
    private const string UnattendedUserEmail = "admin@prism.local";
    private const string UnattendedUserPassword = "PrismLocal!12345";

    public const string RuntimeRootEnvironmentVariable = "PRISM_TESTSITE_RUNTIME_ROOT";
    public const string ResetRuntimeEnvironmentVariable = "PRISM_TESTSITE_RESET_RUNTIME";

    public static TestSiteRuntimeLayoutState Apply(WebApplicationBuilder builder)
    {
        var runtimeRoot = Environment.GetEnvironmentVariable(RuntimeRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            // SEC-PT2-006: always persist DataProtection keys so cookies and antiforgery
            // tokens survive process restarts. Without this, every restart invalidates all
            // active sessions. Use a directory under ContentRoot so the path is stable
            // across restarts and survives container volume mounts if ContentRoot is mapped.
            // MULTI-INSTANCE: override with Azure Blob, Redis, or another shared store —
            // see https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers
            // PRODUCTION: also call .ProtectKeysWith*() to encrypt the on-disk key ring.
            var fallbackKeyDir = new DirectoryInfo(
                Path.Combine(builder.Environment.ContentRootPath, "App_Data", "prism-keys"));

            builder.Services
                .AddDataProtection()
                .PersistKeysToFileSystem(fallbackKeyDir)
                .SetApplicationName("UmbracoPrism.TestSite");

            return TestSiteRuntimeLayoutState.Disabled;
        }

        runtimeRoot = Path.GetFullPath(runtimeRoot);
        var wasReset = ShouldResetRuntime();

        if (wasReset)
        {
            DeleteDirectoryContents(runtimeRoot);
        }

        var databaseDirectory = Path.Combine(runtimeRoot, "db");
        var keyRingDirectory = Path.Combine(runtimeRoot, "keys");
        var databasePath = Path.Combine(databaseDirectory, "Umbraco.sqlite.db");

        Directory.CreateDirectory(databaseDirectory);
        Directory.CreateDirectory(keyRingDirectory);

        if (!File.Exists(databasePath))
        {
            using var _ = File.Create(databasePath);
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={databasePath};Cache=Shared;Foreign Keys=True;Pooling=True",
            ["Umbraco:CMS:Unattended:InstallUnattended"] = bool.TrueString,
            ["Umbraco:CMS:Unattended:PackageMigrationsUnattended"] = bool.TrueString,
            ["Umbraco:CMS:Unattended:UpgradeUnattended"] = bool.TrueString,
            ["Umbraco:CMS:Unattended:UnattendedUserName"] = UnattendedUserName,
            ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = UnattendedUserEmail,
            ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = UnattendedUserPassword
        });

        builder.Services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingDirectory))
            .SetApplicationName("UmbracoPrism.TestSite");

        return new TestSiteRuntimeLayoutState(runtimeRoot, databasePath, keyRingDirectory, wasReset);
    }

    private static bool ShouldResetRuntime() =>
        bool.TryParse(Environment.GetEnvironmentVariable(ResetRuntimeEnvironmentVariable), out var reset) && reset;

    private static void DeleteDirectoryContents(string runtimeRoot)
    {
        if (!Directory.Exists(runtimeRoot))
        {
            return;
        }

        var directory = new DirectoryInfo(runtimeRoot);

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            childDirectory.Delete(recursive: true);
        }

        foreach (var file in directory.EnumerateFiles())
        {
            file.Delete();
        }
    }
}

internal sealed record TestSiteRuntimeLayoutState(
    string RuntimeRoot,
    string DatabasePath,
    string KeyRingDirectory,
    bool WasReset)
{
    public static readonly TestSiteRuntimeLayoutState Disabled = new(string.Empty, string.Empty, string.Empty, false);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(RuntimeRoot);
}
