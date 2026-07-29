using System.Reflection;
using System.Text.Json;
using Wayfinder.Models.ServiceDesign;

namespace UmbracoPrism.MockBusinessApp.Services;

/// <summary>
/// Loads the canonical reference workflows from the flattened workflow seed contract.
/// </summary>
public static class ReferenceServiceBlueprintRepository
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
        "payment-demo",
        "money-modeller"
    ];

    public static IReadOnlyList<KeyValuePair<string, ServiceBlueprint>> GetReferenceWorkflows()
    {
        var seedDirectory = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory,
            "service-blueprints");

        return ReferenceWorkflowKeys
            .Select(key => new KeyValuePair<string, ServiceBlueprint>(key, LoadDefinition(seedDirectory, key)))
            .ToArray();
    }

    private static ServiceBlueprint LoadDefinition(string seedDirectory, string workflowKey)
    {
        var filePath = Path.Combine(seedDirectory, $"{workflowKey}.json");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Reference workflow seed '{workflowKey}' was not found.", filePath);
        }

        ServiceBlueprint? definition;

        try
        {
            definition = JsonSerializer.Deserialize<ServiceBlueprint>(
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
