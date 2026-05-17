using System.Text.Json;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.WorkflowRuntime.Stores;

public sealed class FilesystemWorkflowDefinitionStore(string workflowSeedPath) : IWorkflowDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public IReadOnlyDictionary<string, WorkflowDefinitionFile> LoadDefinitions(ILogger logger)
    {
        var definitions = new Dictionary<string, WorkflowDefinitionFile>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(workflowSeedPath))
        {
            logger.LogWarning(
                "workflow-seeds directory not found at {Path}; no workflow definitions loaded.",
                workflowSeedPath);
            return definitions;
        }

        foreach (var file in Directory.GetFiles(workflowSeedPath, "*.json"))
        {
            try
            {
                var definition = JsonSerializer.Deserialize<WorkflowDefinitionFile>(
                    File.ReadAllText(file),
                    JsonOptions);

                if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionKey))
                {
                    continue;
                }

                definitions[definition.DefinitionKey] = definition;
                logger.LogInformation(
                    "Loaded workflow definition '{Key}' from {File}",
                    definition.DefinitionKey,
                    Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load workflow definition from {File}", file);
            }
        }

        return definitions;
    }
}
