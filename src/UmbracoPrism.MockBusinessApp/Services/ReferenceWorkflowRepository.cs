using System.Reflection;
using System.Text.Json;
using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Loads the four canonical reference workflows from the flattened workflow seed contract.
/// </summary>
public static class ReferenceWorkflowRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true
    };

    private static readonly string[] ReferenceWorkflowKeys =
    [
        "planning",
        "community-enquiry",
        "information-request",
        "payment-demo"
    ];

    public static IReadOnlyList<KeyValuePair<string, WorkflowDefinitionFile>> GetReferenceWorkflows()
    {
        var seedDirectory = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory,
            "workflow-seeds");

        return ReferenceWorkflowKeys
            .Select(key => new KeyValuePair<string, WorkflowDefinitionFile>(key, LoadDefinition(seedDirectory, key)))
            .ToArray();
    }

    private static WorkflowDefinitionFile LoadDefinition(string seedDirectory, string workflowKey)
    {
        var filePath = Path.Combine(seedDirectory, $"{workflowKey}.json");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Reference workflow seed '{workflowKey}' was not found.", filePath);
        }

        WorkflowDefinitionFile? definition;

        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinitionFile>(
            File.ReadAllText(filePath),
            JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Reference workflow seed '{workflowKey}' could not be deserialized.", ex);
        }

        if (definition is null)
        {
            throw new InvalidOperationException($"Reference workflow seed '{workflowKey}' could not be deserialized.");
        }

        return definition;
    }
}
