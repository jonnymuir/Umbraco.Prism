using System.Text.Json;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Reads <c>*.json</c> fixture files from disk for tests that exercise the
/// projection / patch pipeline. Replaces the production-side
/// <c>FilesystemAuthoredServiceBlueprintStore</c> after that type was retired in Slice B
/// (the service blueprint editor no longer ships a server-side authored blueprint store —
/// hosts implement <c>ServiceBlueprintSource</c> in TypeScript instead).
/// </summary>
internal static class AuthoredServiceBlueprintFixtureLoader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<AuthoredServiceBlueprint?> LoadAsync(
        string basePath,
        string blueprintKey,
        CancellationToken ct = default)
    {
        var path = Path.Combine(basePath, $"{blueprintKey}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AuthoredServiceBlueprint>(stream, ReadOptions, ct);
    }

    public static IReadOnlyList<string> ListKeys(string basePath)
    {
        if (!Directory.Exists(basePath))
        {
            return Array.Empty<string>();
        }

        return Directory
            .GetFiles(basePath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
    }
}
