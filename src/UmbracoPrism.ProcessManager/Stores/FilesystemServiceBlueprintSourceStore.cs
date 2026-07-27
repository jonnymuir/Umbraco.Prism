using System.Text.Json;
using System.Text.Json.Serialization;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.ProcessManager.Stores;

/// <summary>
/// File-backed, dependency-light implementation of <see cref="IServiceBlueprintSourceStore"/> — one
/// <c>{definitionKey}.json</c> file per blueprint under <paramref name="basePath"/>. Reads with
/// plain <see cref="System.Text.Json"/> only (no editor/projector dependency), so it's usable
/// by any standalone host — e.g. the MCP server, which runs as its own process and can't share
/// a running app's in-memory engine.
/// </summary>
public sealed class FilesystemServiceBlueprintSourceStore(string basePath) : IServiceBlueprintSourceStore
{
    // Serializes save's read-check-write so the version compare-and-swap is atomic within this
    // process. Sufficient for a single-process reference app; a real multi-process file-backed
    // store needs OS file locking, and a real database-backed store should use an atomic
    // UPDATE ... WHERE Version = @expectedVersion instead of a lock at all.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

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
                $"Resolved path '{resolved}' escapes blueprint source base directory '{baseFull}'.");
        }
        return resolved;
    }

    public async Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(basePath))
            return Array.Empty<ServiceBlueprintSourceSummary>();

        var summaries = new List<ServiceBlueprintSourceSummary>();
        foreach (var path in Directory.EnumerateFiles(basePath, "*.json"))
        {
            // Deserialize from a fully-read string, not DeserializeAsync(stream, ...) — System.Text.Json's
            // streaming reader can fail to resolve PrismComponent's [JsonDerivedType] polymorphism on
            // some buffer boundaries. The sync string overload (used throughout the rest of the codebase,
            // e.g. FilesystemServiceBlueprintStore) doesn't have this issue.
            var json = await File.ReadAllTextAsync(path, ct);
            var blueprint = JsonSerializer.Deserialize<ServiceBlueprint>(json, ReadOptions);
            if (blueprint is not null)
            {
                summaries.Add(new ServiceBlueprintSourceSummary(blueprint.DefinitionKey, blueprint.DisplayName));
            }
        }

        return summaries
            .OrderBy(summary => summary.DefinitionKey, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{definitionKey}.json");
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<ServiceBlueprint>(json, ReadOptions);
    }

    public async Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint blueprint, int expectedVersion, CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            var current = await LoadAsync(blueprint.DefinitionKey, ct);
            var currentVersion = current?.Version ?? 0;
            if (currentVersion != expectedVersion)
            {
                var existingPath = ResolveSafePath($"{blueprint.DefinitionKey}.json");
                return new ServiceBlueprintSaveResult(Saved: false, CurrentVersion: currentVersion, Location: existingPath);
            }

            Directory.CreateDirectory(basePath);
            var newVersion = expectedVersion + 1;
            var toSave = blueprint with { Version = newVersion };

            var path = ResolveSafePath($"{blueprint.DefinitionKey}.json");
            await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, toSave, WriteOptions, ct);
            return new ServiceBlueprintSaveResult(Saved: true, CurrentVersion: newVersion, Location: path);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{definitionKey}.json");
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }
}
