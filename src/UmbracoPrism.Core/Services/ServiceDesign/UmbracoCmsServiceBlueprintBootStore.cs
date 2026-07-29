using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Persistence;
using UmbracoPrism.Core.Persistence;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.Core.Services.ServiceDesign;

/// <summary>
/// Boot-time <see cref="IServiceBlueprintStore"/> that seeds <c>CmsProcessManager</c> from the
/// prismCmsServiceBlueprint table at startup. Deliberately has no dependency on
/// <c>IProcessManager</c> — unlike <see cref="UmbracoCmsServiceBlueprintStore"/> (the
/// authoring-side store), which pushes saves back into the live engine and therefore must depend
/// on it. Depending on the engine here would create a DI cycle at construction time.
/// </summary>
public sealed class UmbracoCmsServiceBlueprintBootStore(IUmbracoDatabaseFactory databaseFactory)
    : IServiceBlueprintStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true
    };

    public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger)
    {
        using var db = databaseFactory.CreateDatabase();
        var rows = db.Fetch<PrismCmsServiceBlueprintSchema>("SELECT * FROM prismCmsServiceBlueprint");

        var definitions = new Dictionary<string, ServiceBlueprint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            try
            {
                var blueprint = JsonSerializer.Deserialize<ServiceBlueprint>(row.Json, ReadOptions);
                if (blueprint is not null)
                {
                    definitions[row.DefinitionKey] = blueprint with { Version = row.Version };
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize CMS Service Blueprint '{Key}' at boot; skipping.", row.DefinitionKey);
            }
        }

        logger.LogInformation("CMS Service Blueprint boot store loaded {Count} definition(s) from the database.", definitions.Count);
        return definitions;
    }
}
