using System.Text.Json;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.Core.Tests.Workflow.Authoring;

/// <summary>
/// Reads <c>*.workflow.json</c> fixture files from disk for tests that exercise the
/// projection / patch pipeline. Replaces the production-side
/// <c>FilesystemAuthoredWorkflowStore</c> after that type was retired in Slice B
/// (the workflow editor no longer ships a server-side authored workflow store —
/// hosts implement <c>WorkflowSource</c> in TypeScript instead).
/// </summary>
internal static class AuthoredWorkflowFixtureLoader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<AuthoredWorkflow?> LoadAsync(
        string basePath,
        string workflowKey,
        CancellationToken ct = default)
    {
        var path = Path.Combine(basePath, $"{workflowKey}.workflow.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AuthoredWorkflow>(stream, ReadOptions, ct);
    }

    public static IReadOnlyList<string> ListKeys(string basePath)
    {
        if (!Directory.Exists(basePath))
        {
            return Array.Empty<string>();
        }

        return Directory
            .GetFiles(basePath, "*.workflow.json")
            .Select(p => Path.GetFileNameWithoutExtension(p).Replace(".workflow", string.Empty, StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
    }
}
