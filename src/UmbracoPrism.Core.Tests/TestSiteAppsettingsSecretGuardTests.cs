using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Repo-level guard against reintroducing tracked Umbraco imaging HMAC secrets.
/// </summary>
public class TestSiteAppsettingsSecretGuardTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void TrackedAppsettingsFiles_MustNotContain_UmbracoImagingHmacSecretKey()
    {
        var offendingFiles = GetTrackedAppsettingsFiles()
            .Where(ContainsTrackedHmacSecretKey)
            .Select(path => Path.GetRelativePath(RepoRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        offendingFiles.Should().BeEmpty(
            because:
            "Umbraco:CMS:Imaging:HMACSecretKey must never be committed in tracked appsettings*.json files. " +
            "Keep it in src/UmbracoPrism.TestSite/appsettings.Local.json or user-secrets instead.");
    }

    private static IReadOnlyList<string> GetTrackedAppsettingsFiles()
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(":(glob)**/appsettings*.json");

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull("git must be available for repository validation tests");

        var output = process!.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.Should().Be(0,
            because: $"git ls-files must succeed when resolving tracked appsettings files. stderr: {error}");

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).StartsWith("appsettings-schema", StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(Path.GetFileName(path), "appsettings.Local.json", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(Path.Combine(RepoRoot, path)))
            .ToArray();
    }

    private static bool ContainsTrackedHmacSecretKey(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (!TryGetProperty(root, "Umbraco", out var umbraco) ||
            !TryGetProperty(umbraco, "CMS", out var cms) ||
            !TryGetProperty(cms, "Imaging", out var imaging) ||
            !TryGetProperty(imaging, "HMACSecretKey", out _))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
