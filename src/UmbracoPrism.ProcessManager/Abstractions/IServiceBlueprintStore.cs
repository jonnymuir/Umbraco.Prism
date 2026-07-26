using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.ServiceDesign;

namespace UmbracoPrism.ProcessManager.Abstractions;

public interface IServiceBlueprintStore
{
    IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger);
}
