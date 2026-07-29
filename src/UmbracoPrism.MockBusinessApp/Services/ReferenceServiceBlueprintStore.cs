using Microsoft.Extensions.Logging;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// In-memory runtime definition store seeded from the flattened reference workflow contract.
/// </summary>
public sealed class ReferenceServiceBlueprintStore : IServiceBlueprintStore
{
    public ReferenceServiceBlueprintStore()
    {
    }

    public ReferenceServiceBlueprintStore(object? _)
    {
    }

    public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger)
    {
        var definitions = new Dictionary<string, ServiceBlueprint>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, definition) in ReferenceServiceBlueprintRepository.GetReferenceWorkflows())
        {
            definitions[key] = definition;
            logger.LogInformation(
                "Loaded reference workflow '{Key}' as runtime lookup key for {DisplayName}",
                key,
                definition.DisplayName);
        }

        return definitions;
    }
}
