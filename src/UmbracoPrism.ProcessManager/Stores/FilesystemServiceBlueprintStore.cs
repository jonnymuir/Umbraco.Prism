using System.Text.Json;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.ProcessManager.Abstractions;

namespace UmbracoPrism.ProcessManager.Stores;

public sealed class FilesystemServiceBlueprintStore(string blueprintSeedPath) : IServiceBlueprintStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public IReadOnlyDictionary<string, ServiceBlueprint> LoadDefinitions(ILogger logger)
    {
        var definitions = new Dictionary<string, ServiceBlueprint>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(blueprintSeedPath))
        {
            logger.LogWarning(
                "workflow-seeds directory not found at {Path}; no service blueprints loaded.",
                blueprintSeedPath);
            return definitions;
        }

        foreach (var file in Directory.GetFiles(blueprintSeedPath, "*.json"))
        {
            try
            {
                var definition = JsonSerializer.Deserialize<ServiceBlueprint>(
                    File.ReadAllText(file),
                    JsonOptions);

                if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionKey))
                {
                    continue;
                }

                definitions[definition.DefinitionKey] = definition;
                logger.LogInformation(
                    "Loaded service blueprint '{Key}' from {File}",
                    definition.DefinitionKey,
                    Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load service blueprint from {File}", file);
            }
        }

        return definitions;
    }
}
