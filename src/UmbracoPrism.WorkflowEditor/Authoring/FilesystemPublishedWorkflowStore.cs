using System.Text.Json;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.WorkflowEditor.Authoring;

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

    public async Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        var path = Path.Combine(basePath, $"{definitionKey}.json");
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<WorkflowDefinitionFile>(stream, ReadOptions, ct);
    }

    public async Task<string> SaveAsync(WorkflowDefinitionFile workflow, CancellationToken ct = default)
    {
        Directory.CreateDirectory(basePath);

        var path = Path.Combine(basePath, $"{workflow.DefinitionKey}.json");
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workflow, WorkflowProjector.CanonicalOptions, ct);
        return path;
    }
}
