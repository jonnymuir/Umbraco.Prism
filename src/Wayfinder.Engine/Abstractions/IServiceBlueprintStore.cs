using Microsoft.Extensions.Logging;
using Wayfinder.Models.ServiceDesign;

namespace Wayfinder.Engine.Abstractions;

public interface IServiceBlueprintStore
{
    IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger);
}
