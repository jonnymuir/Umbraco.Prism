using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Implementation of IWorkflowSeedService that loads workflow definitions from embedded resources.
/// </summary>
public class WorkflowSeedServiceImpl : IWorkflowSeedService
{
    private readonly ILogger<WorkflowSeedServiceImpl> _logger;

    public WorkflowSeedServiceImpl(ILogger<WorkflowSeedServiceImpl> logger)
    {
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workflow definition seeding from embedded resources");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(r => r.Contains("workflow-seeds") && r.EndsWith(".json"))
            .ToList();

        _logger.LogInformation("Found {Count} workflow seed resources", resourceNames.Count);

        foreach (var resourceName in resourceNames)
        {
            try
            {
                _logger.LogDebug("Loading workflow seed resource: {ResourceName}", resourceName);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger.LogWarning("Could not load resource stream for {ResourceName}", resourceName);
                    continue;
                }

                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync(cancellationToken);

                // TODO: Parse JSON and call IWorkflowDefinitionRepository.UpsertAsync()
                // This will be implemented by Blathers when the repository is available
                
                _logger.LogInformation("Loaded workflow seed: {ResourceName} ({Length} bytes)", resourceName, json.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed workflow from resource {ResourceName}", resourceName);
            }
        }

        _logger.LogInformation("Workflow definition seeding completed");
    }
}
