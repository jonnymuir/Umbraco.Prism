using System.Text.Json;

namespace UmbracoPrism.Core.Workflow.Authoring;

/// <summary>
/// File-backed implementation of <see cref="IAuthoredWorkflowStore"/>.
/// Reads <c>*.workflow.json</c> files from a configurable base directory
/// (conventionally <c>workflow-authored/</c> under the repository or service root).
/// Not registered in the live host in V1 — consumed directly in tests via fixture paths.
/// </summary>
public sealed class FilesystemAuthoredWorkflowStore : IAuthoredWorkflowStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _basePath;

    /// <param name="basePath">
    /// Absolute or relative path to the directory containing <c>*.workflow.json</c> files.
    /// </param>
    public FilesystemAuthoredWorkflowStore(string basePath) => _basePath = basePath;

    /// <inheritdoc/>
    public async Task<AuthoredWorkflow?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = Path.Combine(_basePath, $"{definitionKey}.workflow.json");

        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AuthoredWorkflow>(stream, ReadOptions, ct);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_basePath))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var keys = Directory
            .GetFiles(_basePath, "*.workflow.json")
            .Select(f => Path.GetFileNameWithoutExtension(f).Replace(".workflow", ""))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    /// <inheritdoc/>
    public async Task<string> SaveAsync(AuthoredWorkflow workflow, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_basePath);

        var path = Path.Combine(_basePath, $"{workflow.DefinitionKey}.workflow.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workflow, options, ct);

        return path;
    }
}
