using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using UmbracoPrism.Core.Models.Workflow;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Implementation of IWorkflowSeedService that loads workflow definitions from embedded resources.
/// </summary>
public class WorkflowSeedServiceImpl : IWorkflowSeedService
{
    private readonly ILogger<WorkflowSeedServiceImpl> _logger;
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly WorkflowElementTypeSeeder _elementTypeSeeder;

    public WorkflowSeedServiceImpl(
        ILogger<WorkflowSeedServiceImpl> logger,
        IWorkflowDefinitionRepository repository,
        WorkflowElementTypeSeeder elementTypeSeeder)
    {
        _logger = logger;
        _repository = repository;
        _elementTypeSeeder = elementTypeSeeder;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting workflow definition seeding from embedded resources");

        // First: Ensure Element Types exist
        try
        {
            await _elementTypeSeeder.EnsureElementTypesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed workflow element types");
            // Continue with workflow definitions even if element types fail
        }

        // Then: Load and seed workflow definitions
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

                // Parse JSON into WorkflowDefinition
                var seedData = JsonSerializer.Deserialize<WorkflowSeedData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (seedData == null)
                {
                    _logger.LogWarning("Failed to deserialize workflow seed: {ResourceName}", resourceName);
                    continue;
                }

                // Map seed data to WorkflowDefinition
                var definition = new WorkflowDefinition
                {
                    TenantId = "default",
                    DefinitionKey = seedData.DefinitionKey,
                    DisplayName = seedData.DisplayName,
                    Version = seedData.Version.ToString(),
                    States = seedData.States,
                    Transitions = seedData.Transitions,
                    InitialState = seedData.InitialState ?? seedData.States.FirstOrDefault()?.StateKey ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Upsert to repository
                await _repository.UpsertAsync(definition);
                
                _logger.LogInformation("Seeded workflow definition: {DefinitionKey} v{Version}", definition.DefinitionKey, definition.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed workflow from resource {ResourceName}", resourceName);
            }
        }

        _logger.LogInformation("Workflow definition seeding completed");
    }

    /// <summary>
    /// DTO for deserializing workflow seed JSON files.
    /// </summary>
    private class WorkflowSeedData
    {
        public string DefinitionKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Version { get; set; }
        public string? InitialState { get; set; }
        public List<WorkflowState> States { get; set; } = new();
        public List<WorkflowTransition> Transitions { get; set; } = new();
    }
}
