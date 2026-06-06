using System.Text.Json;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowEditor.Authoring;

namespace UmbracoPrism.MockBusinessApp.Services.Publishing;

/// <summary>
/// File-backed implementation of <see cref="IPublishedWorkflowStore"/> that writes canonical
/// runtime workflow definition JSON to <c>workflow-seeds/</c>.
/// </summary>
public sealed class FilesystemPublishedWorkflowStore(string basePath) : IPublishedWorkflowStore
{
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

    public async Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = ResolveSafePath($"{definitionKey}.json");
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<WorkflowDefinitionFile>(stream, ReadOptions, ct);
    }

    public async Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default)
    {
        Directory.CreateDirectory(basePath);

        var path = ResolveSafePath($"{workflow.DefinitionKey}.json");
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workflow, WorkflowProjector.CanonicalOptions, ct);
        return path;
    }
}
