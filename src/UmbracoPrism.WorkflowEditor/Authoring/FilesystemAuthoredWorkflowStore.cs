using System.Text.Json;

namespace UmbracoPrism.WorkflowEditor.Authoring;

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

    /// <summary>
    /// Defence-in-depth path guard. The endpoint layer rejects unsafe keys against
    /// <c>^[a-zA-Z0-9_-]+$</c>, but the store re-asserts that the resolved path lives
    /// inside <see cref="_basePath"/> so callers cannot smuggle traversal through any
    /// other entry point.
    /// </summary>
    private string ResolveSafePath(string fileName)
    {
        var combined = Path.Combine(_basePath, fileName);
        var resolved = Path.GetFullPath(combined);
        var baseFull = Path.GetFullPath(_basePath);
        if (!resolved.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(resolved, baseFull, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved path '{resolved}' escapes authored workflow base directory '{baseFull}'.");
        }
        return resolved;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuthoredWorkflowStoreEntry>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_basePath))
            return [];

        var entries = new List<AuthoredWorkflowStoreEntry>();

        foreach (var path in Directory.GetFiles(_basePath, "*.workflow.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var workflowKey = Path.GetFileNameWithoutExtension(path).Replace(".workflow", "", StringComparison.Ordinal);

            try
            {
                await using var stream = File.OpenRead(path);
                var workflow = await JsonSerializer.DeserializeAsync<AuthoredWorkflow>(stream, ReadOptions, ct);

                if (workflow is not null)
                {
                    entries.Add(new AuthoredWorkflowStoreEntry
                    {
                        WorkflowKey = workflowKey,
                        Id = workflow.Id,
                        DefinitionKey = workflow.DefinitionKey,
                        DisplayName = workflow.DisplayName,
                        IsLoadable = !string.IsNullOrWhiteSpace(workflow.DefinitionKey)
                    });
                    continue;
                }
            }
            catch (JsonException ex)
            {
                entries.Add(new AuthoredWorkflowStoreEntry
                {
                    WorkflowKey = workflowKey,
                    IsLoadable = false,
                    ErrorMessage = ex.Message
                });
                continue;
            }

            entries.Add(new AuthoredWorkflowStoreEntry
            {
                WorkflowKey = workflowKey,
                IsLoadable = false,
                ErrorMessage = $"Workflow '{workflowKey}' could not be loaded from the authored store."
            });
        }

        return entries;
    }

    /// <inheritdoc/>
    public async Task<AuthoredWorkflow?> LoadAsync(string workflowKey, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{workflowKey}.workflow.json");

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
    public Task<string> SaveAsync(string workflowKey, AuthoredWorkflow workflow, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_basePath);

        var path = ResolveSafePath($"{workflowKey}.workflow.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        return SaveAsync(path, workflow, options, ct);
    }

    /// <inheritdoc/>
    public async Task<string> SaveAsync(AuthoredWorkflow workflow, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{workflow.DefinitionKey}.workflow.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        Directory.CreateDirectory(_basePath);
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workflow, options, ct);

        return path;
    }

    private static async Task<string> SaveAsync(
        string path,
        AuthoredWorkflow workflow,
        JsonSerializerOptions options,
        CancellationToken ct)
    {
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workflow, options, ct);

        return path;
    }
}
