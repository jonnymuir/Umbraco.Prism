using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services.ServiceDesign;

/// <summary>
/// <see cref="IServiceBlueprintSourceStore"/> for backoffice-authored CMS Service Blueprint
/// definitions — persists to the prismCmsServiceBlueprint table (uSync-portable) rather than
/// MockBusinessApp's memory-only reference store. A successful save is pushed straight into
/// <paramref name="engine"/> so the live engine reflects it immediately, matching the promise
/// the AI-authoring surface already makes.
/// </summary>
public sealed class UmbracoCmsServiceBlueprintStore(
    IUmbracoDatabaseFactory databaseFactory,
    IProcessManager engine) : IServiceBlueprintSourceStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // PrismComponent is a [JsonPolymorphic] type; not every blueprint's components have
        // "type" written first, so this must be relaxed — matches FilesystemServiceBlueprintSourceStore.
        AllowOutOfOrderMetadataProperties = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public Task<IReadOnlyList<ServiceBlueprintSourceSummary>> ListAsync(CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismCmsServiceBlueprintSchema>(
            "SELECT DefinitionKey, DisplayName FROM prismCmsServiceBlueprint ORDER BY DefinitionKey");

        IReadOnlyList<ServiceBlueprintSourceSummary> summaries = rows
            .Select(row => new ServiceBlueprintSourceSummary(row.DefinitionKey, row.DisplayName))
            .ToArray();

        return Task.FromResult(summaries);
    }

    public Task<ServiceBlueprint?> LoadAsync(string definitionKey, CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();
        var row = db.FirstOrDefault<PrismCmsServiceBlueprintSchema>(
            "SELECT * FROM prismCmsServiceBlueprint WHERE DefinitionKey = @0", definitionKey);

        if (row is null)
        {
            return Task.FromResult<ServiceBlueprint?>(null);
        }

        var blueprint = JsonSerializer.Deserialize<ServiceBlueprint>(row.Json, ReadOptions);
        return Task.FromResult(blueprint is null ? null : blueprint with { Version = row.Version });
    }

    public Task<ServiceBlueprintSaveResult> SaveAsync(
        ServiceBlueprint blueprint, int expectedVersion, CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();

        var existing = db.FirstOrDefault<PrismCmsServiceBlueprintSchema>(
            "SELECT * FROM prismCmsServiceBlueprint WHERE DefinitionKey = @0", blueprint.DefinitionKey);

        if (existing is null)
        {
            if (expectedVersion != 0)
            {
                return Task.FromResult(new ServiceBlueprintSaveResult(Saved: false, CurrentVersion: 0, Location: "prismCmsServiceBlueprint"));
            }

            var newRow = new PrismCmsServiceBlueprintSchema
            {
                DefinitionKey = blueprint.DefinitionKey,
                DisplayName = blueprint.DisplayName,
                Json = JsonSerializer.Serialize(blueprint with { Version = 1 }, WriteOptions),
                Version = 1,
                UpdatedUtc = DateTime.UtcNow
            };
            db.Insert(newRow);

            engine.UpdateDefinition(blueprint.DefinitionKey, blueprint with { Version = 1 });
            return Task.FromResult(new ServiceBlueprintSaveResult(Saved: true, CurrentVersion: 1, Location: "prismCmsServiceBlueprint"));
        }

        if (existing.Version != expectedVersion)
        {
            return Task.FromResult(new ServiceBlueprintSaveResult(Saved: false, CurrentVersion: existing.Version, Location: "prismCmsServiceBlueprint"));
        }

        var newVersion = expectedVersion + 1;
        var toSave = blueprint with { Version = newVersion };

        // Atomic compare-and-swap: only the writer that still sees `expectedVersion` wins the race.
        var rowsAffected = db.Execute(
            "UPDATE prismCmsServiceBlueprint SET DisplayName = @0, Json = @1, Version = @2, UpdatedUtc = @3 " +
            "WHERE DefinitionKey = @4 AND Version = @5",
            blueprint.DisplayName,
            JsonSerializer.Serialize(toSave, WriteOptions),
            newVersion,
            DateTime.UtcNow,
            blueprint.DefinitionKey,
            expectedVersion);

        if (rowsAffected == 0)
        {
            var current = db.FirstOrDefault<PrismCmsServiceBlueprintSchema>(
                "SELECT * FROM prismCmsServiceBlueprint WHERE DefinitionKey = @0", blueprint.DefinitionKey);
            return Task.FromResult(new ServiceBlueprintSaveResult(
                Saved: false, CurrentVersion: current?.Version ?? existing.Version, Location: "prismCmsServiceBlueprint"));
        }

        engine.UpdateDefinition(blueprint.DefinitionKey, toSave);
        return Task.FromResult(new ServiceBlueprintSaveResult(Saved: true, CurrentVersion: newVersion, Location: "prismCmsServiceBlueprint"));
    }

    public Task<bool> DeleteAsync(string definitionKey, CancellationToken ct = default)
    {
        using var db = databaseFactory.CreateDatabase();
        var rowsAffected = db.Execute(
            "DELETE FROM prismCmsServiceBlueprint WHERE DefinitionKey = @0", definitionKey);

        if (rowsAffected > 0)
        {
            engine.RemoveDefinition(definitionKey);
        }

        return Task.FromResult(rowsAffected > 0);
    }
}
