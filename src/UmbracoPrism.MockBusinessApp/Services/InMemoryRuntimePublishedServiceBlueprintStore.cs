using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// A plain in-memory dictionary backing <c>/mockapp/service-blueprints/*</c> and the AI/tooling
/// authoring surface — author/list/read/save/delete only. No workflow engine involved: this
/// mock app no longer hosts service-request execution at all (that's Wayfinder.Umbraco's job
/// now, in-process inside whichever Umbraco site is the real host), only the shared
/// blueprint-authoring demo, so there's nothing here to keep in sync with an engine's own
/// definition store the way the previous <c>BusinessAppProcessManager</c>-backed version needed.
/// </summary>
public sealed class InMemoryRuntimePublishedServiceBlueprintStore : IServiceBlueprintSourceStore
{
    private readonly Dictionary<string, ServiceBlueprint> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saveLock = new();

    public Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default) =>
        Task.FromResult(_definitions.GetValueOrDefault(definitionKey));

    public Task<ServiceBlueprintSaveResult> SaveAsync(ServiceBlueprint workflow, int expectedVersion, CancellationToken ct = default)
    {
        lock (_saveLock)
        {
            var currentVersion = _definitions.TryGetValue(workflow.DefinitionKey, out var current) ? current.Version : 0;

            if (currentVersion != expectedVersion)
            {
                return Task.FromResult(new ServiceBlueprintSaveResult(
                    Saved: false,
                    CurrentVersion: currentVersion,
                    Location: $"memory://published-workflows/{workflow.DefinitionKey}"));
            }

            var newVersion = expectedVersion + 1;
            var toSave = workflow with { Version = newVersion };
            _definitions[workflow.DefinitionKey] = toSave;

            return Task.FromResult(new ServiceBlueprintSaveResult(
                Saved: true,
                CurrentVersion: newVersion,
                Location: $"memory://published-workflows/{workflow.DefinitionKey}"));
        }
    }

    public Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        var summaries = _definitions.Values
            .OrderBy(workflow => workflow.DefinitionKey, StringComparer.Ordinal)
            .Select(workflow => new ServiceBlueprintSourceSummary(workflow.DefinitionKey, workflow.DisplayName))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ServiceBlueprintSourceSummary>>(summaries);
    }

    public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default)
    {
        lock (_saveLock)
        {
            return Task.FromResult(_definitions.Remove(definitionKey));
        }
    }
}
