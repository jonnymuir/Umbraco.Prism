using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// <see cref="IWorkflowSourceStore"/> for backoffice-authored CMS Workflow definitions —
/// persists to the prismCmsWorkflowDefinition table (uSync-portable via
/// <c>PrismCmsWorkflowHandler</c>) rather than MockBusinessApp's memory-only reference store.
/// A successful save is pushed straight into <paramref name="engine"/> so the live engine
/// reflects it immediately, matching the promise the AI-authoring surface already makes.
/// </summary>
public sealed class UmbracoCmsWorkflowDefinitionStore(
    IUmbracoDatabaseFactory databaseFactory,
    IWorkflowRuntimeEngine engine) : IWorkflowSourceStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // PrismComponent is a [JsonPolymorphic] type; not every workflow's components have
        // "type" written first, so this must be relaxed — matches FilesystemWorkflowSourceStore.
        AllowOutOfOrderMetadataProperties = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public Task<IReadOnlyList<WorkflowSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismCmsWorkflowDefinitionSchema>(
            "SELECT DefinitionKey, DisplayName FROM prismCmsWorkflowDefinition ORDER BY DefinitionKey");

        IReadOnlyList<WorkflowSourceSummary> summaries = rows
            .Select(row => new WorkflowSourceSummary(row.DefinitionKey, row.DisplayName))
            .ToArray();

        return Task.FromResult(summaries);
    }

    public Task<WorkflowDefinitionFile?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();
        var row = db.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
            "SELECT * FROM prismCmsWorkflowDefinition WHERE DefinitionKey = @0", definitionKey);

        if (row is null)
        {
            return Task.FromResult<WorkflowDefinitionFile?>(null);
        }

        var workflow = JsonSerializer.Deserialize<WorkflowDefinitionFile>(row.Json, ReadOptions);
        return Task.FromResult(workflow is null ? null : workflow with { Version = row.Version });
    }

    public Task<WorkflowSaveResult> SaveAsync(
        WorkflowDefinitionFile workflow, int expectedVersion, CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
            "SELECT * FROM prismCmsWorkflowDefinition WHERE DefinitionKey = @0", workflow.DefinitionKey);

        if (existing is null)
        {
            if (expectedVersion != 0)
            {
                return Task.FromResult(new WorkflowSaveResult(Saved: false, CurrentVersion: 0, Location: "prismCmsWorkflowDefinition"));
            }

            var newRow = new PrismCmsWorkflowDefinitionSchema
            {
                DefinitionKey = workflow.DefinitionKey,
                DisplayName = workflow.DisplayName,
                Json = JsonSerializer.Serialize(workflow with { Version = 1 }, WriteOptions),
                Version = 1,
                UpdatedUtc = DateTime.UtcNow
            };
            db.Insert(newRow);

            engine.UpdateDefinition(workflow.DefinitionKey, workflow with { Version = 1 });
            return Task.FromResult(new WorkflowSaveResult(Saved: true, CurrentVersion: 1, Location: "prismCmsWorkflowDefinition"));
        }

        if (existing.Version != expectedVersion)
        {
            return Task.FromResult(new WorkflowSaveResult(Saved: false, CurrentVersion: existing.Version, Location: "prismCmsWorkflowDefinition"));
        }

        var newVersion = expectedVersion + 1;
        var toSave = workflow with { Version = newVersion };

        // Atomic compare-and-swap: only the writer that still sees `expectedVersion` wins the race.
        var rowsAffected = db.Execute(
            "UPDATE prismCmsWorkflowDefinition SET DisplayName = @0, Json = @1, Version = @2, UpdatedUtc = @3 " +
            "WHERE DefinitionKey = @4 AND Version = @5",
            workflow.DisplayName,
            JsonSerializer.Serialize(toSave, WriteOptions),
            newVersion,
            DateTime.UtcNow,
            workflow.DefinitionKey,
            expectedVersion);

        if (rowsAffected == 0)
        {
            var current = db.FirstOrDefault<PrismCmsWorkflowDefinitionSchema>(
                "SELECT * FROM prismCmsWorkflowDefinition WHERE DefinitionKey = @0", workflow.DefinitionKey);
            return Task.FromResult(new WorkflowSaveResult(
                Saved: false, CurrentVersion: current?.Version ?? existing.Version, Location: "prismCmsWorkflowDefinition"));
        }

        engine.UpdateDefinition(workflow.DefinitionKey, toSave);
        return Task.FromResult(new WorkflowSaveResult(Saved: true, CurrentVersion: newVersion, Location: "prismCmsWorkflowDefinition"));
    }

    public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();
        var rowsAffected = db.Execute(
            "DELETE FROM prismCmsWorkflowDefinition WHERE DefinitionKey = @0", definitionKey);

        if (rowsAffected > 0)
        {
            engine.RemoveDefinition(definitionKey);
        }

        return Task.FromResult(rowsAffected > 0);
    }
}
