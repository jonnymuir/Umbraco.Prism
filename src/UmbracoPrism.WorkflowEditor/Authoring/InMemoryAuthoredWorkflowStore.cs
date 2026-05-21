using System.Collections.Concurrent;
using System.Text.Json;

namespace UmbracoPrism.WorkflowEditor.Authoring;

/// <summary>
/// In-memory implementation of <see cref="IAuthoredWorkflowStore"/>.
/// Can be seeded from existing authored workflow documents but never writes changes back to disk.
/// </summary>
public sealed class InMemoryAuthoredWorkflowStore : IAuthoredWorkflowStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ConcurrentDictionary<string, AuthoredWorkflow> _workflows;
    private readonly ConcurrentDictionary<string, JsonException> _invalidDocuments;

    public InMemoryAuthoredWorkflowStore(
        IEnumerable<AuthoredWorkflow>? seedWorkflows = null,
        IEnumerable<KeyValuePair<string, JsonException>>? invalidDocuments = null)
        : this(
            (seedWorkflows ?? [])
            .Select(workflow => new KeyValuePair<string, AuthoredWorkflow>(
                workflow.DefinitionKey,
                workflow)),
            invalidDocuments)
    {
    }

    public InMemoryAuthoredWorkflowStore(
        IEnumerable<KeyValuePair<string, AuthoredWorkflow>>? seedWorkflows,
        IEnumerable<KeyValuePair<string, JsonException>>? invalidDocuments = null)
    {
        _workflows = new ConcurrentDictionary<string, AuthoredWorkflow>(
            (seedWorkflows ?? [])
            .Select(entry => new KeyValuePair<string, AuthoredWorkflow>(
                entry.Key,
                Clone(entry.Value))),
            StringComparer.OrdinalIgnoreCase);

        _invalidDocuments = new ConcurrentDictionary<string, JsonException>(
            invalidDocuments ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    public static InMemoryAuthoredWorkflowStore FromFilesystemDirectory(string basePath)
    {
        var workflows = new List<KeyValuePair<string, AuthoredWorkflow>>();
        var invalidDocuments = new List<KeyValuePair<string, JsonException>>();

        if (Directory.Exists(basePath))
        {
            foreach (var path in Directory.GetFiles(basePath, "*.workflow.json"))
            {
                var key = Path.GetFileNameWithoutExtension(path).Replace(".workflow", "", StringComparison.Ordinal);

                try
                {
                    var workflow = JsonSerializer.Deserialize<AuthoredWorkflow>(
                        File.ReadAllText(path),
                        ReadOptions);

                    if (workflow is not null && !string.IsNullOrWhiteSpace(workflow.DefinitionKey))
                        workflows.Add(new KeyValuePair<string, AuthoredWorkflow>(key, workflow));
                }
                catch (JsonException ex)
                {
                    invalidDocuments.Add(new KeyValuePair<string, JsonException>(key, ex));
                }
            }
        }

        return new InMemoryAuthoredWorkflowStore(workflows, invalidDocuments);
    }

    public Task<IReadOnlyList<AuthoredWorkflowStoreEntry>> ListAsync(CancellationToken ct = default)
    {
        var entries = _workflows
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AuthoredWorkflowStoreEntry
            {
                WorkflowKey = pair.Key,
                Id = pair.Value.Id,
                DefinitionKey = pair.Value.DefinitionKey,
                DisplayName = pair.Value.DisplayName,
                IsLoadable = true
            })
            .Concat(_invalidDocuments
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new AuthoredWorkflowStoreEntry
                {
                    WorkflowKey = pair.Key,
                    IsLoadable = false,
                    ErrorMessage = pair.Value.Message
                }))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AuthoredWorkflowStoreEntry>>(entries);
    }

    public Task<AuthoredWorkflow?> LoadAsync(string workflowKey, CancellationToken ct = default)
    {
        if (_invalidDocuments.TryGetValue(workflowKey, out var invalidDocument))
            throw invalidDocument;

        return Task.FromResult(
            _workflows.TryGetValue(workflowKey, out var workflow)
                ? Clone(workflow)
                : null);
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default)
    {
        var keys = _workflows.Keys
            .Concat(_invalidDocuments.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task<string> SaveAsync(string workflowKey, AuthoredWorkflow workflow, CancellationToken ct = default)
    {
        _workflows[workflowKey] = Clone(workflow);
        _invalidDocuments.TryRemove(workflowKey, out _);
        return Task.FromResult($"memory://authored-workflows/{workflowKey}");
    }

    public Task<string> SaveAsync(AuthoredWorkflow workflow, CancellationToken ct = default)
        => SaveAsync(workflow.DefinitionKey, workflow, ct);

    private static AuthoredWorkflow Clone(AuthoredWorkflow workflow) =>
        JsonSerializer.Deserialize<AuthoredWorkflow>(
            JsonSerializer.Serialize(workflow, WriteOptions),
            ReadOptions)
        ?? throw new InvalidOperationException(
            $"Authored workflow '{workflow.DefinitionKey}' could not be cloned for in-memory storage.");
}
