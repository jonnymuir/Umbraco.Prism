using Microsoft.Extensions.Logging;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace Wayfinder.Engine.Stores;

/// <summary>
/// Serves a single, caller-supplied definition. Used to wire a real <c>ProcessManagerEngine</c>
/// against one blueprint at a time — e.g. for dry-run simulation of a definition that isn't
/// (yet) persisted anywhere.
/// </summary>
public sealed class SingleDefinitionServiceBlueprintStore(ServiceBlueprint definition) : IServiceBlueprintStore
{
    public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger) =>
        new Dictionary<string, ServiceBlueprint>(StringComparer.OrdinalIgnoreCase)
        {
            [definition.DefinitionKey] = definition
        };
}
