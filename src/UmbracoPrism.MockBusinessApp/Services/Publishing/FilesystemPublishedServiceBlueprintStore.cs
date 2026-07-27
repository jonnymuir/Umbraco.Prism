using System.Text.Json;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services.Publishing;

/// <summary>
/// File-backed implementation of <see cref="IServiceBlueprintSourceStore"/> that writes canonical
/// runtime workflow definition JSON to <c>service-blueprints/</c>.
/// </summary>
public sealed class FilesystemPublishedServiceBlueprintStore(string basePath) : IServiceBlueprintSourceStore
{
    // See FilesystemWorkflowSourceStore — same rationale: serializes save's read-check-write
    // so the version compare-and-swap is atomic within this process.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
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
                $"Resolved path '{resolved}' escapes published workflow base directory '{baseFull}'.");
        }
        return resolved;
    }

    public async Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{definitionKey}.json");
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ServiceBlueprint>(stream, ReadOptions, ct);
    }

    public async Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint workflow, int expectedVersion, CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            var current = await LoadAsync(workflow.DefinitionKey, ct);
            var currentVersion = current?.Version ?? 0;
            if (currentVersion != expectedVersion)
            {
                return new ServiceBlueprintSaveResult(
                    Saved: false,
                    CurrentVersion: currentVersion,
                    Location: ResolveSafePath($"{workflow.DefinitionKey}.json"));
            }

            Directory.CreateDirectory(basePath);
            var newVersion = expectedVersion + 1;
            var toSave = workflow with { Version = newVersion };

            var path = ResolveSafePath($"{workflow.DefinitionKey}.json");
            await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, toSave, ServiceBlueprintProjector.CanonicalOptions, ct);
            return new ServiceBlueprintSaveResult(Saved: true, CurrentVersion: newVersion, Location: path);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(basePath))
            return Array.Empty<ServiceBlueprintSourceSummary>();

        var summaries = new List<ServiceBlueprintSourceSummary>();
        foreach (var path in Directory.EnumerateFiles(basePath, "*.json"))
        {
            await using var stream = File.OpenRead(path);
            var workflow = await JsonSerializer.DeserializeAsync<ServiceBlueprint>(stream, ReadOptions, ct);
            if (workflow is not null)
            {
                summaries.Add(new ServiceBlueprintSourceSummary(workflow.DefinitionKey, workflow.DisplayName));
            }
        }

        return summaries
            .OrderBy(summary => summary.DefinitionKey, StringComparer.Ordinal)
            .ToArray();
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
