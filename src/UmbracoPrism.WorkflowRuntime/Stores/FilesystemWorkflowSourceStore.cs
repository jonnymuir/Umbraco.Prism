using System.Text.Json;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.WorkflowRuntime.Stores;

/// <summary>
/// File-backed, dependency-light implementation of <see cref="IWorkflowSourceStore"/> — one
/// <c>{definitionKey}.json</c> file per workflow under <paramref name="basePath"/>. Reads with
/// plain <see cref="System.Text.Json"/> only (no editor/projector dependency), so it's usable
/// by any standalone host — e.g. the MCP server, which runs as its own process and can't share
/// a running app's in-memory engine.
/// </summary>
public sealed class FilesystemWorkflowSourceStore(string basePath) : IWorkflowSourceStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // PrismComponent is a [JsonPolymorphic] type; not every seed file's components have
        // "type" written first (e.g. information-request.json), so this must be relaxed —
        // matches ReferenceWorkflowRepository's production JsonOptions.
        AllowOutOfOrderMetadataProperties = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private string ResolveSafePath(string fileName)
    {
        var combined = Path.Combine(basePath, fileName);
        var resolved = Path.GetFullPath(combined);
        var baseFull = Path.GetFullPath(basePath);
        if (!resolved.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(resolved, baseFull, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved path '{resolved}' escapes workflow source base directory '{baseFull}'.");
        }
        return resolved;
    }

    public async Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(basePath))
            return Array.Empty<WorkflowSourceSummary>();

        var summaries = new List<WorkflowSourceSummary>();
        foreach (var path in Directory.EnumerateFiles(basePath, "*.json"))
        {
            // Deserialize from a fully-read string, not DeserializeAsync(stream, ...) — System.Text.Json's
            // streaming reader can fail to resolve PrismComponent's [JsonDerivedType] polymorphism on
            // some buffer boundaries. The sync string overload (used throughout the rest of the codebase,
            // e.g. FilesystemWorkflowDefinitionStore) doesn't have this issue.
            var json = await File.ReadAllTextAsync(path, ct);
            var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, ReadOptions);
            if (workflow is not null)
            {
                summaries.Add(new WorkflowSourceSummary(workflow.DefinitionKey, workflow.DisplayName));
            }
        }

        return summaries
            .OrderBy(summary => summary.DefinitionKey, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{definitionKey}.json");
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<WorkflowDefinitionFile>(json, ReadOptions);
    }

    public async Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default)
    {
        Directory.CreateDirectory(basePath);

        var path = ResolveSafePath($"{workflow.DefinitionKey}.json");
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workflow, WriteOptions, ct);
        return path;
    }
}
